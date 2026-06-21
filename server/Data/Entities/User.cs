namespace Poseidon.Server.Data.Entities;

public sealed class User
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public UserRole? Role { get; set; }
}
