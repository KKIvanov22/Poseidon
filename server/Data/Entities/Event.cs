using System;
using System.ComponentModel.DataAnnotations;

namespace Poseidon.Server.Data.Entities;

public sealed class Event
{
    public Guid EventId { get; set; }
    public Guid OrganizerId { get; set; }
    public int EventStatusId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Capacity { get; set; }
    public string? LocationText { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    // BE-18 Concurrency Token mapping
    [ConcurrencyCheck]
    public uint RowVersion { get; set; }

    public User? Organizer { get; set; }
}