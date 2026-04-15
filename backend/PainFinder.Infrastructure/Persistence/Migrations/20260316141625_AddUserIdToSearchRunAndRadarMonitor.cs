using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToSearchRunAndRadarMonitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "SearchRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "RadarMonitors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchRuns_UserId",
                table: "SearchRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RadarMonitors_UserId",
                table: "RadarMonitors",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RadarMonitors_AspNetUsers_UserId",
                table: "RadarMonitors",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SearchRuns_AspNetUsers_UserId",
                table: "SearchRuns",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RadarMonitors_AspNetUsers_UserId",
                table: "RadarMonitors");

            migrationBuilder.DropForeignKey(
                name: "FK_SearchRuns_AspNetUsers_UserId",
                table: "SearchRuns");

            migrationBuilder.DropIndex(
                name: "IX_SearchRuns_UserId",
                table: "SearchRuns");

            migrationBuilder.DropIndex(
                name: "IX_RadarMonitors_UserId",
                table: "RadarMonitors");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SearchRuns");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RadarMonitors");
        }
    }
}
