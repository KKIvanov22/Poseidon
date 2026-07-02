namespace Poseidon.Server.Data.Entities;

public sealed class EventStatus
{
    public int EventStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
