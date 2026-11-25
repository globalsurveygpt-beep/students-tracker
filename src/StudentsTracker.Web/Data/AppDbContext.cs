using Microsoft.EntityFrameworkCore;

namespace StudentsTracker.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Сущности добавляются на этапе 1 (см. docs/SPEC.md, раздел 4).
}
