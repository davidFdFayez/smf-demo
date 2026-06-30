using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentalConsentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SignatureHmac = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GovernanceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CoveringYear = table.Column<int>(type: "int", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernanceDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParentalConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GuardianFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Relation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GuardianEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GuardianPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    GuardianNationalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionIpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DecisionUserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DecisionSignatureHmac = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeclineReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentalConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentalConsents_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentalConsents_PolicyDocuments_PolicyDocumentId",
                        column: x => x.PolicyDocumentId,
                        principalTable: "PolicyDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PolicyDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SignatureHmac = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAcceptances_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PolicyAcceptances_PolicyDocuments_PolicyDocumentId",
                        column: x => x.PolicyDocumentId,
                        principalTable: "PolicyDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentLogs_EventType",
                table: "ConsentLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentLogs_MemberId",
                table: "ConsentLogs",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentLogs_OccurredAtUtc",
                table: "ConsentLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentLogs_ParentalConsentId",
                table: "ConsentLogs",
                column: "ParentalConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDocuments_DocumentType",
                table: "GovernanceDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDocuments_IsPublished",
                table: "GovernanceDocuments",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDocuments_IsPublished_CoveringYear",
                table: "GovernanceDocuments",
                columns: new[] { "IsPublished", "CoveringYear" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentalConsents_MemberId",
                table: "ParentalConsents",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentalConsents_PolicyDocumentId",
                table: "ParentalConsents",
                column: "PolicyDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentalConsents_Status",
                table: "ParentalConsents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ParentalConsents_TokenHash",
                table: "ParentalConsents",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcceptances_MemberId",
                table: "PolicyAcceptances",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcceptances_MemberId_PolicyKind_AcceptedAtUtc",
                table: "PolicyAcceptances",
                columns: new[] { "MemberId", "PolicyKind", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcceptances_PolicyDocumentId",
                table: "PolicyAcceptances",
                column: "PolicyDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyDocuments_Kind_IsActive",
                table: "PolicyDocuments",
                columns: new[] { "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyDocuments_Kind_Version",
                table: "PolicyDocuments",
                columns: new[] { "Kind", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentLogs");

            migrationBuilder.DropTable(
                name: "GovernanceDocuments");

            migrationBuilder.DropTable(
                name: "ParentalConsents");

            migrationBuilder.DropTable(
                name: "PolicyAcceptances");

            migrationBuilder.DropTable(
                name: "PolicyDocuments");
        }
    }
}
