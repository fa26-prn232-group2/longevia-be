using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterAndSleep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sleep_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SleptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WokeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SleepQuality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationHours = table.Column<double>(type: "double precision", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sleep_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "water_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMl = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    LoggedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_water_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sleep_logs_UserId_Date",
                table: "sleep_logs",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_sleep_logs_UserId_WokeAt",
                table: "sleep_logs",
                columns: new[] { "UserId", "WokeAt" });

            migrationBuilder.CreateIndex(
                name: "IX_water_logs_UserId_Date",
                table: "water_logs",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sleep_logs");

            migrationBuilder.DropTable(
                name: "water_logs");
        }
    }
}
