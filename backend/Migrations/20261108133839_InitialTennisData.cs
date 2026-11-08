using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialTennisData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Provenance = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FilesProcessed = table.Column<int>(type: "integer", nullable: false),
                    RowsRead = table.Column<int>(type: "integer", nullable: false),
                    MatchesInserted = table.Column<int>(type: "integer", nullable: false),
                    PlayersInserted = table.Column<int>(type: "integer", nullable: false),
                    DuplicatesSkipped = table.Column<int>(type: "integer", nullable: false),
                    InvalidRowsSkipped = table.Column<int>(type: "integer", nullable: false),
                    MissingStatisticCells = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceYear = table.Column<int>(type: "integer", nullable: true),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    Provenance = table.Column<string>(type: "text", nullable: false),
                    HeaderJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RowsRead = table.Column<int>(type: "integer", nullable: false),
                    MatchesInserted = table.Column<int>(type: "integer", nullable: false),
                    DuplicatesSkipped = table.Column<int>(type: "integer", nullable: false),
                    InvalidRowsSkipped = table.Column<int>(type: "integer", nullable: false),
                    MissingStatisticCells = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportFiles_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentEditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Surface = table.Column<string>(type: "text", nullable: true),
                    Level = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentEditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentEditions_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "text", nullable: false),
                    SourceYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonFileName = table.Column<string>(type: "text", nullable: false),
                    WinnerCode = table.Column<short>(type: "smallint", nullable: true),
                    PlayedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RawRecord = table.Column<string>(type: "jsonb", nullable: false),
                    TournamentEditionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportFileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_ImportFiles_ImportFileId",
                        column: x => x.ImportFileId,
                        principalTable: "ImportFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_TournamentEditions_TournamentEditionId",
                        column: x => x.TournamentEditionId,
                        principalTable: "TournamentEditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<string>(type: "text", nullable: false),
                    IsWinner = table.Column<bool>(type: "boolean", nullable: true),
                    Ranking = table.Column<int>(type: "integer", nullable: true),
                    Statistics = table.Column<string>(type: "jsonb", nullable: false),
                    Odds = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_StartedAt",
                table: "ImportBatches",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_ImportBatchId",
                table: "ImportFiles",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_Sha256",
                table: "ImportFiles",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ImportFileId",
                table: "Matches",
                column: "ImportFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlayedOn",
                table: "Matches",
                column: "PlayedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_SourceKey",
                table: "Matches",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_SourceYear",
                table: "Matches",
                column: "SourceYear");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TournamentEditionId",
                table: "Matches",
                column: "TournamentEditionId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_MatchId_Side",
                table: "MatchPlayers",
                columns: new[] { "MatchId", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_PlayerId",
                table: "MatchPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_SourceId",
                table: "Players",
                column: "SourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEditions_TournamentId_Year",
                table: "TournamentEditions",
                columns: new[] { "TournamentId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_SourceId",
                table: "Tournaments",
                column: "SourceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPlayers");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "ImportFiles");

            migrationBuilder.DropTable(
                name: "TournamentEditions");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
