using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationPreferencesAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recommendations_seen_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Los teléfonos ya registrados siguen recibiendo los avisos de la IA.
            migrationBuilder.AddColumn<bool>(
                name: "recommendations_enabled",
                table: "push_devices",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "feedback_at",
                table: "ai_recommendations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "was_helpful",
                table: "ai_recommendations",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recommendations_seen_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "recommendations_enabled",
                table: "push_devices");

            migrationBuilder.DropColumn(
                name: "feedback_at",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "was_helpful",
                table: "ai_recommendations");
        }
    }
}
