namespace Poseidon.Server.Data.Entities;

public sealed class RegistrationStatus
{
    public int RegistrationStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
