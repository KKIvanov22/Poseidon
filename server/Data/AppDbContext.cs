using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace Poseidon.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public async Task<(Guid RegistrationId, int StatusId)> RegisterStudentTransactionAsync(Guid eventId, Guid studentId)
    {
        using var transaction = await Database.BeginTransactionAsync();
        try
        {
            await Database.ExecuteSqlRawAsync(
                "SELECT event_id, capacity FROM public.events WHERE event_id = {0} FOR UPDATE;", 
                eventId
            );

            string insertSql = @"
                INSERT INTO public.registrations (event_id, student_id, registration_status_id)
                VALUES (
                    {0}, 
                    {1},
                    CASE 
                        WHEN (
                            SELECT COUNT(*) 
                            FROM public.registrations 
                            WHERE event_id = {0} 
                              AND registration_status_id = 1 
                              AND cancelled_at IS NULL
                        ) < (
                            SELECT capacity 
                            FROM public.events 
                            WHERE event_id = {0}
                        ) THEN 1
                        ELSE 2
                    END
                )
                RETURNING registration_id, registration_status_id;";

            using var command = Database.GetDbConnection().CreateCommand();
            command.CommandText = insertSql;
            command.Transaction = transaction.GetDbTransaction();
            
            command.Parameters.Add(new NpgsqlParameter { Value = eventId });
            command.Parameters.Add(new NpgsqlParameter { Value = studentId });

            if (Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                await Database.GetDbConnection().OpenAsync();

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Guid regId = reader.GetGuid(0);
                int statusId = reader.GetInt32(1);

                reader.Close();
                
                string outboxSql = @"
                    INSERT INTO public.notification_jobs (event_id, recipient_user_id, job_status_id, payload, title, message)
                    VALUES ({0}, {1}, 1, CAST({2} AS jsonb), {3}, {4});";

                string payloadJson = $"{{\"type\": \"RegistrationProcessed\", \"event_id\": \"{eventId}\", \"student_id\": \"{studentId}\", \"assigned_status_id\": {statusId}}}";
                string jobTitle = statusId == 1 ? "Registration Confirmed!" : "Placed on Waitlist";
                string jobMessage = statusId == 1 
                    ? "Great news! Your seat for the event has been confirmed." 
                    : "The event is currently full. You have been placed securely in the queue line.";

                await Database.ExecuteSqlRawAsync(outboxSql, eventId, studentId, payloadJson, jobTitle, jobMessage);
                await transaction.CommitAsync();
                
                return (regId, statusId);
            }

            throw new InvalidOperationException("Failed to register student transaction output missing.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Guid?> CancelRegistrationTransactionAsync(Guid eventId, Guid studentId)
    {
        using var transaction = await Database.BeginTransactionAsync();
        try
        {
            string cancelSql = @"
                UPDATE public.registrations
                SET registration_status_id = 3,
                    cancelled_at = CURRENT_TIMESTAMP
                WHERE event_id = {0} 
                  AND student_id = {1} 
                  AND registration_status_id = 1;";

            int affectedRows = await Database.ExecuteSqlRawAsync(cancelSql, eventId, studentId);
            if (affectedRows == 0)
            {
                await transaction.RollbackAsync();
                return null;
            }

            string promoteSql = @"
                WITH next_waitlist AS (
                    SELECT registration_id, student_id 
                    FROM public.registrations
                    WHERE event_id = {0} 
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
                RETURNING public.registrations.registration_id, public.registrations.student_id;";

            using var command = Database.GetDbConnection().CreateCommand();
            command.CommandText = promoteSql;
            command.Transaction = transaction.GetDbTransaction();
            command.Parameters.Add(new NpgsqlParameter { Value = eventId });

            if (Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                await Database.GetDbConnection().OpenAsync();

            Guid? promotedStudentId = null;
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                promotedStudentId = reader.GetGuid(1);
            }
            reader.Close();

            if (promotedStudentId.HasValue)
            {
                string promotionOutboxSql = @"
                    INSERT INTO public.notification_jobs (event_id, recipient_user_id, job_status_id, payload, title, message)
                    VALUES ({0}, {1}, 1, CAST({2} AS jsonb), 'Waitlist Promotion Confirmed!', 'Good news! You have been automatically promoted.');";

                string promotionPayloadJson = $"{{\"type\": \"WaitlistPromoted\", \"event_id\": \"{eventId}\", \"student_id\": \"{promotedStudentId.Value}\"}}";
                await Database.ExecuteSqlRawAsync(promotionOutboxSql, eventId, promotedStudentId.Value, promotionPayloadJson);
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
}