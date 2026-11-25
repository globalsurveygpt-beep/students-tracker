using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Auth;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public record StudentRequest(string? FullName, int GroupId);
public record StudentAccountRequest(string? Login);
public record StudentDto(int Id, string FullName, int GroupId, string GroupName, int? UserId, string? Login, bool? IsActive);
public record StudentAccountDto(string Login, string TemporaryPassword);

[ApiController]
[Route("api/students")]
public class StudentsController(AppDbContext db) : ApiControllerBase
{
    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>Админ видит всех студентов, преподаватель — только студентов групп из своих назначений.</summary>
    [HttpGet]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int? groupId)
    {
        var query = db.Students.AsNoTracking().AsQueryable();
        if (User.IsInRole(Roles.Teacher))
        {
            var me = CurrentUserId;
            query = query.Where(s => db.TeachingAssignments.Any(a => a.TeacherId == me && a.GroupId == s.GroupId));
        }
        if (groupId is not null) query = query.Where(s => s.GroupId == groupId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{EscapeLike(q.Trim())}%";
            query = query.Where(s => EF.Functions.ILike(s.FullName, pattern, "\\"));
        }

        var rows = await Project(query.OrderBy(s => s.FullName)).ToListAsync();
        return Ok(rows.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Policy.AdminOrTeacher)]
    public async Task<IActionResult> Get(int id)
    {
        var s = await Project(db.Students.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync();
        if (s is null) return NotFound("Студент не найден");
        if (User.IsInRole(Roles.Teacher) && !await TeachesGroup(s.GroupId)) return Forbid();
        return Ok(ToDto(s));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(StudentRequest request)
    {
        if (!TryCleanText(request.FullName, 200, out var fullName)) return BadRequest("ФИО: от 1 до 200 символов");
        if (!await db.Groups.AnyAsync(g => g.Id == request.GroupId)) return BadRequest("Группа не найдена");

        var student = new Student { FullName = fullName, GroupId = request.GroupId };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = student.Id }, ToDto(await Project(db.Students.AsNoTracking().Where(s => s.Id == student.Id)).FirstAsync()));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, StudentRequest request)
    {
        var student = await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound("Студент не найден");
        if (!TryCleanText(request.FullName, 200, out var fullName)) return BadRequest("ФИО: от 1 до 200 символов");
        if (!await db.Groups.AnyAsync(g => g.Id == request.GroupId)) return BadRequest("Группа не найдена");

        if (request.GroupId != student.GroupId
            && await db.Attendance.AnyAsync(a => a.StudentId == id))
            return Conflict("Студента нельзя перевести в другую группу: у него есть отметки посещаемости");

        student.FullName = fullName;
        student.GroupId = request.GroupId;
        if (student.User is not null) student.User.FullName = fullName;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Студент без отметок удаляется вместе со своей учётной записью одной транзакцией.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound("Студент не найден");
        if (await db.Attendance.AnyAsync(a => a.StudentId == id))
            return Conflict("Студента нельзя удалить: у него есть отметки посещаемости. Отключите его учётную запись");

        db.Students.Remove(student);
        if (student.User is not null) db.Users.Remove(student.User);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/account")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CreateAccount(int id, StudentAccountRequest request)
    {
        var student = await db.Students.FindAsync(id);
        if (student is null) return NotFound("Студент не найден");
        if (student.UserId is not null) return Conflict("У студента уже есть учётная запись");

        var login = (request.Login ?? "").Trim().ToLowerInvariant();
        if (!LoginRules.IsValid(login))
            return BadRequest("Логин: латиница в нижнем регистре, цифры, точка, дефис, подчёркивание; от 3 до 50 символов");
        if (await db.Users.AnyAsync(u => u.Login == login)) return Conflict("Пользователь с таким логином уже есть");

        var password = TempPassword.Generate();
        var user = new User { Login = login, FullName = student.FullName, Role = Roles.Student };
        user.PasswordHash = _hasher.HashPassword(user, password);
        student.User = user;
        await db.SaveChangesAsync();
        return Ok(new StudentAccountDto(login, password));
    }

    private static IQueryable<StudentRow> Project(IQueryable<Student> source) =>
        source.Select(s => new StudentRow(
            s.Id, s.FullName, s.GroupId, s.Group.Name, s.UserId,
            s.User != null ? s.User.Login : null,
            s.User != null ? s.User.IsActive : (bool?)null));

    private Task<bool> TeachesGroup(int groupId)
    {
        var me = CurrentUserId;
        return db.TeachingAssignments.AnyAsync(a => a.TeacherId == me && a.GroupId == groupId);
    }

    private static StudentDto ToDto(StudentRow s) =>
        new(s.Id, s.FullName, s.GroupId, s.GroupName, s.UserId, s.Login, s.IsActive);

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private record StudentRow(int Id, string FullName, int GroupId, string GroupName, int? UserId, string? Login, bool? IsActive);
}
