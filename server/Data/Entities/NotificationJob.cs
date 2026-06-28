using System.ComponentModel.DataAnnotations;

namespace Poseidon.Server.Data.Entities;

public class NotificationJob
{
    [Key]
    public Guid NotificationJobId { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Guid RecipientUserId { get; set; }
    public int JobStatusId { get; set; }
    public string Payload { get; set; } = "{}";

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Channel { get; set; } = "Email";

    public int Attempts { get; set; }
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublisherLockedUntil { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }

    public Event? Event { get; set; }
    public User? RecipientUser { get; set; }
}
