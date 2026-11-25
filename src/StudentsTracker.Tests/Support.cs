using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace StudentsTracker.Tests;

/// <summary>
/// Поднимает приложение на отдельной базе students_tracker_test (на том же сервере PostgreSQL, строка подключения
/// берётся из user-secrets основного проекта). База создаётся миграциями и удаляется после прогона.
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    public const string AdminLogin = "test.admin";
    public const string AdminPassword = "TestAdmin#2026";
    private const string TestDatabase = "students_tracker_test";
    private string? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var original = cfg.Build()["ConnectionStrings:Default"]
                ?? throw new InvalidOperationException("Не задана ConnectionStrings:Default (user-secrets основного проекта).");
            _connection = new NpgsqlConnectionStringBuilder(original) { Database = TestDatabase }.ConnectionString;
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connection,
                ["Admin:Login"] = AdminLogin,
                ["Admin:Password"] = AdminPassword,
            });
        });
    }

    public Api CreateApi(bool csrfHeader = true)
    {
        var http = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        if (csrfHeader) http.DefaultRequestHeaders.Add("X-Requested-With", "tests");
        return new Api(http);
    }

    public async Task<Api> LoginAsync(string login, string password)
    {
        var api = CreateApi();
        var r = await api.Post("/api/auth/login", new { login, password });
        Assert.True(r.Code == 200, $"Вход {login} не удался: {r.Code} {r.Raw}");
        return api;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _connection is null) return;
        NpgsqlConnection.ClearAllPools();
        var admin = new NpgsqlConnectionStringBuilder(_connection) { Database = "postgres" }.ConnectionString;
        using var conn = new NpgsqlConnection(admin);
        conn.Open();
        using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {TestDatabase} WITH (FORCE)", conn);
        cmd.ExecuteNonQuery();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<AppFactory> { }

public sealed record Result(HttpStatusCode Status, JsonElement Json, string Raw)
{
    public int Code => (int)Status;
    public int Id => Json.GetProperty("id").GetInt32();
    public JsonElement this[string name] => Json.GetProperty(name);
    public string Title => Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
    public int Count => Json.GetArrayLength();
}

public sealed class Api(HttpClient http)
{
    public async Task<Result> Send(HttpMethod method, string url, object? body = null)
    {
        using var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        using var resp = await http.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        JsonElement json = default;
        if (raw.Length > 0)
        {
            try { json = JsonDocument.Parse(raw).RootElement.Clone(); } catch (JsonException) { }
        }
        return new Result(resp.StatusCode, json, raw);
    }

    public Task<Result> Get(string url) => Send(HttpMethod.Get, url);
    public Task<Result> Post(string url, object? body = null) => Send(HttpMethod.Post, url, body);
    public Task<Result> Put(string url, object? body = null) => Send(HttpMethod.Put, url, body);
    public Task<Result> Delete(string url) => Send(HttpMethod.Delete, url);
}

public static class Dates
{
    public static string Today => Format(0);
    public static string Format(int daysAgo) => DateOnly.FromDateTime(DateTime.Now).AddDays(-daysAgo).ToString("yyyy-MM-dd");
}

public static class Marks
{
    public static object Of(params (int student, string status)[] items) =>
        new { items = items.Select(i => new { studentId = i.student, status = i.status }).ToArray() };
}

/// <summary>
/// Набор данных на один тест: две группы, два предмета, два преподавателя и студент с учётной записью.
/// Преподаватель 1 назначен на (Г1, П1) и (Г2, П1), преподаватель 2 — на (Г1, П2). В Г1 три студента, в Г2 один.
/// </summary>
public sealed class World
{
    public required Api Admin, Teacher1, Teacher2, Student;
    public required int G1, G2, Sub1, Sub2, T1, T2, S1, S2, S3, S4, Assign1, StudentUserId;

    public static async Task<World> CreateAsync(AppFactory f)
    {
        var admin = await f.LoginAsync(AppFactory.AdminLogin, AppFactory.AdminPassword);
        var s = Guid.NewGuid().ToString("N")[..8];

        async Task<int> Create(string url, object body) { var r = await admin.Post(url, body); Assert.Equal(201, r.Code); return r.Id; }
        var g1 = await Create("/api/groups", new { name = "G1-" + s, startYear = 2024 });
        var g2 = await Create("/api/groups", new { name = "G2-" + s, startYear = 2024 });
        var sub1 = await Create("/api/subjects", new { name = "Math-" + s });
        var sub2 = await Create("/api/subjects", new { name = "Art-" + s });
        var s1 = await Create("/api/students", new { fullName = "Alpha One " + s, groupId = g1 });
        var s2 = await Create("/api/students", new { fullName = "Beta Two " + s, groupId = g1 });
        var s3 = await Create("/api/students", new { fullName = "Gamma Three " + s, groupId = g1 });
        var s4 = await Create("/api/students", new { fullName = "Delta Four " + s, groupId = g2 });

        async Task<(int id, string password)> NewTeacher(string login)
        {
            var r = await admin.Post("/api/users", new { login, fullName = "Teacher " + login, role = "teacher" });
            Assert.Equal(201, r.Code);
            return (r["user"].GetProperty("id").GetInt32(), r["temporaryPassword"].GetString()!);
        }
        var t1 = await NewTeacher("t1-" + s);
        var t2 = await NewTeacher("t2-" + s);
        var assign1 = await Create("/api/assignments", new { teacherId = t1.id, groupId = g1, subjectId = sub1 });
        await Create("/api/assignments", new { teacherId = t1.id, groupId = g2, subjectId = sub1 });
        await Create("/api/assignments", new { teacherId = t2.id, groupId = g1, subjectId = sub2 });

        var account = await admin.Post($"/api/students/{s1}/account", new { login = "st-" + s });
        Assert.Equal(200, account.Code);
        var studentUser = (await admin.Get($"/api/students/{s1}"))["userId"].GetInt32();

        return new World
        {
            Admin = admin,
            Teacher1 = await f.LoginAsync("t1-" + s, t1.password),
            Teacher2 = await f.LoginAsync("t2-" + s, t2.password),
            Student = await f.LoginAsync("st-" + s, account["temporaryPassword"].GetString()!),
            G1 = g1, G2 = g2, Sub1 = sub1, Sub2 = sub2, T1 = t1.id, T2 = t2.id,
            S1 = s1, S2 = s2, S3 = s3, S4 = s4, Assign1 = assign1, StudentUserId = studentUser,
        };
    }

    public async Task<Result> NewLesson(Api who, string date, int pair, int subject, params int[] groups) =>
        await who.Post("/api/lessons", new { date, pairNumber = pair, subjectId = subject, groupIds = groups });

    public async Task<int> LessonId(Api who, string date, int pair, int subject, int group)
    {
        var r = await NewLesson(who, date, pair, subject, group);
        Assert.Equal(201, r.Code);
        return r.Json[0].GetProperty("id").GetInt32();
    }
}
