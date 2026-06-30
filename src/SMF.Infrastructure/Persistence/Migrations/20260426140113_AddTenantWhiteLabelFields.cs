using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantWhiteLabelFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomDomain",
                table: "ScoringTenants",
                type: "nvarchar(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DarkLogoUrl",
                table: "ScoringTenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "ScoringTenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringTenants_CustomDomain",
                table: "ScoringTenants",
                column: "CustomDomain",
                unique: true,
                filter: "[CustomDomain] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoringTenants_CustomDomain",
                table: "ScoringTenants");

            migrationBuilder.DropColumn(
                name: "CustomDomain",
                table: "ScoringTenants");

            migrationBuilder.DropColumn(
                name: "DarkLogoUrl",
                table: "ScoringTenants");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "ScoringTenants");
        }
    }
}
