using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Event> Events => Set<Event>(); 
    public DbSet<Notification> Notifications => Set<Notification>();

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

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications", "public");
            entity.HasKey(n => n.NotificationId);

            entity.Property(n => n.NotificationId).HasColumnName("notification_id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(n => n.UserId).HasColumnName("user_id");
            entity.Property(n => n.EventId).HasColumnName("event_id");
            entity.Property(n => n.NotificationType).HasColumnName("notification_type").HasMaxLength(100).IsRequired();
            entity.Property(n => n.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(255).IsRequired();
            entity.Property(n => n.MessageBody).HasColumnName("message_body").IsRequired();
            entity.Property(n => n.IsSent).HasColumnName("is_sent").IsRequired();
            entity.Property(n => n.RetryCount).HasColumnName("retry_count").IsRequired();
            entity.Property(n => n.FailureReason).HasColumnName("failure_reason").HasMaxLength(255);
            entity.Property(n => n.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(n => n.SentAt).HasColumnName("sent_at");
        });
    }
}