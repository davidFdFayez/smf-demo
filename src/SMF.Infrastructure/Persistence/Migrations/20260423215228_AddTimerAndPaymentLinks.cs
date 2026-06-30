using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimerAndPaymentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventRegistrationId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentRound",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsTimerRunning",
                table: "Matches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RoundDurationSeconds",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RoundElapsedSecondsAtStart",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RoundStartedAtUtc",
                table: "Matches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TimekeeperId",
                table: "Matches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoundTimerEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimekeeperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    RoundDurationSeconds = table.Column<int>(type: "int", nullable: false),
                    ElapsedSecondsAtEvent = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundTimerEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_EventRegistrationId",
                table: "Payments",
                column: "EventRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTimerEvents_MatchId_OccurredAtUtc",
                table: "RoundTimerEvents",
                columns: new[] { "MatchId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoundTimerEvents");

            migrationBuilder.DropIndex(
                name: "IX_Payments_EventRegistrationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "EventRegistrationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CurrentRound",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "IsTimerRunning",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RoundDurationSeconds",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RoundElapsedSecondsAtStart",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RoundStartedAtUtc",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "TimekeeperId",
                table: "Matches");
        }
    }
}
