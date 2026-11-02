using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFastingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fasting_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Protocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetHours = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fasting_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fasting_sessions_UserId",
                table: "fasting_sessions",
                column: "UserId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_fasting_sessions_UserId_StartedAt",
                table: "fasting_sessions",
                columns: new[] { "UserId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fasting_sessions");
        }
    }
}
