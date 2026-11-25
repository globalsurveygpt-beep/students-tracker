using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public record GroupRequest(string? Name, short StartYear);
public record GroupDto(int Id, string Name, short StartYear, int StudentCount);

[ApiController]
[Route("api/groups")]
public class GroupsController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> List() =>
        Ok(await db.Groups.AsNoTracking().OrderBy(g => g.Name)
            .Select(g => new GroupDto(g.Id, g.Name, g.StartYear, db.Students.Count(s => s.GroupId == g.Id)))
            .ToListAsync());

    [HttpGet("{id:int}")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> Get(int id)
    {
        var dto = await db.Groups.AsNoTracking().Where(g => g.Id == id)
            .Select(g => new GroupDto(g.Id, g.Name, g.StartYear, db.Students.Count(s => s.GroupId == g.Id)))
            .FirstOrDefaultAsync();
        return dto is null ? NotFound("Группа не найдена") : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(GroupRequest request)
    {
        var error = Validate(request, out var name);
        if (error is not null) return error;
        if (await db.Groups.AnyAsync(g => g.Name == name)) return Conflict("Группа с таким названием уже есть");

        var group = new Group { Name = name, StartYear = request.StartYear };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = group.Id }, new GroupDto(group.Id, group.Name, group.StartYear, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, GroupRequest request)
    {
        var group = await db.Groups.FindAsync(id);
        if (group is null) return NotFound("Группа не найдена");
        var error = Validate(request, out var name);
        if (error is not null) return error;
        if (await db.Groups.AnyAsync(g => g.Name == name && g.Id != id)) return Conflict("Группа с таким названием уже есть");

        group.Name = name;
        group.StartYear = request.StartYear;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await db.Groups.FindAsync(id);
        if (group is null) return NotFound("Группа не найдена");

        if (await db.Students.AnyAsync(s => s.GroupId == id)
            || await db.TeachingAssignments.AnyAsync(a => a.GroupId == id)
            || await db.Lessons.AnyAsync(l => l.GroupId == id))
            return Conflict("Группу нельзя удалить: в ней есть студенты, назначения или занятия");

        db.Groups.Remove(group);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private IActionResult? Validate(GroupRequest r, out string name)
    {
        if (!TryCleanText(r.Name, 50, out name)) return BadRequest("Название группы: от 1 до 50 символов");
        if (r.StartYear is < 2000 or > 2100) return BadRequest("Год начала обучения: от 2000 до 2100");
        return null;
    }
}
