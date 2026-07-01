using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Event> Events => Set<Event>(); 
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<NotificationJob> NotificationJobs => Set<NotificationJob>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", "public");
            entity.HasKey(user => user.UserId);

            entity.Property(user => user.UserId)
                .HasColumnName("user_id")
                .HasDefaultValueSql("gen_random_uuid()");
                
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
            entity.Property(user => user.RoleId).HasColumnName("role_id");
            entity.Property(user => user.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(user => user.RoleId).HasDatabaseName("ix_users_role");

            entity.HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles", "public");
            entity.HasKey(role => role.RoleId);

            entity.Property(role => role.RoleId).HasColumnName("role_id");
            entity.Property(role => role.RoleName).HasColumnName("role_name").HasMaxLength(30).IsRequired();

            entity.HasIndex(role => role.RoleName).IsUnique();
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events", "public");
            entity.HasKey(e => e.EventId);

            entity.Property(e => e.EventId).HasColumnName("event_id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrganizerId).HasColumnName("organizer_id");
            entity.Property(e => e.EventStatusId).HasColumnName("event_status_id").HasDefaultValue(1); 
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
            entity.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
            entity.Property(e => e.Capacity).HasColumnName("capacity").IsRequired();
            entity.Property(e => e.LocationText).HasColumnName("location_text").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Organizer)
                .WithMany()
                .HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.ToTable("registrations", "public");
            entity.HasKey(registration => registration.RegistrationId);

            entity.Property(registration => registration.RegistrationId)
                .HasColumnName("registration_id")
                .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(registration => registration.EventId).HasColumnName("event_id");
            entity.Property(registration => registration.StudentId).HasColumnName("student_id");
            entity.Property(registration => registration.RegistrationStatusId).HasColumnName("registration_status_id");
            entity.Property(registration => registration.RegisteredAt)
                .HasColumnName("registered_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(registration => registration.CancelledAt).HasColumnName("cancelled_at");

            entity.HasIndex(registration => new { registration.EventId, registration.RegistrationStatusId, registration.RegisteredAt })
                .HasDatabaseName("ix_registrations_waitlist_order");

            entity.HasOne(registration => registration.Event)
                .WithMany()
                .HasForeignKey(registration => registration.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(registration => registration.Student)
                .WithMany()
                .HasForeignKey(registration => registration.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificationJob>(entity =>
        {
            entity.ToTable("notification_jobs", "public");
            entity.HasKey(job => job.NotificationJobId);

            entity.Property(job => job.NotificationJobId)
                .HasColumnName("notification_job_id")
                .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(job => job.EventId).HasColumnName("event_id");
            entity.Property(job => job.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(job => job.JobStatusId).HasColumnName("job_status_id");
            entity.Property(job => job.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(job => job.Title).HasColumnName("title").HasMaxLength(150).IsRequired();
            entity.Property(job => job.Message).HasColumnName("message").IsRequired();
            entity.Property(job => job.Channel).HasColumnName("channel").HasMaxLength(30).HasDefaultValue("Email").IsRequired();
            entity.Property(job => job.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            entity.Property(job => job.AvailableAt).HasColumnName("available_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(job => job.PublisherLockedUntil).HasColumnName("publisher_locked_until");
            entity.Property(job => job.PublishedAt).HasColumnName("published_at");
            entity.Property(job => job.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(job => job.ProcessedAt).HasColumnName("processed_at");
            entity.Property(job => job.LastError).HasColumnName("last_error");

            entity.HasIndex(job => new { job.EventId }).HasDatabaseName("ix_notification_jobs_event");
            entity.HasIndex(job => new { job.RecipientUserId }).HasDatabaseName("ix_notification_jobs_recipient");
            entity.HasIndex(job => new { job.JobStatusId, job.AvailableAt }).HasDatabaseName("ix_notification_jobs_status_available");

            entity.HasOne(job => job.Event)
                .WithMany()
                .HasForeignKey(job => job.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(job => job.RecipientUser)
                .WithMany()
                .HasForeignKey(job => job.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("notification_deliveries", "public");
            entity.HasKey(delivery => delivery.NotificationDeliveryId);

            entity.Property(delivery => delivery.NotificationDeliveryId)
                .HasColumnName("notification_delivery_id")
                .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(delivery => delivery.NotificationJobId).HasColumnName("notification_job_id");
            entity.Property(delivery => delivery.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(delivery => delivery.SentAt).HasColumnName("sent_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(delivery => delivery.Channel).HasColumnName("channel").HasMaxLength(30).IsRequired();
            entity.Property(delivery => delivery.Result).HasColumnName("result").HasMaxLength(50).IsRequired();

            entity.HasIndex(delivery => delivery.NotificationJobId).HasDatabaseName("ix_notification_deliveries_job");
            entity.HasIndex(delivery => delivery.RecipientUserId).HasDatabaseName("ix_notification_deliveries_recipient");
            entity.HasIndex(delivery => delivery.SentAt).HasDatabaseName("ix_notification_deliveries_sent_at");
            entity.HasIndex(delivery => delivery.NotificationJobId)
                .IsUnique()
                .HasFilter("result = 'Succeeded'")
                .HasDatabaseName("ux_notification_deliveries_job_success");

            entity.HasOne(delivery => delivery.NotificationJob)
                .WithMany()
                .HasForeignKey(delivery => delivery.NotificationJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(delivery => delivery.RecipientUser)
                .WithMany()
                .HasForeignKey(delivery => delivery.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
