using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProblemStatement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TargetUsers = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CoreFeaturesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TechStack = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ValidationStrategy = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FirstStep = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EstimatedTimeline = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExactIcp = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ValueProposition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OutreachMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ValidationTest = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FirstStepTomorrow = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionPlans_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlans_OpportunityId",
                table: "ActionPlans",
                column: "OpportunityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionPlans");
        }
    }
}
