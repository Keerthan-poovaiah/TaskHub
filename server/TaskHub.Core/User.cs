namespace TaskHub.Core;

// A person who has signed up
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";   // the scrambled version, never the password
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<TaskItem> Tasks { get; set; } = new();
}