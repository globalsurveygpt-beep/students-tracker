using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Data;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Auth;

/// <summary>
/// Создаёт первого администратора из конфигурации (Admin:Login, Admin:Password, Admin:FullName),
/// если в таблице users нет ни одного администратора. Пароль задаётся в user-secrets или переменных окружения.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");

        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync(u => u.Role == Roles.Admin))
            return;

        var login = config["Admin:Login"]?.Trim().ToLowerInvariant();
        var password = config["Admin:Password"];
        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("Администратора нет, а Admin:Login / Admin:Password не заданы: вход в систему невозможен.");
            return;
        }

        if (!LoginRules.IsValid(login) || password.Length < LoginRules.MinPasswordLength)
        {
            logger.LogError("Admin:Login или Admin:Password не соответствуют правилам (логин по шаблону, пароль от {Min} символов).",
                LoginRules.MinPasswordLength);
            return;
        }

        var admin = new User
        {
            Login = login,
            Role = Roles.Admin,
            FullName = config["Admin:FullName"] ?? "Администратор",
        };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Создан первый администратор «{Login}».", login);
    }
}
