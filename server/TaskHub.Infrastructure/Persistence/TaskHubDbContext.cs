using Microsoft.EntityFrameworkCore;
using TaskHub.Core;

namespace TaskHub.Infrastructure;

public class TaskHubDbContext : DbContext
{
    public TaskHubDbContext(DbContextOptions<TaskHubDbContext> options) : base(options)
    {
    }

    // Each DbSet becomes a table
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<JobRun> Runs => Set<JobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as readable text ("Queued") instead of numbers (0)
        modelBuilder.Entity<TaskItem>().Property(t => t.Type).HasConversion<string>();
        modelBuilder.Entity<JobRun>().Property(r => r.Status).HasConversion<string>();

        // No two users can share an email. The database itself enforces this,
        // even if two sign-ups arrive at the same instant.
        modelBuilder.Entity<User>().Property(u => u.Email).HasMaxLength(254);
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // One user has many tasks. Deleting a user deletes their tasks too.
        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tasks)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The scheduler asks "which active tasks are due?" every second.
        // An index makes that question fast, like the index at the back of a book.
        modelBuilder.Entity<TaskItem>().HasIndex(t => new { t.IsActive, t.NextRunAtUtc });
    }
}