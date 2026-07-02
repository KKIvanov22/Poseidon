using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "event_statuses",
                schema: "public",
                columns: table => new
                {
                    event_status_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_statuses", x => x.event_status_id);
                });

            migrationBuilder.CreateTable(
                name: "notification_job_statuses",
                schema: "public",
                columns: table => new
                {
                    job_status_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_job_statuses", x => x.job_status_id);
                });

            migrationBuilder.CreateTable(
                name: "registration_statuses",
                schema: "public",
                columns: table => new
                {
                    registration_status_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_statuses", x => x.registration_status_id);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "public",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_users_user_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "public",
                        principalTable: "user_roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "public",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    organizer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_status_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    location_text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.event_id);
                    table.CheckConstraint("ck_events_capacity", "capacity >= 1");
                    table.CheckConstraint("ck_events_time", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "FK_events_event_statuses_event_status_id",
                        column: x => x.event_status_id,
                        principalSchema: "public",
                        principalTable: "event_statuses",
                        principalColumn: "event_status_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_events_users_organizer_id",
                        column: x => x.organizer_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_jobs",
                schema: "public",
                columns: table => new
                {
                    notification_job_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_status_id = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Email"),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    publisher_locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_jobs", x => x.notification_job_id);
                    table.CheckConstraint("ck_notification_jobs_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_notification_jobs_channel", "channel IN ('Email')");
                    table.ForeignKey(
                        name: "FK_notification_jobs_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_jobs_notification_job_statuses_job_status_id",
                        column: x => x.job_status_id,
                        principalSchema: "public",
                        principalTable: "notification_job_statuses",
                        principalColumn: "job_status_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_jobs_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrations",
                schema: "public",
                columns: table => new
                {
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_status_id = table.Column<int>(type: "integer", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrations", x => x.registration_id);
                    table.ForeignKey(
                        name: "FK_registrations_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_registrations_registration_statuses_registration_status_id",
                        column: x => x.registration_status_id,
                        principalSchema: "public",
                        principalTable: "registration_statuses",
                        principalColumn: "registration_status_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrations_users_student_id",
                        column: x => x.student_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                schema: "public",
                columns: table => new
                {
                    notification_delivery_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    notification_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.notification_delivery_id);
                    table.CheckConstraint("ck_notification_deliveries_channel", "channel IN ('Email')");
                    table.CheckConstraint("ck_notification_deliveries_result", "result IN ('Succeeded', 'Failed')");
                    table.ForeignKey(
                        name: "FK_notification_deliveries_notification_jobs_notification_job_~",
                        column: x => x.notification_job_id,
                        principalSchema: "public",
                        principalTable: "notification_jobs",
                        principalColumn: "notification_job_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "event_statuses",
                columns: new[] { "event_status_id", "status_name" },
                values: new object[,]
                {
                    { 1, "Draft" },
                    { 2, "Published" },
                    { 3, "Cancelled" },
                    { 4, "Completed" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "notification_job_statuses",
                columns: new[] { "job_status_id", "status_name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Processing" },
                    { 3, "Succeeded" },
                    { 4, "Failed" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "registration_statuses",
                columns: new[] { "registration_status_id", "status_name" },
                values: new object[,]
                {
                    { 1, "Confirmed" },
                    { 2, "Waitlisted" },
                    { 3, "Cancelled" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "user_roles",
                columns: new[] { "role_id", "role_name" },
                values: new object[,]
                {
                    { 1, "Student" },
                    { 2, "Teacher" },
                    { 3, "Admin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_statuses_status_name",
                schema: "public",
                table: "event_statuses",
                column: "status_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_date_range",
                schema: "public",
                table: "events",
                columns: new[] { "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_events_organizer",
                schema: "public",
                table: "events",
                column: "organizer_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_starts_at",
                schema: "public",
                table: "events",
                column: "starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_events_status",
                schema: "public",
                table: "events",
                column: "event_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_recipient",
                schema: "public",
                table: "notification_deliveries",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_sent_at",
                schema: "public",
                table: "notification_deliveries",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ux_notification_deliveries_job_success",
                schema: "public",
                table: "notification_deliveries",
                column: "notification_job_id",
                unique: true,
                filter: "result = 'Succeeded'");

            migrationBuilder.CreateIndex(
                name: "IX_notification_job_statuses_status_name",
                schema: "public",
                table: "notification_job_statuses",
                column: "status_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_jobs_event",
                schema: "public",
                table: "notification_jobs",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_jobs_payload",
                schema: "public",
                table: "notification_jobs",
                column: "payload")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_notification_jobs_publishable",
                schema: "public",
                table: "notification_jobs",
                columns: new[] { "job_status_id", "available_at", "publisher_locked_until" },
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_jobs_recipient",
                schema: "public",
                table: "notification_jobs",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_jobs_status_available",
                schema: "public",
                table: "notification_jobs",
                columns: new[] { "job_status_id", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_registration_statuses_status_name",
                schema: "public",
                table: "registration_statuses",
                column: "status_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_registrations_confirmed",
                schema: "public",
                table: "registrations",
                columns: new[] { "event_id", "registration_status_id" },
                filter: "cancelled_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_registrations_registration_status_id",
                schema: "public",
                table: "registrations",
                column: "registration_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_registrations_student_id",
                schema: "public",
                table: "registrations",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_registrations_waitlist_order",
                schema: "public",
                table: "registrations",
                columns: new[] { "event_id", "registration_status_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "ux_registrations_one_active",
                schema: "public",
                table: "registrations",
                columns: new[] { "event_id", "student_id" },
                unique: true,
                filter: "cancelled_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_name",
                schema: "public",
                table: "user_roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_role",
                schema: "public",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                schema: "public",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.set_updated_at()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.updated_at = CURRENT_TIMESTAMP;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_events_set_updated_at
                BEFORE UPDATE ON public.events
                FOR EACH ROW
                EXECUTE FUNCTION public.set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_events_set_updated_at ON public.events;
                DROP FUNCTION IF EXISTS public.set_updated_at();
                """);

            migrationBuilder.DropTable(
                name: "notification_deliveries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "registrations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "notification_jobs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "registration_statuses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "notification_job_statuses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "event_statuses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "public");
        }
    }
}
