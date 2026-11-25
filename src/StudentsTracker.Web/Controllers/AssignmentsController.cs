using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public record AssignmentRequest(int TeacherId, int GroupId, int SubjectId);
public record AssignmentDto(int Id, int TeacherId, string TeacherName, int GroupId, string GroupName, int SubjectId, string SubjectName);

[ApiController]
public class AssignmentsController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("api/assignments")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> List([FromQuery] int? teacherId, [FromQuery] int? groupId) =>
        Ok(await Query(teacherId, groupId, null).ToListAsync());

    [HttpPost("api/assignments")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(AssignmentRequest request)
    {
        var teacher = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.TeacherId);
        if (teacher is not { Role: Roles.Teacher }) return BadRequest("Преподаватель не найден");
        if (!teacher.IsActive) return BadRequest("Учётная запись преподавателя отключена");
        if (!await db.Groups.AnyAsync(g => g.Id == request.GroupId)) return BadRequest("Группа не найдена");
        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId)) return BadRequest("Предмет не найден");
        if (await db.TeachingAssignments.AnyAsync(a =>
                a.TeacherId == request.TeacherId && a.GroupId == request.GroupId && a.SubjectId == request.SubjectId))
            return Conflict("Такое назначение уже есть");

        var assignment = new TeachingAssignment
        {
            TeacherId = request.TeacherId, GroupId = request.GroupId, SubjectId = request.SubjectId,
        };
        db.TeachingAssignments.Add(assignment);
        await db.SaveChangesAsync();
        var dto = await Query(null, null, assignment.Id).FirstAsync();
        return Created($"api/assignments/{assignment.Id}", dto);
    }

    [HttpDelete("api/assignments/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await db.TeachingAssignments.FindAsync(id);
        if (a is null) return NotFound("Назначение не найдено");

        if (await db.Lessons.AnyAsync(l => l.TeacherId == a.TeacherId && l.GroupId == a.GroupId && l.SubjectId == a.SubjectId))
            return Conflict("Назначение нельзя удалить: по нему уже проведены занятия");

        db.TeachingAssignments.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Свои группы и предметы преподавателя.</summary>
    [HttpGet("api/my/assignments")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Mine() =>
        Ok(await Query(CurrentUserId, null, null).ToListAsync());

    private IQueryable<AssignmentDto> Query(int? teacherId, int? groupId, int? id)
    {
        var q = db.TeachingAssignments.AsNoTracking().AsQueryable();
        if (id is not null) q = q.Where(a => a.Id == id);
        if (teacherId is not null) q = q.Where(a => a.TeacherId == teacherId);
        if (groupId is not null) q = q.Where(a => a.GroupId == groupId);
        return q.OrderBy(a => a.Group.Name).ThenBy(a => a.Subject.Name).ThenBy(a => a.Teacher.FullName)
            .Select(a => new AssignmentDto(a.Id, a.TeacherId, a.Teacher.FullName,
                a.GroupId, a.Group.Name, a.SubjectId, a.Subject.Name));
    }
}
