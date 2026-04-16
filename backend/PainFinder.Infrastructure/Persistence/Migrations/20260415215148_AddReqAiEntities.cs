using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReqAiEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalInput = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FunctionalRequirements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NonFunctionalRequirements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ambiguities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuggestedFeatures = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementGenerations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefinementResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RefinedOutput = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefinementResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefinementResults_RequirementGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "RequirementGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UserId",
                table: "Projects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefinementResults_GenerationId",
                table: "RefinementResults",
                column: "GenerationId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementGenerations_ProjectId",
                table: "RequirementGenerations",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefinementResults");

            migrationBuilder.DropTable(
                name: "RequirementGenerations");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
