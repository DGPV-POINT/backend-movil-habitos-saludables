using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    lockout_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    focus_habit = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    recommendation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_recommendations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    goal = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scheduled_days = table.Column<short>(type: "smallint", nullable: false),
                    tracking_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    server_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habits", x => x.id);
                    table.ForeignKey(
                        name: "fk_habits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    habit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_date = table.Column<DateOnly>(type: "date", nullable: false),
                    completed_value = table.Column<int>(type: "integer", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    server_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_habit_records_habits_habit_id",
                        column: x => x.habit_id,
                        principalTable: "habits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habit_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_recommendations_user_id_created_at",
                table: "ai_recommendations",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_habit_records_habit_id_record_date",
                table: "habit_records",
                columns: new[] { "habit_id", "record_date" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_habit_records_user_id_record_date",
                table: "habit_records",
                columns: new[] { "user_id", "record_date" });

            migrationBuilder.CreateIndex(
                name: "ix_habit_records_user_id_server_modified_at",
                table: "habit_records",
                columns: new[] { "user_id", "server_modified_at" });

            migrationBuilder.CreateIndex(
                name: "ix_habits_user_id_server_modified_at",
                table: "habits",
                columns: new[] { "user_id", "server_modified_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_device_id",
                table: "refresh_tokens",
                columns: new[] { "user_id", "device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_username",
                table: "users",
                column: "normalized_username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_role",
                table: "users",
                column: "role");

            // EF no modela índices por expresión: nombre único por usuario, sin mayúsculas y solo entre vigentes.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_habits_user_id_lower_name ON habits (user_id, lower(name)) WHERE deleted_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_habits_user_id_lower_name;");

            migrationBuilder.DropTable(
                name: "ai_recommendations");

            migrationBuilder.DropTable(
                name: "habit_records");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "habits");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
