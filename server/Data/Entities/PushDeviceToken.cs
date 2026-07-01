using System.ComponentModel.DataAnnotations;

namespace Poseidon.Server.Data.Entities;

public sealed class PushDeviceToken
{
    public Guid PushDeviceTokenId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Platform { get; set; } = "Android";

    [MaxLength(100)]
    public string? DeviceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    public User? User { get; set; }
}
