using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MatchesAndScoringEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HeadRefereeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoreOverrideEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeadRefereeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Red = table.Column<int>(type: "int", nullable: false),
                    Blue = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreOverrideEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrikeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefereeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FighterColor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrikeEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchReferees",
                columns: table => new
                {
                    RefereeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchReferees", x => new { x.MatchId, x.RefereeId });
                    table.ForeignKey(
                        name: "FK_MatchReferees_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Code",
                table: "Matches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreOverrideEvents_MatchId_OccurredAtUtc",
                table: "ScoreOverrideEvents",
                columns: new[] { "MatchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StrikeEvents_MatchId_OccurredAtUtc",
                table: "StrikeEvents",
                columns: new[] { "MatchId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchReferees");

            migrationBuilder.DropTable(
                name: "ScoreOverrideEvents");

            migrationBuilder.DropTable(
                name: "StrikeEvents");

            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
