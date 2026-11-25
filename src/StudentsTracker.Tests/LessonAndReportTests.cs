namespace StudentsTracker.Tests;

[Collection("api")]
public class LessonTests(AppFactory f)
{
    [Fact]
    public async Task LessonValidation()
    {
        var w = await World.CreateAsync(f);
        Assert.Equal(400, (await w.NewLesson(w.Teacher1, Dates.Format(-1), 1, w.Sub1, w.G1)).Code); // будущая дата
        Assert.Equal(400, (await w.NewLesson(w.Teacher1, Dates.Today, 9, w.Sub1, w.G1)).Code);     // час 9
        Assert.Equal(400, (await w.NewLesson(w.Teacher1, Dates.Today, 0, w.Sub1, w.G1)).Code);     // час 0
        Assert.Equal(400, (await w.NewLesson(w.Teacher1, Dates.Today, 1, w.Sub1)).Code);           // нет групп
        Assert.Equal(400, (await w.NewLesson(w.Teacher1, Dates.Today, 1, 999999, w.G1)).Code);     // нет предмета
        Assert.Equal(0, (await w.Teacher1.Get("/api/lessons")).Count);
    }

    [Fact]
    public async Task OneLessonForSeveralGroupsCreatesOneRecordPerGroup()
    {
        var w = await World.CreateAsync(f);
        var r = await w.NewLesson(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1, w.G2);
        Assert.Equal(201, r.Code);
        Assert.Equal(2, r.Count);
        Assert.Equal(2, (await w.Teacher1.Get("/api/lessons")).Count);
    }

    [Fact]
    public async Task BusyHourOfAnyGroupRejectsTheWholeRequestAndCreatesNothing()
    {
        var w = await World.CreateAsync(f);
        Assert.Equal(201, (await w.NewLesson(w.Teacher1, Dates.Today, 3, w.Sub1, w.G1)).Code);
        // Г1 занята в 3-й час: Г2 тоже не должна получить занятие.
        Assert.Equal(409, (await w.NewLesson(w.Teacher1, Dates.Today, 3, w.Sub1, w.G2, w.G1)).Code);
        var forG2 = await w.Teacher1.Get($"/api/lessons?groupId={w.G2}");
        Assert.Equal(0, forG2.Count);
    }

    [Fact]
    public async Task LessonCanBeMovedButNotOntoABusyHourAndNotDeletedWithMarks()
    {
        var w = await World.CreateAsync(f);
        var a = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        await w.LessonId(w.Teacher1, Dates.Today, 2, w.Sub1, w.G1);

        Assert.Equal(409, (await w.Teacher1.Put($"/api/lessons/{a}", new { date = Dates.Today, pairNumber = 2, subjectId = w.Sub1 })).Code);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{a}", new { date = Dates.Today, pairNumber = 5, subjectId = w.Sub1, topic = "  Topic " })).Code);

        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{a}/attendance", Marks.Of((w.S1, "present")))).Code);
        Assert.Equal(409, (await w.Teacher1.Delete($"/api/lessons/{a}")).Code);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{a}/attendance", Marks.Of())).Code); // отметки сняты
        Assert.Equal(204, (await w.Teacher1.Delete($"/api/lessons/{a}")).Code);
    }
}

[Collection("api")]
public class AttendanceTests(AppFactory f)
{
    [Fact]
    public async Task UnmarkedStudentsComeBackWithoutStatus()
    {
        var w = await World.CreateAsync(f);
        var l = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        var r = await w.Teacher1.Get($"/api/lessons/{l}/attendance");
        Assert.Equal(3, r["students"].GetArrayLength());
        Assert.All(r["students"].EnumerateArray(), s => Assert.Equal(System.Text.Json.JsonValueKind.Null, s.GetProperty("status").ValueKind));
    }

    [Fact]
    public async Task SavingReplacesTheWholeList()
    {
        var w = await World.CreateAsync(f);
        var l = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{l}/attendance", Marks.Of((w.S1, "present"), (w.S2, "absent")))).Code);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{l}/attendance", Marks.Of((w.S2, "late")))).Code);

        var marked = (await w.Teacher1.Get($"/api/lessons/{l}/attendance"))["students"].EnumerateArray()
            .Where(s => s.GetProperty("status").ValueKind != System.Text.Json.JsonValueKind.Null)
            .ToDictionary(s => s.GetProperty("studentId").GetInt32(), s => s.GetProperty("status").GetString());
        Assert.Single(marked);
        Assert.Equal("late", marked[w.S2]);
    }

    [Fact]
    public async Task InvalidMarksAreRejectedAndNothingIsSaved()
    {
        var w = await World.CreateAsync(f);
        var l = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{l}/attendance", Marks.Of((w.S1, "present")))).Code);

        Assert.Equal(400, (await w.Teacher1.Put($"/api/lessons/{l}/attendance", Marks.Of((w.S4, "present")))).Code);          // студент другой группы
        Assert.Equal(400, (await w.Teacher1.Put($"/api/lessons/{l}/attendance", Marks.Of((w.S1, "sick")))).Code);             // неверный статус
        Assert.Equal(400, (await w.Teacher1.Put($"/api/lessons/{l}/attendance", Marks.Of((w.S1, "present"), (w.S1, "absent")))).Code); // дубль

        var lesson = (await w.Teacher1.Get($"/api/lessons/{l}/attendance"))["lesson"];
        Assert.Equal(1, lesson.GetProperty("markCount").GetInt32());
    }

    [Fact]
    public async Task PreviousAttendanceComesFromTheNearestEarlierHourOfTheSameDay()
    {
        var w = await World.CreateAsync(f);
        var first = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        var second = await w.LessonId(w.Teacher1, Dates.Today, 2, w.Sub1, w.G1);
        var third = await w.LessonId(w.Teacher1, Dates.Today, 3, w.Sub1, w.G1);

        Assert.Equal(404, (await w.Teacher1.Get($"/api/lessons/{first}/previous-attendance")).Code);  // раньше ничего нет
        Assert.Equal(404, (await w.Teacher1.Get($"/api/lessons/{second}/previous-attendance")).Code); // в 1-м часе нет отметок

        await w.Teacher1.Put($"/api/lessons/{first}/attendance", Marks.Of((w.S1, "present"), (w.S2, "absent")));
        var prev = await w.Teacher1.Get($"/api/lessons/{second}/previous-attendance");
        Assert.Equal(200, prev.Code);
        Assert.Equal(2, prev.Count);
        Assert.Equal(404, (await w.Teacher1.Get($"/api/lessons/{third}/previous-attendance")).Code); // ближайший (2-й час) без отметок
    }
}

[Collection("api")]
public class ReportTests(AppFactory f)
{
    /// <summary>
    /// П1 (преподаватель 1): ч1 — S1 был, S2 не был, S3 уваж.; ч2 — S1 опоздал, S2 не был; ч3 — S1 не был;
    /// 5 дней назад — S1 уваж., S2 был. П2 (преподаватель 2), 2 дня назад: S1 был.
    /// </summary>
    private async Task<World> Prepare()
    {
        var w = await World.CreateAsync(f);
        var l1 = await w.LessonId(w.Teacher1, Dates.Today, 1, w.Sub1, w.G1);
        var l2 = await w.LessonId(w.Teacher1, Dates.Today, 2, w.Sub1, w.G1);
        var l3 = await w.LessonId(w.Teacher1, Dates.Today, 3, w.Sub1, w.G1);
        var l4 = await w.LessonId(w.Teacher1, Dates.Format(5), 1, w.Sub1, w.G1);
        var l5 = await w.LessonId(w.Teacher2, Dates.Format(2), 1, w.Sub2, w.G1);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{l1}/attendance", Marks.Of((w.S1, "present"), (w.S2, "absent"), (w.S3, "excused")))).Code);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{l2}/attendance", Marks.Of((w.S1, "late"), (w.S2, "absent")))).Code);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{l3}/attendance", Marks.Of((w.S1, "absent")))).Code);
        Assert.Equal(204, (await w.Teacher1.Put($"/api/lessons/{l4}/attendance", Marks.Of((w.S1, "excused"), (w.S2, "present")))).Code);
        Assert.Equal(204, (await w.Teacher2.Put($"/api/lessons/{l5}/attendance", Marks.Of((w.S1, "present")))).Code);
        return w;
    }

    private static decimal? Percent(Result r, params string[] path)
    {
        var e = r.Json;
        foreach (var p in path) e = e.GetProperty(p);
        return e.ValueKind == System.Text.Json.JsonValueKind.Null ? null : e.GetDecimal();
    }

    [Fact]
    public async Task PercentFollowsTheFormulaAndExcusedAreLeftOut()
    {
        var w = await Prepare();
        var s1 = await w.Admin.Get($"/api/reports/student/{w.S1}");
        // был 2, опоздал 1, не был 1, уваж. 1: (2+1) / (5-1) = 75,0
        Assert.Equal(75.0m, Percent(s1, "total", "percent"));
        Assert.Equal(5, s1["total"].GetProperty("marked").GetInt32());

        var bySubject = s1["bySubject"].EnumerateArray().ToDictionary(x => x.GetProperty("subjectId").GetInt32(), x => x.GetProperty("summary").GetProperty("percent").GetDecimal());
        Assert.Equal(66.7m, bySubject[w.Sub1]);  // (1+1) / (4-1)
        Assert.Equal(100.0m, bySubject[w.Sub2]);

        var s2 = await w.Admin.Get($"/api/reports/student/{w.S2}");
        Assert.Equal(33.3m, Percent(s2, "total", "percent")); // 1 / 3
    }

    [Fact]
    public async Task PercentIsEmptyWhenOnlyExcusedMarksExist()
    {
        var w = await Prepare();
        var s3 = await w.Admin.Get($"/api/reports/student/{w.S3}");
        Assert.Null(Percent(s3, "total", "percent"));
        var s4 = await w.Admin.Get($"/api/reports/student/{w.S4}"); // совсем без отметок
        Assert.Null(Percent(s4, "total", "percent"));
        Assert.Equal(0, s4["total"].GetProperty("marked").GetInt32());
    }

    [Fact]
    public async Task GroupReportSumsAllStudents()
    {
        var w = await Prepare();
        var g = await w.Admin.Get($"/api/reports/group/{w.G1}");
        var t = g["total"];
        Assert.Equal(9, t.GetProperty("marked").GetInt32());
        Assert.Equal(57.1m, t.GetProperty("percent").GetDecimal()); // (3+1) / (9-2)
        Assert.Equal(3, g["students"].GetArrayLength());
    }

    [Fact]
    public async Task FiltersByDatesAndSubjectApply()
    {
        var w = await Prepare();
        var recent = await w.Admin.Get($"/api/reports/student/{w.S1}?from={Dates.Format(1)}");
        Assert.Equal(3, recent["total"].GetProperty("marked").GetInt32());
        var old = await w.Admin.Get($"/api/reports/student/{w.S1}?to={Dates.Format(3)}");
        Assert.Equal(1, old["total"].GetProperty("marked").GetInt32());
        var art = await w.Admin.Get($"/api/reports/student/{w.S1}?subjectId={w.Sub2}");
        Assert.Equal(1, art["total"].GetProperty("marked").GetInt32());
        Assert.Equal(400, (await w.Admin.Get($"/api/reports/student/{w.S1}?from={Dates.Today}&to={Dates.Format(5)}")).Code);
    }

    [Fact]
    public async Task TeacherReportIsLimitedToTheirOwnSubjects()
    {
        var w = await Prepare();
        var t1 = await w.Teacher1.Get($"/api/reports/student/{w.S1}");
        Assert.Equal(4, t1["total"].GetProperty("marked").GetInt32());
        Assert.Equal(66.7m, Percent(t1, "total", "percent"));

        var t2 = await w.Teacher2.Get($"/api/reports/student/{w.S1}");
        Assert.Equal(1, t2["total"].GetProperty("marked").GetInt32());
        Assert.Equal(0, (await w.Teacher2.Get($"/api/reports/student/{w.S1}?subjectId={w.Sub1}"))["total"].GetProperty("marked").GetInt32());

        Assert.Equal(403, (await w.Teacher2.Get($"/api/reports/group/{w.G2}")).Code); // чужая группа
    }

    [Fact]
    public async Task StudentCabinetShowsTheStudentsOwnNumbers()
    {
        var w = await Prepare();
        var me = await w.Student.Get("/api/me/attendance");
        Assert.Equal(200, me.Code);
        Assert.Equal(75.0m, Percent(me, "total", "percent"));
        Assert.Equal(5, me["records"].GetArrayLength());
    }
}
