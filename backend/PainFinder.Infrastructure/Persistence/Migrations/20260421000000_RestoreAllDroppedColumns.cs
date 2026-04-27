using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreAllDroppedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeep",
                table: "RequirementGenerations");

            migrationBuilder.AddColumn<string>(
                name: "DecisionPoints",
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
                name: "ImplementationRisks",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Inconsistencies",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LifecycleModel",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnershipActions",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionPoints",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "DomainModel",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "ImplementationRisks",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "Inconsistencies",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "LifecycleModel",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "OwnershipActions",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "SystemInsights",
                table: "RequirementGenerations");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeep",
                table: "RequirementGenerations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
