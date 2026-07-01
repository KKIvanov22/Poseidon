namespace Poseidon.Server.Data.Entities;

public sealed class NotificationJobStatus
{
    public int JobStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public ICollection<NotificationJob> NotificationJobs { get; set; } = new List<NotificationJob>();
}
