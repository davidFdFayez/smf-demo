using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsSafeguardingAndAthleteReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MedicalCleared",
                table: "Members",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MedicalClearedAtUtc",
                table: "Members",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightCategoryKg",
                table: "Members",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewsArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthorDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SafeguardingReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    IncidentLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    ReporterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReporterEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReporterPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReviewerNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafeguardingReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_IsPublished_PublishedAtUtc",
                table: "NewsArticles",
                columns: new[] { "IsPublished", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_Slug",
                table: "NewsArticles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafeguardingReports_ReferenceCode",
                table: "SafeguardingReports",
                column: "ReferenceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafeguardingReports_Status",
                table: "SafeguardingReports",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsArticles");

            migrationBuilder.DropTable(
                name: "SafeguardingReports");

            migrationBuilder.DropColumn(
                name: "MedicalCleared",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MedicalClearedAtUtc",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "WeightCategoryKg",
                table: "Members");
        }
    }
}
