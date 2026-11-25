using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Controllers;

public static class Policy
{
    public const string AdminOrTeacher = $"{Roles.Admin},{Roles.Teacher}";
}

/// <summary>
/// Переводит нарушения уникальности (23505) и внешних ключей (23503) из PostgreSQL в ответ 409.
/// Срабатывает, если проверка в обработчике не успела поймать конфликт (например, гонка запросов).
/// </summary>
public class DbConflictFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateException { InnerException: PostgresException pg })
            return;

        var title = pg.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => "Запись с такими данными уже существует",
            PostgresErrorCodes.ForeignKeyViolation => "Запись связана с другими данными, действие невозможно",
            _ => null,
        };
        if (title is null) return;

        context.Result = new ObjectResult(new ProblemDetails { Status = 409, Title = title }) { StatusCode = 409 };
        context.ExceptionHandled = true;
    }
}

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Conflict(string title) => Problem(statusCode: StatusCodes.Status409Conflict, title: title);
    protected IActionResult BadRequest(string title) => Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
    protected IActionResult NotFound(string title) => Problem(statusCode: StatusCodes.Status404NotFound, title: title);

    protected int CurrentUserId =>
        int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    /// <summary>Пробелы по краям убираются; пустая строка и слишком длинная строка недопустимы.</summary>
    protected static bool TryCleanText(string? value, int maxLength, out string clean)
    {
        clean = (value ?? "").Trim();
        return clean.Length > 0 && clean.Length <= maxLength;
    }
}

public static class TempPassword
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public static string Generate(int length = 12)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
