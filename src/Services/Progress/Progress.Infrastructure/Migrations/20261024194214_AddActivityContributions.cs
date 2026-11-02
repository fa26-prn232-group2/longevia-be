using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_contributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceLogId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_contributions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_contributions_Source_SourceLogId",
                table: "activity_contributions",
                columns: new[] { "Source", "SourceLogId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activity_contributions_UserId_Date",
                table: "activity_contributions",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_contributions");
        }
    }
}
