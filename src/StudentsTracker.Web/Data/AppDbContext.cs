
using Microsoft.EntityFrameworkCore;
using StudentsTracker.Web.Models;

namespace StudentsTracker.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeachingAssignment> TeachingAssignments => Set<TeachingAssignment>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Attendance> Attendance => Set<Attendance>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users", t => t.HasCheckConstraint("ck_users_role", "role in ('admin','teacher','student')"));
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Login).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasMaxLength(16).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        });

        b.Entity<Group>(e =>
        {
            e.ToTable("groups", t => t.HasCheckConstraint("ck_groups_start_year", "start_year between 2000 and 2100"));
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Subject>(e =>
        {
            e.ToTable("subjects");
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Student>(e =>
        {
            e.ToTable("students");
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.GroupId);
            e.HasIndex(x => x.UserId).IsUnique();
        });

        b.Entity<TeachingAssignment>(e =>
        {
            e.ToTable("teaching_assignments");
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TeacherId, x.GroupId, x.SubjectId }).IsUnique();
            e.HasIndex(x => x.GroupId);
            e.HasIndex(x => x.SubjectId);
        });

        b.Entity<Lesson>(e =>
        {
            e.ToTable("lessons", t => t.HasCheckConstraint("ck_lessons_pair_number", "pair_number between 1 and 8"));
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.LessonDate).HasColumnType("date");
            e.Property(x => x.Topic).HasMaxLength(500);
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.GroupId, x.LessonDate, x.PairNumber }).IsUnique();
            e.HasIndex(x => x.SubjectId);
            e.HasIndex(x => x.TeacherId);
        });

        b.Entity<Attendance>(e =>
        {
            e.ToTable("attendance", t => t.HasCheckConstraint("ck_attendance_status", "status in ('present','absent','excused','late')"));
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Status).HasMaxLength(16).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.LessonId, x.StudentId }).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.LessonId });
        });

        // Таблицы уже названы в snake_case; приводим к тому же виду колонки, ключи и индексы.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var p in entity.GetProperties())
                p.SetColumnName(ToSnake(p.Name));
            foreach (var k in entity.GetKeys())
                k.SetName(ToSnake(k.GetName() ?? ""));
            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnake(fk.GetConstraintName() ?? ""));
            foreach (var ix in entity.GetIndexes())
                ix.SetDatabaseName(ToSnake(ix.GetDatabaseName() ?? ""));
        }
    }

    private static string ToSnake(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
