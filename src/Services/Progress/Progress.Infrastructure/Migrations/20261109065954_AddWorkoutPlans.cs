using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workout_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    SessionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    MinutesPerSession = table.Column<int>(type: "integer", nullable: false),
                    Goal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workout_plans_UserId_WeekStartDate",
                table: "workout_plans",
                columns: new[] { "UserId", "WeekStartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workout_plans");
        }
    }
}
