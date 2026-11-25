using StudentsTracker.Web.Auth;
using StudentsTracker.Web.Controllers;

namespace StudentsTracker.Tests;

[Collection("api")]
public class AuthTests(AppFactory f)
{
    [Fact]
    public async Task WrongPasswordAndUnknownLoginGiveTheSameAnswer()
    {
        var api = f.CreateApi();
        var wrongPassword = await api.Post("/api/auth/login", new { login = AppFactory.AdminLogin, password = "definitely-wrong" });
        var unknownLogin = await api.Post("/api/auth/login", new { login = "no.such.user", password = "definitely-wrong" });
        Assert.Equal(401, wrongPassword.Code);
        Assert.Equal(401, unknownLogin.Code);
        Assert.Equal(wrongPassword.Title, unknownLogin.Title);
    }

    [Fact]
    public async Task ChangingRequestsWithoutXRequestedWithAreRejected()
    {
        var api = f.CreateApi(csrfHeader: false);
        var r = await api.Post("/api/auth/login", new { login = AppFactory.AdminLogin, password = AppFactory.AdminPassword });
        Assert.Equal(400, r.Code);
    }

    [Fact]
    public async Task LoginReturnsRoleAndSessionWorks()
    {
        var admin = await f.LoginAsync(AppFactory.AdminLogin.ToUpperInvariant(), AppFactory.AdminPassword); // логин нечувствителен к регистру
        var me = await admin.Get("/api/auth/me");
        Assert.Equal(200, me.Code);
        Assert.Equal("admin", me["role"].GetString());
        Assert.Equal(401, (await f.CreateApi().Get("/api/auth/me")).Code);
    }

    [Fact]
    public async Task ChangePasswordChecksCurrentAndLengthThenSwitchesPassword()
    {
        var w = await World.CreateAsync(f);
        var reset = await w.Admin.Post($"/api/users/{w.T1}/reset-password");
        var old = reset["temporaryPassword"].GetString()!;
        var login = (await w.Admin.Get($"/api/users/{w.T1}"))["login"].GetString()!;
        var api = await f.LoginAsync(login, old);

        Assert.Equal(400, (await api.Post("/api/auth/change-password", new { currentPassword = "wrong-current", newPassword = "NewPassw0rd!" })).Code);
        Assert.Equal(400, (await api.Post("/api/auth/change-password", new { currentPassword = old, newPassword = "short" })).Code);
        Assert.Equal(204, (await api.Post("/api/auth/change-password", new { currentPassword = old, newPassword = "NewPassw0rd!" })).Code);

        Assert.Equal(401, (await f.CreateApi().Post("/api/auth/login", new { login, password = old })).Code);
        await f.LoginAsync(login, "NewPassw0rd!");
    }

    [Fact]
    public async Task DisabledAccountLosesItsSessionAndCannotLogIn()
    {
        var w = await World.CreateAsync(f);
        Assert.Equal(200, (await w.Teacher1.Get("/api/groups")).Code);

        Assert.Equal(204, (await w.Admin.Put($"/api/users/{w.T1}/active", new { isActive = false })).Code);

        Assert.Equal(401, (await w.Teacher1.Get("/api/groups")).Code);
        var login = (await w.Admin.Get($"/api/users/{w.T1}"))["login"].GetString()!;
        var again = await f.CreateApi().Post("/api/auth/login", new { login, password = "whatever-123" });
        Assert.Equal(401, again.Code);
    }

    [Fact]
    public async Task AdminCannotDisableDeleteOrDemoteThemselves()
    {
        var admin = await f.LoginAsync(AppFactory.AdminLogin, AppFactory.AdminPassword);
        var me = (await admin.Get("/api/auth/me"))["id"].GetInt32();
        Assert.Equal(409, (await admin.Put($"/api/users/{me}/active", new { isActive = false })).Code);
        Assert.Equal(409, (await admin.Delete($"/api/users/{me}")).Code);
        Assert.Equal(409, (await admin.Put($"/api/users/{me}", new { fullName = "Admin", role = "teacher" })).Code);
    }
}

[Collection("api")]
public class AccessTests(AppFactory f)
{
    // Ожидаемые коды по ролям: admin, teacher, student, anonymous.
    public static readonly (string Method, string Url, int Admin, int Teacher, int Student, int Anon)[] Matrix =
    [
        ("GET", "/api/groups", 200, 200, 403, 401),
        ("GET", "/api/subjects", 200, 200, 403, 401),
        ("GET", "/api/students", 200, 200, 403, 401),
        ("GET", "/api/users", 200, 403, 403, 401),
        ("GET", "/api/assignments", 200, 403, 403, 401),
        ("GET", "/api/my/assignments", 403, 200, 403, 401),
        ("GET", "/api/lessons", 200, 200, 403, 401),
        ("GET", "/api/me/attendance", 403, 403, 200, 401),
        ("GET", "/api/reports/group/0", 404, 404, 403, 401),
        ("POST", "/api/groups", 400, 403, 403, 401),
        ("POST", "/api/subjects", 400, 403, 403, 401),
        ("POST", "/api/users", 400, 403, 403, 401),
        ("POST", "/api/assignments", 400, 403, 403, 401),
        ("POST", "/api/lessons", 403, 400, 403, 401),
    ];

    [Fact]
    public async Task EachRoleSeesOnlyWhatItIsAllowedTo()
    {
        var w = await World.CreateAsync(f);
        var roles = new (string Name, Api Api, Func<(string, string, int, int, int, int), int> Expected)[]
        {
            ("admin", w.Admin, m => m.Item3), ("teacher", w.Teacher1, m => m.Item4),
            ("student", w.Student, m => m.Item5), ("anonymous", f.CreateApi(), m => m.Item6),
        };
        var failures = new List<string>();
        foreach (var m in Matrix)
            foreach (var role in roles)
            {
                // Пустое тело: у разрешённых ролей это 400 (валидация), у запрещённых — 401/403 до валидации.
                var r = await role.Api.Send(new HttpMethod(m.Method), m.Url, m.Method == "POST" ? new { } : null);
                if (r.Code != role.Expected(m)) failures.Add($"{role.Name} {m.Method} {m.Url}: ожидалось {role.Expected(m)}, получено {r.Code}");
            }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task TeacherSeesOnlyStudentsOfAssignedGroups()
    {
        var w = await World.CreateAsync(f);
        // Преподаватель 2 назначен только на Г1.
        var list = await w.Teacher2.Get("/api/students");
        var groups = list.Json.EnumerateArray().Select(s => s.GetProperty("groupId").GetInt32()).Distinct().ToList();
        Assert.Contains(w.G1, groups);
        Assert.DoesNotContain(w.G2, groups);
        Assert.Equal(403, (await w.Teacher2.Get($"/api/students/{w.S4}")).Code);
    }

    [Fact]
    public async Task TeacherCannotTouchAnotherTeachersLesson()
    {
        var w = await World.CreateAsync(f);
        var lesson = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{lesson}/attendance", Marks.Of((w.S1, "present")))).Code);

        Assert.Equal(403, (await w.Teacher2.Get($"/api/lessons/{lesson}/attendance")).Code);
        Assert.Equal(403, (await w.Teacher2.Put($"/api/lessons/{lesson}/attendance", Marks.Of((w.S1, "absent")))).Code);
        Assert.Equal(403, (await w.Teacher2.Get($"/api/lessons/{lesson}/previous-attendance")).Code);
        Assert.Equal(403, (await w.Teacher2.Put($"/api/lessons/{lesson}", new { date = Dates.Today, pairNumber = 5, subjectId = w.Sub1 })).Code);
        Assert.Equal(403, (await w.Teacher2.Delete($"/api/lessons/{lesson}")).Code);
        Assert.Equal(0, (await w.Teacher2.Get("/api/lessons")).Count);
        // Администратор может исправить отметку.
        Assert.Equal(204, (await w.Admin.Put($"/api/lessons/{lesson}/attendance", Marks.Of((w.S1, "excused")))).Code);
    }

    [Fact]
    public async Task TeacherCannotCreateLessonWithoutAssignment()
    {
        var w = await World.CreateAsync(f);
        // Преподаватель 2: предмет П2 только в Г1; предмет П1 или группа Г2 — без назначения.
        Assert.Equal(403, (await w.NewLesson(w.Teacher2, Dates.Today, 1, w.Sub2, w.G1, w.G2)).Code);
        Assert.Equal(403, (await w.NewLesson(w.Teacher2, Dates.Today, 1, w.Sub1, w.G1)).Code);
        Assert.Equal(201, (await w.NewLesson(w.Teacher2, Dates.Today, 1, w.Sub2, w.G1)).Code);
    }

    [Fact]
    public async Task StudentSeesOnlyOwnAttendance()
    {
        var w = await World.CreateAsync(f);
        Assert.Equal(403, (await w.Student.Get($"/api/reports/student/{w.S2}")).Code);
        Assert.Equal(403, (await w.Student.Get($"/api/reports/group/{w.G1}")).Code);
        var own = await w.Student.Get("/api/me/attendance");
        Assert.Equal(w.S1, own["student"].GetProperty("id").GetInt32());
    }
}

public class UnitTests
{
    [Theory]
    [InlineData("admin", true)]
    [InlineData("a.b-c_9", true)]
    [InlineData("abc", true)]
    [InlineData("ab", false)]
    [InlineData("Admin", false)]
    [InlineData("user name", false)]
    [InlineData("пользователь", false)]
    [InlineData("a@b.c", false)]
    public void LoginRulesAcceptOnlyLowercaseLatinDigitsAndSeparators(string login, bool valid) =>
        Assert.Equal(valid, LoginRules.IsValid(login));

    [Fact]
    public void LoginRulesLimitLengthTo50()
    {
        Assert.True(LoginRules.IsValid(new string('a', 50)));
        Assert.False(LoginRules.IsValid(new string('a', 51)));
    }

    [Fact]
    public void TemporaryPasswordsAreLongEnoughUnambiguousAndDifferent()
    {
        var a = TempPassword.Generate();
        var b = TempPassword.Generate();
        Assert.Equal(12, a.Length);
        Assert.NotEqual(a, b);
        Assert.DoesNotContain(a, c => "0O1lI".Contains(c));
    }
}
