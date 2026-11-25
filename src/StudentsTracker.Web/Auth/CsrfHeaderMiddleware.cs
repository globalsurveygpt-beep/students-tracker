namespace StudentsTracker.Web.Auth;

/// <summary>
/// Защита от CSRF: изменяющие запросы к /api должны содержать заголовок X-Requested-With.
/// Кросс-доменная форма не может его выставить без CORS-разрешения.
/// </summary>
public class CsrfHeaderMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    public async Task InvokeAsync(HttpContext context)
    {
        var isApi = context.Request.Path.StartsWithSegments("/api");
        if (isApi
            && !SafeMethods.Contains(context.Request.Method)
            && !context.Request.Headers.ContainsKey("X-Requested-With"))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 400,
                Title = "Отсутствует заголовок X-Requested-With"
            });
            return;
        }

        await next(context);
    }
}
