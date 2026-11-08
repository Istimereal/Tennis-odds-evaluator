using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEditionQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TournamentEditions_Level",
                table: "TournamentEditions",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEditions_Surface",
                table: "TournamentEditions",
                column: "Surface");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEditions_Year",
                table: "TournamentEditions",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TournamentEditions_Level",
                table: "TournamentEditions");

            migrationBuilder.DropIndex(
                name: "IX_TournamentEditions_Surface",
                table: "TournamentEditions");

            migrationBuilder.DropIndex(
                name: "IX_TournamentEditions_Year",
                table: "TournamentEditions");
        }
    }
}
