using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace GE.BandSite.Server.Migrations;

/// <inheritdoc />
public partial class AddContactSubmissionTimezone : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EventTimezone",
            schema: "Organization",
            table: "ContactSubmission",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BandMemberProfile",
            schema: "Organization");

        migrationBuilder.DropTable(
            name: "ContactNotificationRecipient",
            schema: "Organization");

        migrationBuilder.DropTable(
            name: "ContactSubmission",
            schema: "Organization");

        migrationBuilder.DropTable(
            name: "EventListing",
            schema: "Organization");

        migrationBuilder.DropTable(
            name: "MediaAssetTag",
            schema: "Media");

        migrationBuilder.DropTable(
            name: "PasswordResetRequest",
            schema: "Authentication");

        migrationBuilder.DropTable(
            name: "RefreshToken",
            schema: "Authentication");

        migrationBuilder.DropTable(
            name: "Testimonial",
            schema: "Organization");

        migrationBuilder.DropTable(
            name: "MediaAsset",
            schema: "Media");

        migrationBuilder.DropTable(
            name: "MediaTag",
            schema: "Media");

        migrationBuilder.DropTable(
            name: "User",
            schema: "Organization");
    }
}
