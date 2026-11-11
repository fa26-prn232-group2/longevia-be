using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailChangeRequestedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailChangeTokenConsumedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailChangeTokenExpiresAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailChangeTokenHash",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailChangeRequestedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailChangeTokenConsumedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailChangeTokenExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailChangeTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PendingEmail",
                table: "users");
        }
    }
}
