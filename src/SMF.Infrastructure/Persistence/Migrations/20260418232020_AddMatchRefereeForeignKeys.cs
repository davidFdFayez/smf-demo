using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRefereeForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Matches_HeadRefereeId",
                table: "Matches",
                column: "HeadRefereeId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReferees_RefereeId",
                table: "MatchReferees",
                column: "RefereeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchReferees_Members_RefereeId",
                table: "MatchReferees",
                column: "RefereeId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Members_HeadRefereeId",
                table: "Matches",
                column: "HeadRefereeId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchReferees_Members_RefereeId",
                table: "MatchReferees");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Members_HeadRefereeId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_HeadRefereeId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_MatchReferees_RefereeId",
                table: "MatchReferees");
        }
    }
}
