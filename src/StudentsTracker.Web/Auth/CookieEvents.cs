using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Auth;

public static class CookieEvents
{
    public const string StampClaim = "stamp";

    /// <summary>Штамп зависит от хеша пароля и роли: смена пароля или роли обнуляет старые сессии.</summary>
    public static string Stamp(User u) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{u.PasswordHash}|{u.Role}")))[..16];

    /// <summary>На каждый запрос проверяет, что пользователь существует, активен и штамп совпадает.</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.Principal?.FindFirstValue(StampClaim);
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        var user = int.TryParse(idText, out var id)
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
            : null;

        if (user is not { IsActive: true } || user.Role != context.Principal!.FindFirstValue(ClaimTypes.Role)
            || Stamp(user) != stamp)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
