using System.Text.RegularExpressions;

namespace StudentsTracker.Web.Auth;

public static partial class LoginRules
{
    public const int MinPasswordLength = 8;

    /// <summary>Латиница в нижнем регистре, цифры, точка, дефис, подчёркивание; 3–50 символов.</summary>
    [GeneratedRegex("^[a-z0-9._-]{3,50}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string login) => Pattern().IsMatch(login);
}
