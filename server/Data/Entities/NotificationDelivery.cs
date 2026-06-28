using System.ComponentModel.DataAnnotations;

namespace Poseidon.Server.Data.Entities;

public class NotificationDelivery
{
    [Key]
    public Guid NotificationDeliveryId { get; set; } = Guid.NewGuid();

    public Guid NotificationJobId { get; set; }
    public Guid RecipientUserId { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    [MaxLength(30)]
    public string Channel { get; set; } = "Email";

    [Required]
    [MaxLength(50)]
    public string Result { get; set; } = string.Empty;

    public NotificationJob? NotificationJob { get; set; }
    public User? RecipientUser { get; set; }
}
