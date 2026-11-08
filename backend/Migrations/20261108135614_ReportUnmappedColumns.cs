using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReportUnmappedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerId",
                table: "MatchPlayers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "UnmappedColumns",
                table: "ImportFiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnmappedColumns",
                table: "ImportFiles");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerId",
                table: "MatchPlayers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
