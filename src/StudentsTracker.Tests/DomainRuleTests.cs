namespace StudentsTracker.Tests;

[Collection("api")]
public class DomainRuleTests(AppFactory f)
{
    [Fact]
    public async Task ReferencedRecordsCannotBeDeleted()
    {
        var w = await World.CreateAsync(f);
        Assert.Equal(409, (await w.Admin.Delete($"/api/groups/{w.G1}")).Code);     // есть студенты
        Assert.Equal(409, (await w.Admin.Delete($"/api/subjects/{w.Sub1}")).Code); // есть назначения
        Assert.Equal(409, (await w.Admin.Delete($"/api/users/{w.T1}")).Code);      // есть назначения
        Assert.Equal(409, (await w.Admin.Delete($"/api/users/{w.StudentUserId}")).Code); // учётка студента — только вместе со студентом
    }

    [Fact]
    public async Task AssignmentWithLessonsCannotBeDeletedAndTeacherRoleCannotChange()
    {
        var w = await World.CreateAsync(f);
        await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        Assert.Equal(409, (await w.Admin.Delete($"/api/assignments/{w.Assign1}")).Code);
        Assert.Equal(409, (await w.Admin.Put($"/api/users/{w.T1}", new { fullName = "Teacher", role = "admin" })).Code);
        Assert.Equal(409, (await w.Admin.Delete($"/api/users/{w.T1}")).Code);
    }

    [Fact]
    public async Task StudentWithMarksCannotBeMovedOrDeletedButOneWithoutMarksCan()
    {
        var w = await World.CreateAsync(f);
        var lesson = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        await w.Teacher1.Put($"/api/lessons/{lesson}/attendance", Marks.Of((w.S2, "present")));

        Assert.Equal(409, (await w.Admin.Put($"/api/students/{w.S2}", new { fullName = "Beta Two", groupId = w.G2 })).Code);
        Assert.Equal(409, (await w.Admin.Delete($"/api/students/{w.S2}")).Code);
        // имя можно менять и при наличии отметок
        Assert.Equal(204, (await w.Admin.Put($"/api/students/{w.S2}", new { fullName = "Beta Renamed", groupId = w.G1 })).Code);

        // S1 без отметок: удаляется вместе с учётной записью
        Assert.Equal(204, (await w.Admin.Delete($"/api/students/{w.S1}")).Code);
        Assert.Equal(404, (await w.Admin.Get($"/api/users/{w.StudentUserId}")).Code);
        Assert.Equal(401, (await w.Student.Get("/api/me/attendance")).Code); // учётка исчезла, сессия недействительна
    }

    [Fact]
    public async Task DuplicatesAreRejected()
    {
        var w = await World.CreateAsync(f);
        var groupName = (await w.Admin.Get($"/api/groups/{w.G1}"))["name"].GetString()!;
        var subjectName = (await w.Admin.Get($"/api/subjects/{w.Sub1}"))["name"].GetString()!;
        Assert.Equal(409, (await w.Admin.Post("/api/groups", new { name = groupName, startYear = 2024 })).Code);
        Assert.Equal(409, (await w.Admin.Post("/api/subjects", new { name = subjectName })).Code);
        Assert.Equal(409, (await w.Admin.Post("/api/assignments", new { teacherId = w.T1, groupId = w.G1, subjectId = w.Sub1 })).Code);
        Assert.Equal(409, (await w.Admin.Post($"/api/students/{w.S1}/account", new { login = "another.login" })).Code); // уже есть учётка
        var login = (await w.Admin.Get($"/api/users/{w.T1}"))["login"].GetString()!;
        Assert.Equal(409, (await w.Admin.Post("/api/users", new { login, fullName = "Copy", role = "teacher" })).Code);
    }

    [Fact]
    public async Task InputIsValidated()
    {
        var w = await World.CreateAsync(f);
        Assert.Equal(400, (await w.Admin.Post("/api/groups", new { name = "   ", startYear = 2024 })).Code);
        Assert.Equal(400, (await w.Admin.Post("/api/groups", new { name = "X", startYear = 1999 })).Code);
        Assert.Equal(400, (await w.Admin.Post("/api/groups", new { name = "X", startYear = 2101 })).Code);
        Assert.Equal(400, (await w.Admin.Post("/api/users", new { login = "Bad Login", fullName = "X", role = "teacher" })).Code);
        Assert.Equal(400, (await w.Admin.Post("/api/users", new { login = "ok.login", fullName = "X", role = "student" })).Code);
        Assert.Equal(400, (await w.Admin.Post("/api/students", new { fullName = "X", groupId = 999999 })).Code);
    }

    [Fact]
    public async Task StudentSearchIgnoresCaseAndTreatsWildcardsAsText()
    {
        var w = await World.CreateAsync(f);
        var name = (await w.Admin.Get($"/api/students/{w.S1}"))["fullName"].GetString()!;
        Assert.True((await w.Admin.Get($"/api/students?q={Uri.EscapeDataString(name.ToUpperInvariant())}")).Count >= 1);
        Assert.Equal(0, (await w.Admin.Get("/api/students?q=%25")).Count); // '%' ищется как символ, а не «любой текст»
    }
}
