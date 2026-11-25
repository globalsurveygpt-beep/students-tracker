using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Auth;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public record CreateUserRequest(string? Login, string? FullName, string? Role);
public record UpdateUserRequest(string? FullName, string? Role);
public record SetActiveRequest(bool IsActive);
public record UserDto(int Id, string Login, string FullName, string Role, bool IsActive);
public record UserWithPasswordDto(UserDto User, string TemporaryPassword);
public record PasswordDto(string TemporaryPassword);

/// <summary>Учётные записи сотрудников (admin, teacher). Учётные записи студентов создаются через /api/students/{id}/account.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db) : ApiControllerBase
{
    private readonly PasswordHasher<User> _hasher = new();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? role)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Role == role);
        return Ok(await query.OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.Login, u.FullName, u.Role, u.IsActive)).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return u is null ? NotFound("Пользователь не найден") : Ok(ToDto(u));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var login = (request.Login ?? "").Trim().ToLowerInvariant();
        if (!LoginRules.IsValid(login))
            return BadRequest("Логин: латиница в нижнем регистре, цифры, точка, дефис, подчёркивание; от 3 до 50 символов");
        if (!TryCleanText(request.FullName, 200, out var fullName))
            return BadRequest("ФИО: от 1 до 200 символов");
        if (request.Role is not (Roles.Admin or Roles.Teacher))
            return BadRequest("Роль сотрудника: admin или teacher");
        if (await db.Users.AnyAsync(u => u.Login == login))
            return Conflict("Пользователь с таким логином уже есть");

        var password = TempPassword.Generate();
        var user = new User { Login = login, FullName = fullName, Role = request.Role };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new UserWithPasswordDto(ToDto(user), password));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound("Пользователь не найден");
        if (!TryCleanText(request.FullName, 200, out var fullName))
            return BadRequest("ФИО: от 1 до 200 символов");

        var role = request.Role ?? user.Role;
        if (role != user.Role)
        {
            if (user.Role == Roles.Student || role is not (Roles.Admin or Roles.Teacher))
                return BadRequest("Роль можно менять только между admin и teacher");
            if (id == CurrentUserId)
                return Conflict("Нельзя снять с себя роль администратора");
            if (user.Role == Roles.Teacher
                && (await db.TeachingAssignments.AnyAsync(a => a.TeacherId == id)
                    || await db.Lessons.AnyAsync(l => l.TeacherId == id)))
                return Conflict("Роль преподавателя нельзя сменить: есть назначения или занятия");
        }

        user.FullName = fullName;
        user.Role = role;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, SetActiveRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound("Пользователь не найден");
        if (id == CurrentUserId && !request.IsActive)
            return Conflict("Нельзя отключить собственную учётную запись");

        user.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound("Пользователь не найден");

        var password = TempPassword.Generate();
        user.PasswordHash = _hasher.HashPassword(user, password);
        await db.SaveChangesAsync();
        return Ok(new PasswordDto(password));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound("Пользователь не найден");
        if (id == CurrentUserId) return Conflict("Нельзя удалить собственную учётную запись");

        if (await db.Students.AnyAsync(s => s.UserId == id))
            return Conflict("Учётная запись студента удаляется только вместе со студентом");
        if (await db.TeachingAssignments.AnyAsync(a => a.TeacherId == id)
            || await db.Lessons.AnyAsync(l => l.TeacherId == id))
            return Conflict("Пользователя нельзя удалить: есть назначения или занятия. Отключите учётную запись");

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Login, u.FullName, u.Role, u.IsActive);
}
