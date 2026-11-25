using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

/// <summary>
/// Итоги по отметкам. Percent = (present + late) / (marked − excused) × 100; null, если знаменатель равен 0.
/// Занятия без отметки студента в расчёт не входят.
/// </summary>
public record Summary(int Present, int Late, int Absent, int Excused, int Marked, decimal? Percent);
public record SubjectSummary(int SubjectId, string SubjectName, Summary Summary);
public record AttendanceRecord(int LessonId, DateOnly Date, short PairNumber, int SubjectId, string SubjectName, string Status, string? Note);
public record StudentInfo(int Id, string FullName, int GroupId, string GroupName);
public record StudentReportDto(StudentInfo Student, Summary Total, List<SubjectSummary> BySubject, List<AttendanceRecord> Records);
public record GroupStudentSummary(int StudentId, string FullName, Summary Summary);
public record GroupReportDto(int GroupId, string GroupName, Summary Total, List<SubjectSummary> BySubject, List<GroupStudentSummary> Students);

[ApiController]
public class ReportsController(AppDbContext db) : ApiControllerBase
{
    private record MarkRow(int StudentId, int LessonId, DateOnly Date, short Pair, int SubjectId, string SubjectName,
        string Status, string? Note);

    [HttpGet("api/reports/student/{id:int}")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> StudentReport(int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? subjectId)
    {
        if (from > to) return BadRequest("Дата «с» позже даты «по»");
        var student = await db.Students.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new StudentInfo(s.Id, s.FullName, s.GroupId, s.Group.Name)).FirstOrDefaultAsync();
        if (student is null) return NotFound("Студент не найден");

        var allowed = await AllowedSubjectsAsync(student.GroupId);
        if (allowed is not null && allowed.Count == 0) return Forbid();

        return Ok(await BuildStudentReportAsync(student, from, to, subjectId, allowed));
    }

    [HttpGet("api/reports/group/{id:int}")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> GroupReport(int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? subjectId)
    {
        if (from > to) return BadRequest("Дата «с» позже даты «по»");
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound("Группа не найдена");

        var allowed = await AllowedSubjectsAsync(id);
        if (allowed is not null && allowed.Count == 0) return Forbid();

        var students = await db.Students.AsNoTracking().Where(s => s.GroupId == id)
            .OrderBy(s => s.FullName).Select(s => new { s.Id, s.FullName }).ToListAsync();
        var rows = await LoadRowsAsync(db.Attendance.AsNoTracking().Where(a => a.Lesson.GroupId == id), from, to, subjectId, allowed);

        return Ok(new GroupReportDto(group.Id, group.Name, Summarize(rows), BySubject(rows),
            students.Select(s => new GroupStudentSummary(s.Id, s.FullName,
                Summarize(rows.Where(r => r.StudentId == s.Id)))).ToList()));
    }

    /// <summary>Своя посещаемость студента.</summary>
    [HttpGet("api/me/attendance")]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> MyAttendance([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? subjectId)
    {
        if (from > to) return BadRequest("Дата «с» позже даты «по»");
        var me = CurrentUserId;
        var student = await db.Students.AsNoTracking().Where(s => s.UserId == me)
            .Select(s => new StudentInfo(s.Id, s.FullName, s.GroupId, s.Group.Name)).FirstOrDefaultAsync();
        if (student is null) return NotFound("Студент не найден");

        return Ok(await BuildStudentReportAsync(student, from, to, subjectId, null));
    }

    // ---------- Вспомогательное ----------

    /// <summary>
    /// null — ограничений нет (админ). Для преподавателя — предметы, назначенные ему в этой группе
    /// (пустой список означает, что группа не его).
    /// </summary>
    private async Task<List<int>?> AllowedSubjectsAsync(int groupId)
    {
        if (User.IsInRole(Roles.Admin)) return null;
        var me = CurrentUserId;
        return await db.TeachingAssignments.Where(a => a.TeacherId == me && a.GroupId == groupId)
            .Select(a => a.SubjectId).ToListAsync();
    }

    private async Task<StudentReportDto> BuildStudentReportAsync(StudentInfo student, DateOnly? from, DateOnly? to,
        int? subjectId, List<int>? allowedSubjects)
    {
        var rows = await LoadRowsAsync(db.Attendance.AsNoTracking().Where(a => a.StudentId == student.Id),
            from, to, subjectId, allowedSubjects);
        var records = rows.OrderByDescending(r => r.Date).ThenBy(r => r.Pair)
            .Select(r => new AttendanceRecord(r.LessonId, r.Date, r.Pair, r.SubjectId, r.SubjectName, r.Status, r.Note)).ToList();
        return new StudentReportDto(student, Summarize(rows), BySubject(rows), records);
    }

    private static async Task<List<MarkRow>> LoadRowsAsync(IQueryable<Attendance> source, DateOnly? from, DateOnly? to,
        int? subjectId, List<int>? allowedSubjects)
    {
        if (from is not null) source = source.Where(a => a.Lesson.LessonDate >= from);
        if (to is not null) source = source.Where(a => a.Lesson.LessonDate <= to);
        if (subjectId is not null) source = source.Where(a => a.Lesson.SubjectId == subjectId);
        if (allowedSubjects is not null) source = source.Where(a => allowedSubjects.Contains(a.Lesson.SubjectId));

        return await source.Select(a => new MarkRow(a.StudentId, a.LessonId, a.Lesson.LessonDate, a.Lesson.PairNumber,
            a.Lesson.SubjectId, a.Lesson.Subject.Name, a.Status, a.Note)).ToListAsync();
    }

    private static List<SubjectSummary> BySubject(List<MarkRow> rows) =>
        rows.GroupBy(r => new { r.SubjectId, r.SubjectName })
            .OrderBy(g => g.Key.SubjectName)
            .Select(g => new SubjectSummary(g.Key.SubjectId, g.Key.SubjectName, Summarize(g))).ToList();

    private static Summary Summarize(IEnumerable<MarkRow> rows)
    {
        int present = 0, late = 0, absent = 0, excused = 0;
        foreach (var r in rows)
        {
            switch (r.Status)
            {
                case AttendanceStatus.Present: present++; break;
                case AttendanceStatus.Late: late++; break;
                case AttendanceStatus.Absent: absent++; break;
                case AttendanceStatus.Excused: excused++; break;
            }
        }
        var marked = present + late + absent + excused;
        var denominator = marked - excused;
        decimal? percent = denominator == 0 ? null : Math.Round((present + late) * 100m / denominator, 1);
        return new Summary(present, late, absent, excused, marked, percent);
    }
}
