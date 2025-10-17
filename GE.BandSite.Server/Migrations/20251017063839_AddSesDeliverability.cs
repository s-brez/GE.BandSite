using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace GE.BandSite.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSesDeliverability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SesFeedbackEvent",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SesMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SesFeedbackId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceivedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    SourceEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceArn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TopicArn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesFeedbackEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailSuppression",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReasonDetail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FeedbackEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstSuppressedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    LastSuppressedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    SuppressionCount = table.Column<int>(type: "integer", nullable: false),
                    ReleasedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    ReleaseDetail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSuppression", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailSuppression_SesFeedbackEvent_FeedbackEventId",
                        column: x => x.FeedbackEventId,
                        principalSchema: "Organization",
                        principalTable: "SesFeedbackEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SesFeedbackRecipient",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    BounceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BounceSubType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BounceAction = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BounceStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DiagnosticCode = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ComplaintFeedbackType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplaintSubType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplaintType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Detail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RecipientIndex = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesFeedbackRecipient", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesFeedbackRecipient_SesFeedbackEvent_FeedbackEventId",
                        column: x => x.FeedbackEventId,
                        principalSchema: "Organization",
                        principalTable: "SesFeedbackEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSuppression_FeedbackEventId",
                schema: "Organization",
                table: "EmailSuppression",
                column: "FeedbackEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSuppression_NormalizedEmail",
                schema: "Organization",
                table: "EmailSuppression",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailSuppression_ReleasedAt",
                schema: "Organization",
                table: "EmailSuppression",
                column: "ReleasedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SesFeedbackEvent_NotificationType_ReceivedAt",
                schema: "Organization",
                table: "SesFeedbackEvent",
                columns: new[] { "NotificationType", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SesFeedbackEvent_SesMessageId",
                schema: "Organization",
                table: "SesFeedbackEvent",
                column: "SesMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesFeedbackRecipient_FeedbackEventId",
                schema: "Organization",
                table: "SesFeedbackRecipient",
                column: "FeedbackEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SesFeedbackRecipient_NormalizedEmail",
                schema: "Organization",
                table: "SesFeedbackRecipient",
                column: "NormalizedEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailSuppression",
                schema: "Organization");

            migrationBuilder.DropTable(
                name: "SesFeedbackRecipient",
                schema: "Organization");

            migrationBuilder.DropTable(
                name: "SesFeedbackEvent",
                schema: "Organization");
        }
    }
}
