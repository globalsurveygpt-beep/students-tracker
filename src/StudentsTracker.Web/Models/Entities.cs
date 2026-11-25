namespace StudentsTracker.Web.Models;

public static class Roles
{
    public const string Admin = "admin";
    public const string Teacher = "teacher";
    public const string Student = "student";
}

public static class AttendanceStatus
{
    public const string Present = "present";
    public const string Absent = "absent";
    public const string Excused = "excused";
    public const string Late = "late";
}

public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Student;
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public short StartYear { get; set; }
}

public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
}

public class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class TeachingAssignment
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}

public class Lesson
{
    public int Id { get; set; }
    public DateOnly LessonDate { get; set; }
    public short PairNumber { get; set; }
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public string? Topic { get; set; }
}

public class Attendance
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public string Status { get; set; } = AttendanceStatus.Present;
    public string? Note { get; set; }
}
