namespace ChatApp.Models;

public class Status
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string? Text { get; set; }
    public string? MediaUrl { get; set; }
    public string MediaType { get; set; } = "none";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public ICollection<StatusView> Views { get; set; } = new List<StatusView>();
}

public class StatusView
{
    public int Id { get; set; }
    public int StatusId { get; set; }
    public Status Status { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
