using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

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
    }
}
