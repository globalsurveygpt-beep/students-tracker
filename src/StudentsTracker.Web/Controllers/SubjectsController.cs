using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public record SubjectRequest(string? Name);
public record SubjectDto(int Id, string Name);

[ApiController]
[Route("api/subjects")]
public class SubjectsController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> List() =>
        Ok(await db.Subjects.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name)).ToListAsync());

    [HttpGet("{id:int}")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> Get(int id)
    {
        var s = await db.Subjects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return s is null ? NotFound("Предмет не найден") : Ok(new SubjectDto(s.Id, s.Name));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(SubjectRequest request)
    {
        if (!TryCleanText(request.Name, 200, out var name)) return BadRequest("Название предмета: от 1 до 200 символов");
        if (await db.Subjects.AnyAsync(s => s.Name == name)) return Conflict("Предмет с таким названием уже есть");

        var subject = new Subject { Name = name };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = subject.Id }, new SubjectDto(subject.Id, subject.Name));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, SubjectRequest request)
    {
        var subject = await db.Subjects.FindAsync(id);
        if (subject is null) return NotFound("Предмет не найден");
        if (!TryCleanText(request.Name, 200, out var name)) return BadRequest("Название предмета: от 1 до 200 символов");
        if (await db.Subjects.AnyAsync(s => s.Name == name && s.Id != id)) return Conflict("Предмет с таким названием уже есть");

        subject.Name = name;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var subject = await db.Subjects.FindAsync(id);
        if (subject is null) return NotFound("Предмет не найден");

        if (await db.TeachingAssignments.AnyAsync(a => a.SubjectId == id)
            || await db.Lessons.AnyAsync(l => l.SubjectId == id))
            return Conflict("Предмет нельзя удалить: есть назначения или занятия");

        db.Subjects.Remove(subject);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
