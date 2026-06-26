namespace Poseidon.Server.Models;

// BE-22: Enqueue payload when an event seat is secured
public sealed record RegistrationConfirmedMessage(
    Guid RegistrationId, Guid UserId, Guid EventId, string UserEmail, string EventTitle, DateTimeOffset Timestamp);

// BE-23: Enqueue payload when an event is full and user lands on the waitlist
public sealed record RegistrationWaitlistedMessage(
    Guid RegistrationId, Guid UserId, Guid EventId, string UserEmail, string EventTitle, int WaitlistPosition, DateTimeOffset Timestamp);

// BE-24: Enqueue payload when a spot opens up and a waitlisted user is bumped to active status
public sealed record WaitlistPromotedMessage(
    Guid RegistrationId, Guid UserId, Guid EventId, string UserEmail, string EventTitle, DateTimeOffset Timestamp);