using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Auth;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public record LoginRequest(string? Login, string? Password);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record MeResponse(int Id, string Login, string FullName, string Role);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db) : ControllerBase
{
    private readonly PasswordHasher<User> _hasher = new();

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var login = (request.Login ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login);

        // Одинаковый ответ для неверного логина, пароля и отключённой учётной записи.
        var ok = user is { IsActive: true }
                 && !string.IsNullOrEmpty(request.Password)
                 && _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                    != PasswordVerificationResult.Failed;
        if (!ok)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Неверный логин или пароль");

        await SignInAsync(user!);
        return Ok(ToMe(user!));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await CurrentUserAsync();
        return user is null ? Unauthorized() : Ok(ToMe(user));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();

        if (string.IsNullOrEmpty(request.CurrentPassword)
            || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
               == PasswordVerificationResult.Failed)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Текущий пароль указан неверно");

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < LoginRules.MinPasswordLength)
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: $"Новый пароль должен быть не короче {LoginRules.MinPasswordLength} символов");

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync();
        // Перевыпуск cookie: штамп безопасности меняется вместе с хешем пароля.
        await SignInAsync(user);
        return NoContent();
    }

    private async Task<User?> CurrentUserAsync()
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idText, out var id) ? await db.Users.FindAsync(id) : null;
    }

    private async Task SignInAsync(User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(CookieEvents.StampClaim, CookieEvents.Stamp(user)),
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private static MeResponse ToMe(User u) => new(u.Id, u.Login, u.FullName, u.Role);
}
