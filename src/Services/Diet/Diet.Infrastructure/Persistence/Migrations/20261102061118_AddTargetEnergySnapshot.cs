using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetEnergySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Calculation_ActivityFactor",
                table: "daily_diet_targets",
                type: "numeric(6,4)",
                precision: 6,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Calculation_AgeYears",
                table: "daily_diet_targets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Calculation_Bmr",
                table: "daily_diet_targets",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Calculation_CalculatedAt",
                table: "daily_diet_targets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Calculation_HeightCm",
                table: "daily_diet_targets",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Calculation_IsMale",
                table: "daily_diet_targets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Calculation_SourceMetricId",
                table: "daily_diet_targets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Calculation_Tdee",
                table: "daily_diet_targets",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Calculation_WeightKg",
                table: "daily_diet_targets",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Calculation_WeightMeasuredAt",
                table: "daily_diet_targets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Calculation_WeightRequestedAsOf",
                table: "daily_diet_targets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calculation_ActivityFactor",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_AgeYears",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_Bmr",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_CalculatedAt",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_HeightCm",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_IsMale",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_SourceMetricId",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_Tdee",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_WeightKg",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_WeightMeasuredAt",
                table: "daily_diet_targets");

            migrationBuilder.DropColumn(
                name: "Calculation_WeightRequestedAsOf",
                table: "daily_diet_targets");
        }
    }
}
