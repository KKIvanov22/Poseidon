using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Poseidon.Server.Data;

namespace Poseidon.Server.Services;

public interface IRegistrationOrchestrator
{
    Task<(Guid RegistrationId, int StatusId)> RegisterStudentTransactionAsync(Guid eventId, Guid studentId);
    Task<CancelRegistrationTransactionResult> CancelRegistrationTransactionAsync(Guid eventId, Guid studentId);
}

public sealed class RegistrationOrchestrator(AppDbContext dbContext) : IRegistrationOrchestrator
{
    public async Task<(Guid RegistrationId, int StatusId)> RegisterStudentTransactionAsync(Guid eventId, Guid studentId)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT event_id, capacity FROM public.events WHERE event_id = {0} FOR UPDATE;",
                eventId);

            const string insertSql = """
                INSERT INTO public.registrations (event_id, student_id, registration_status_id)
                VALUES (
                    @eventId,
                    @studentId,
                    CASE
                        WHEN (
                            SELECT COUNT(*)
                            FROM public.registrations
                            WHERE event_id = @eventId
                              AND registration_status_id = 1
                              AND cancelled_at IS NULL
                        ) < (
                            SELECT capacity
                            FROM public.events
                            WHERE event_id = @eventId
                        ) THEN 1
                        ELSE 2
                    END
                )
                RETURNING registration_id, registration_status_id;
                """;

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = insertSql;
            command.Transaction = transaction.GetDbTransaction();
            command.Parameters.Add(new NpgsqlParameter("eventId", eventId));
            command.Parameters.Add(new NpgsqlParameter("studentId", studentId));

            await OpenConnectionIfNeededAsync();

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Failed to register student transaction output missing.");
            }

            Guid registrationId = reader.GetGuid(0);
            int statusId = reader.GetInt32(1);
            await reader.CloseAsync();

            const string outboxSql = """
                INSERT INTO public.notification_jobs (event_id, recipient_user_id, job_status_id, payload, title, message)
                VALUES ({0}, {1}, 1, CAST({2} AS jsonb), {3}, {4});
                """;

            string payloadType = statusId == 1 ? "RegistrationConfirmed" : "RegistrationWaitlisted";
            string payloadJson = $$"""{"type":"{{payloadType}}","event_id":"{{eventId}}","student_id":"{{studentId}}","registration_id":"{{registrationId}}","assigned_status_id":{{statusId}}}""";
            string jobTitle = statusId == 1 ? "Registration Confirmed!" : "Placed on Waitlist";
            string jobMessage = statusId == 1
                ? "Great news! Your seat for the event has been confirmed."
                : "The event is currently full. You have been placed securely in the queue line.";

            await dbContext.Database.ExecuteSqlRawAsync(outboxSql, eventId, studentId, payloadJson, jobTitle, jobMessage);
            await transaction.CommitAsync();

            return (registrationId, statusId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CancelRegistrationTransactionResult> CancelRegistrationTransactionAsync(Guid eventId, Guid studentId)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            const string cancelSql = """
                WITH target AS (
                    SELECT registration_id, registration_status_id
                    FROM public.registrations
                    WHERE event_id = @eventId
                      AND student_id = @studentId
                      AND registration_status_id IN (1, 2)
                      AND cancelled_at IS NULL
                    FOR UPDATE
                ),
                updated AS (
                    UPDATE public.registrations registration
                    SET registration_status_id = 3,
                        cancelled_at = CURRENT_TIMESTAMP
                    FROM target
                    WHERE registration.registration_id = target.registration_id
                    RETURNING registration.registration_id, target.registration_status_id
                )
                SELECT registration_id, registration_status_id
                FROM updated;
                """;

            await using var cancelCommand = dbContext.Database.GetDbConnection().CreateCommand();
            cancelCommand.CommandText = cancelSql;
            cancelCommand.Transaction = transaction.GetDbTransaction();
            cancelCommand.Parameters.Add(new NpgsqlParameter("eventId", eventId));
            cancelCommand.Parameters.Add(new NpgsqlParameter("studentId", studentId));

            await OpenConnectionIfNeededAsync();

            Guid cancelledRegistrationId;
            int previousStatusId;
            await using (var cancelReader = await cancelCommand.ExecuteReaderAsync())
            {
                if (!await cancelReader.ReadAsync())
                {
                    await transaction.RollbackAsync();
                    return CancelRegistrationTransactionResult.NotFound();
                }

                cancelledRegistrationId = cancelReader.GetGuid(0);
                previousStatusId = cancelReader.GetInt32(1);
            }

            const string cancellationOutboxSql = """
                INSERT INTO public.notification_jobs (event_id, recipient_user_id, job_status_id, payload, title, message)
                VALUES ({0}, {1}, 1, CAST({2} AS jsonb), 'Registration Cancelled', 'Your event registration has been cancelled.');
                """;

            string cancellationPayloadJson = $$"""{"type":"RegistrationCancelled","event_id":"{{eventId}}","student_id":"{{studentId}}","registration_id":"{{cancelledRegistrationId}}","previous_status_id":{{previousStatusId}}}""";
            await dbContext.Database.ExecuteSqlRawAsync(cancellationOutboxSql, eventId, studentId, cancellationPayloadJson);

            if (previousStatusId != 1)
            {
                await transaction.CommitAsync();
                return CancelRegistrationTransactionResult.Cancelled(cancelledRegistrationId, previousStatusId);
            }

            const string promoteSql = """
                WITH next_waitlist AS (
                    SELECT registration_id, student_id
                    FROM public.registrations
                    WHERE event_id = @eventId
                      AND registration_status_id = 2
                      AND cancelled_at IS NULL
                    ORDER BY registered_at ASC
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE public.registrations
                SET registration_status_id = 1
                FROM next_waitlist
                WHERE public.registrations.registration_id = next_waitlist.registration_id
                RETURNING public.registrations.registration_id, next_waitlist.student_id;
                """;

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = promoteSql;
            command.Transaction = transaction.GetDbTransaction();
            command.Parameters.Add(new NpgsqlParameter("eventId", eventId));

            await OpenConnectionIfNeededAsync();

            Guid? promotedStudentId = null;
            Guid? promotedRegistrationId = null;
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                promotedRegistrationId = reader.GetGuid(0);
                promotedStudentId = reader.GetGuid(1);
            }

            await reader.CloseAsync();

            if (promotedStudentId.HasValue)
            {
                const string promotionOutboxSql = """
                    INSERT INTO public.notification_jobs (event_id, recipient_user_id, job_status_id, payload, title, message)
                    VALUES ({0}, {1}, 1, CAST({2} AS jsonb), 'Waitlist Promotion Confirmed!', 'Good news! You have been automatically promoted.');
                    """;

                string promotionPayloadJson = $$"""{"type":"WaitlistPromoted","event_id":"{{eventId}}","student_id":"{{promotedStudentId.Value}}"}""";
                await dbContext.Database.ExecuteSqlRawAsync(promotionOutboxSql, eventId, promotedStudentId.Value, promotionPayloadJson);
            }

            await transaction.CommitAsync();
            return CancelRegistrationTransactionResult.Cancelled(
                cancelledRegistrationId,
                previousStatusId,
                promotedRegistrationId,
                promotedStudentId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task OpenConnectionIfNeededAsync()
    {
        if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
        {
            await dbContext.Database.GetDbConnection().OpenAsync();
        }
    }
}

public sealed record CancelRegistrationTransactionResult(
    bool WasCancelled,
    Guid? CancelledRegistrationId = null,
    int? PreviousStatusId = null,
    Guid? PromotedRegistrationId = null,
    Guid? PromotedStudentId = null)
{
    public static CancelRegistrationTransactionResult NotFound() => new(false);

    public static CancelRegistrationTransactionResult Cancelled(
        Guid cancelledRegistrationId,
        int previousStatusId,
        Guid? promotedRegistrationId = null,
        Guid? promotedStudentId = null) =>
        new(true, cancelledRegistrationId, previousStatusId, promotedRegistrationId, promotedStudentId);
}
