using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationTokenLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationTokenConsumedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationTokenExpiresAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationTokenHash",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_VerificationTokenHash",
                table: "users",
                column: "VerificationTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_VerificationTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VerificationTokenConsumedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VerificationTokenExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VerificationTokenHash",
                table: "users");
        }
    }
}
