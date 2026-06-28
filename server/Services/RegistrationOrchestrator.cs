using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Poseidon.Server.Data;

namespace Poseidon.Server.Services;

public interface IRegistrationOrchestrator
{
    Task<(Guid RegistrationId, int StatusId)> RegisterStudentTransactionAsync(Guid eventId, Guid studentId);
    Task<Guid?> CancelRegistrationTransactionAsync(Guid eventId, Guid studentId);
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

    public async Task<Guid?> CancelRegistrationTransactionAsync(Guid eventId, Guid studentId)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            const string cancelSql = """
                UPDATE public.registrations
                SET registration_status_id = 3,
                    cancelled_at = CURRENT_TIMESTAMP
                WHERE event_id = {0}
                  AND student_id = {1}
                  AND registration_status_id = 1;
                """;

            int affectedRows = await dbContext.Database.ExecuteSqlRawAsync(cancelSql, eventId, studentId);
            if (affectedRows == 0)
            {
                await transaction.RollbackAsync();
                return null;
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
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
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
            return promotedStudentId;
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
