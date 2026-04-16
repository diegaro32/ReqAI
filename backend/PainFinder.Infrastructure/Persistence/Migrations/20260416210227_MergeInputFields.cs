using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeInputFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanedInput",
                table: "RequirementGenerations");

            migrationBuilder.RenameColumn(
                name: "OriginalInput",
                table: "RequirementGenerations",
                newName: "ConversationInput");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ConversationInput",
                table: "RequirementGenerations",
                newName: "OriginalInput");

            migrationBuilder.AddColumn<string>(
                name: "CleanedInput",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
