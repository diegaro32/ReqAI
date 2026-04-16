using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedAnalysisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessRules",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DomainModel",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SystemInsights",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SystemOverview",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessRules",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "DomainModel",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "SystemInsights",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "SystemOverview",
                table: "RequirementGenerations");
        }
    }
}
