namespace Poseidon.Server.Data.Entities;

public sealed class Registration
{
    public Guid RegistrationId { get; set; }
    public Guid EventId { get; set; }
    public Guid StudentId { get; set; }
    public int RegistrationStatusId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public Event? Event { get; set; }
    public User? Student { get; set; }
}
