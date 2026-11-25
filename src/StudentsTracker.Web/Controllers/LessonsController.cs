using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public record CreateLessonRequest(DateOnly Date, short PairNumber, int SubjectId, List<int>? GroupIds, string? Topic);
public record UpdateLessonRequest(DateOnly Date, short PairNumber, int SubjectId, string? Topic);
public record LessonDto(int Id, DateOnly Date, short PairNumber, int GroupId, string GroupName,
    int SubjectId, string SubjectName, int TeacherId, string TeacherName, string? Topic, int MarkCount);

public record MarkItem(int StudentId, string? Status, string? Note);
public record SaveAttendanceRequest(List<MarkItem>? Items);
public record AttendanceRowDto(int StudentId, string FullName, string? Status, string? Note);
public record LessonAttendanceDto(LessonDto Lesson, List<AttendanceRowDto> Students);

[ApiController]
[Route("api/lessons")]
public class LessonsController(AppDbContext db) : ApiControllerBase
{
    private static readonly HashSet<string> Statuses =
        [AttendanceStatus.Present, AttendanceStatus.Absent, AttendanceStatus.Excused, AttendanceStatus.Late];

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ---------- Занятия ----------

    /// <summary>Админ видит все занятия, преподаватель — только свои.</summary>
    [HttpGet]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> List([FromQuery] int? groupId, [FromQuery] int? subjectId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var q = db.Lessons.AsNoTracking().AsQueryable();
        if (User.IsInRole(Roles.Teacher))
        {
            var me = CurrentUserId;
            q = q.Where(l => l.TeacherId == me);
        }
        if (groupId is not null) q = q.Where(l => l.GroupId == groupId);
        if (subjectId is not null) q = q.Where(l => l.SubjectId == subjectId);
        if (from is not null) q = q.Where(l => l.LessonDate >= from);
        if (to is not null) q = q.Where(l => l.LessonDate <= to);

        return Ok(await Project(q.OrderByDescending(l => l.LessonDate).ThenBy(l => l.PairNumber).ThenBy(l => l.Group.Name))
            .ToListAsync());
    }

    /// <summary>Создаёт занятие для одной или нескольких групп. Либо создаются все записи, либо ни одной.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Create(CreateLessonRequest request)
    {
        var groupIds = (request.GroupIds ?? []).Distinct().ToList();
        if (groupIds.Count == 0) return BadRequest("Выберите хотя бы одну группу");
        if (request.PairNumber is < 1 or > 8) return BadRequest("Номер часа: от 1 до 8");
        if (request.Date > Today) return BadRequest("Занятие не может быть позже сегодняшней даты");
        var topic = CleanTopic(request.Topic, out var topicError);
        if (topicError is not null) return topicError;

        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId)) return BadRequest("Предмет не найден");

        var me = CurrentUserId;
        var assigned = await db.TeachingAssignments
            .Where(a => a.TeacherId == me && a.SubjectId == request.SubjectId && groupIds.Contains(a.GroupId))
            .Select(a => a.GroupId).ToListAsync();
        if (assigned.Count != groupIds.Count)
            return Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Нет назначения на этот предмет хотя бы для одной из выбранных групп");

        var busy = await db.Lessons
            .Where(l => groupIds.Contains(l.GroupId) && l.LessonDate == request.Date && l.PairNumber == request.PairNumber)
            .Select(l => l.Group.Name).ToListAsync();
        if (busy.Count > 0)
            return Conflict($"У группы уже есть занятие в этот час: {string.Join(", ", busy)}");

        var lessons = groupIds.Select(g => new Lesson
        {
            LessonDate = request.Date, PairNumber = request.PairNumber, GroupId = g,
            SubjectId = request.SubjectId, TeacherId = me, Topic = topic,
        }).ToList();
        db.Lessons.AddRange(lessons);
        await db.SaveChangesAsync(); // одна транзакция

        var ids = lessons.Select(l => l.Id).ToList();
        var created = await Project(db.Lessons.AsNoTracking().Where(l => ids.Contains(l.Id)).OrderBy(l => l.Group.Name))
            .ToListAsync();
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Update(int id, UpdateLessonRequest request)
    {
        var lesson = await db.Lessons.FindAsync(id);
        if (lesson is null) return NotFound("Занятие не найдено");
        if (lesson.TeacherId != CurrentUserId) return Forbid();

        if (request.PairNumber is < 1 or > 8) return BadRequest("Номер часа: от 1 до 8");
        if (request.Date > Today) return BadRequest("Занятие не может быть позже сегодняшней даты");
        var topic = CleanTopic(request.Topic, out var topicError);
        if (topicError is not null) return topicError;

        if (!await db.TeachingAssignments.AnyAsync(a =>
                a.TeacherId == lesson.TeacherId && a.GroupId == lesson.GroupId && a.SubjectId == request.SubjectId))
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Нет назначения на этот предмет для группы занятия");

        if (await db.Lessons.AnyAsync(l => l.Id != id && l.GroupId == lesson.GroupId
                                           && l.LessonDate == request.Date && l.PairNumber == request.PairNumber))
            return Conflict("У группы уже есть занятие в этот час");

        lesson.LessonDate = request.Date;
        lesson.PairNumber = request.PairNumber;
        lesson.SubjectId = request.SubjectId;
        lesson.Topic = topic;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Delete(int id)
    {
        var lesson = await db.Lessons.FindAsync(id);
        if (lesson is null) return NotFound("Занятие не найдено");
        if (lesson.TeacherId != CurrentUserId) return Forbid();
        if (await db.Attendance.AnyAsync(a => a.LessonId == id))
            return Conflict("Занятие нельзя удалить: по нему уже есть отметки посещаемости");

        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Отметки ----------

    /// <summary>Состав группы с отметками. Студент без отметки возвращается со Status = null.</summary>
    [HttpGet("{id:int}/attendance")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> GetAttendance(int id)
    {
        var lesson = await Project(db.Lessons.AsNoTracking().Where(l => l.Id == id)).FirstOrDefaultAsync();
        if (lesson is null) return NotFound("Занятие не найдено");
        if (!CanAccess(lesson.TeacherId)) return Forbid();

        var marks = await db.Attendance.AsNoTracking().Where(a => a.LessonId == id)
            .ToDictionaryAsync(a => a.StudentId);
        var students = await db.Students.AsNoTracking().Where(s => s.GroupId == lesson.GroupId)
            .OrderBy(s => s.FullName).Select(s => new { s.Id, s.FullName }).ToListAsync();

        var rows = students.Select(s => marks.TryGetValue(s.Id, out var m)
            ? new AttendanceRowDto(s.Id, s.FullName, m.Status, m.Note)
            : new AttendanceRowDto(s.Id, s.FullName, null, null)).ToList();
        return Ok(new LessonAttendanceDto(lesson, rows));
    }

    /// <summary>Отметки ближайшего предыдущего занятия той же группы в тот же день («Как в прошлом часе»).</summary>
    [HttpGet("{id:int}/previous-attendance")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> GetPreviousAttendance(int id)
    {
        var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound("Занятие не найдено");
        if (!CanAccess(lesson.TeacherId)) return Forbid();

        var previousId = await db.Lessons.AsNoTracking()
            .Where(l => l.GroupId == lesson.GroupId && l.LessonDate == lesson.LessonDate && l.PairNumber < lesson.PairNumber)
            .OrderByDescending(l => l.PairNumber).Select(l => (int?)l.Id).FirstOrDefaultAsync();
        if (previousId is null) return NotFound("В этот день у группы нет предыдущего занятия");

        var marks = await db.Attendance.AsNoTracking().Where(a => a.LessonId == previousId)
            .Select(a => new MarkItem(a.StudentId, a.Status, a.Note)).ToListAsync();
        if (marks.Count == 0) return NotFound("В предыдущем занятии нет отметок");
        return Ok(marks);
    }

    /// <summary>Заменяет отметки занятия присланным списком (студентов вне списка отметки лишаются).</summary>
    [HttpPut("{id:int}/attendance")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> SaveAttendance(int id, SaveAttendanceRequest request)
    {
        var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound("Занятие не найдено");
        if (!CanAccess(lesson.TeacherId)) return Forbid();

        var items = request.Items ?? [];
        if (items.Select(i => i.StudentId).Distinct().Count() != items.Count)
            return BadRequest("Студент указан в списке более одного раза");
        if (items.Any(i => i.Status is null || !Statuses.Contains(i.Status)))
            return BadRequest("Статус отметки: present, absent, excused или late");
        if (items.Any(i => i.Note is { Length: > 500 })) return BadRequest("Примечание: не более 500 символов");

        var groupStudents = await db.Students.Where(s => s.GroupId == lesson.GroupId).Select(s => s.Id).ToListAsync();
        if (items.Any(i => !groupStudents.Contains(i.StudentId)))
            return BadRequest("В списке есть студент не из группы этого занятия");

        var existing = await db.Attendance.Where(a => a.LessonId == id).ToListAsync();
        var incoming = items.ToDictionary(i => i.StudentId);

        foreach (var mark in existing)
        {
            if (incoming.TryGetValue(mark.StudentId, out var item))
            {
                mark.Status = item.Status!;
                mark.Note = CleanNote(item.Note);
            }
            else db.Attendance.Remove(mark);
        }
        var have = existing.Select(m => m.StudentId).ToHashSet();
        foreach (var item in items.Where(i => !have.Contains(i.StudentId)))
            db.Attendance.Add(new Attendance
            {
                LessonId = id, StudentId = item.StudentId, Status = item.Status!, Note = CleanNote(item.Note),
            });

        await db.SaveChangesAsync(); // одна транзакция
        return NoContent();
    }

    // ---------- Вспомогательное ----------

    private bool CanAccess(int lessonTeacherId) =>
        User.IsInRole(Roles.Admin) || lessonTeacherId == CurrentUserId;

    private static string? CleanNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private string? CleanTopic(string? topic, out IActionResult? error)
    {
        error = null;
        var clean = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
        if (clean is { Length: > 500 }) error = BadRequest("Тема: не более 500 символов");
        return clean;
    }

    private IQueryable<LessonDto> Project(IQueryable<Lesson> source) =>
        source.Select(l => new LessonDto(l.Id, l.LessonDate, l.PairNumber, l.GroupId, l.Group.Name,
            l.SubjectId, l.Subject.Name, l.TeacherId, l.Teacher.FullName, l.Topic,
            db.Attendance.Count(a => a.LessonId == l.Id)));
}
