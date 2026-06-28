using System.ComponentModel.DataAnnotations;

namespace Poseidon.Server.Data.Entities;

public class Notification
{
    [Key]
    public Guid NotificationId { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    public Guid EventId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string NotificationType { get; set; } = string.Empty; // "RegistrationConfirmed", "RegistrationWaitlisted", "WaitlistPromoted"
    
    [Required]
    [MaxLength(255)]
    public string RecipientEmail { get; set; } = string.Empty;
    
    [Required]
    public string MessageBody { get; set; } = string.Empty;
    
    public bool IsSent { get; set; } = false;
    public int RetryCount { get; set; } = 0;
    public string? FailureReason { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}