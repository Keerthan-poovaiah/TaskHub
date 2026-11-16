using Microsoft.EntityFrameworkCore;
using TaskHub.Core;

namespace TaskHub.Infrastructure;

public class TaskHubDbContext : DbContext
{
    public TaskHubDbContext(DbContextOptions<TaskHubDbContext> options) : base(options)
    {
    }

    // Each DbSet becomes a table
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<JobRun> Runs => Set<JobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as readable text ("Queued") instead of numbers (0)
        modelBuilder.Entity<TaskItem>().Property(t => t.Type).HasConversion<string>();
        modelBuilder.Entity<JobRun>().Property(r => r.Status).HasConversion<string>();

        // The scheduler asks "which active tasks are due?" every second.
        // An index makes that question fast, like the index at the back of a book.
        modelBuilder.Entity<TaskItem>().HasIndex(t => new { t.IsActive, t.NextRunAtUtc });
    }
}