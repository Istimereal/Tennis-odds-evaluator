using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchSetsAndPlayerStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchPlayerStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Aces = table.Column<int>(type: "integer", nullable: true),
                    DoubleFaults = table.Column<int>(type: "integer", nullable: true),
                    ServicePointsWonPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    ReturnPointsWonPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    BreakPointsWonPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    BreakPointsSavedPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    FirstServePercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    FirstServePointsWonPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    SecondServePointsWonPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    BreakPointsFaced = table.Column<int>(type: "integer", nullable: true),
                    BreakPointsSaved = table.Column<int>(type: "integer", nullable: true),
                    BreakPointChances = table.Column<int>(type: "integer", nullable: true),
                    BreakPointsConverted = table.Column<int>(type: "integer", nullable: true),
                    Winners = table.Column<int>(type: "integer", nullable: true),
                    UnforcedErrors = table.Column<int>(type: "integer", nullable: true),
                    NetPointsWon = table.Column<int>(type: "integer", nullable: true),
                    TotalPointsWon = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayerStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPlayerStatistics_MatchPlayers_MatchPlayerId",
                        column: x => x.MatchPlayerId,
                        principalTable: "MatchPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetNumber = table.Column<int>(type: "integer", nullable: false),
                    HomeGames = table.Column<int>(type: "integer", nullable: true),
                    AwayGames = table.Column<int>(type: "integer", nullable: true),
                    HomeTiebreak = table.Column<int>(type: "integer", nullable: true),
                    AwayTiebreak = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSets", x => x.Id);
                    table.CheckConstraint("CK_MatchSets_SetNumber", "\"SetNumber\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_MatchSets_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerStatistics_MatchPlayerId",
                table: "MatchPlayerStatistics",
                column: "MatchPlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchSets_MatchId_SetNumber",
                table: "MatchSets",
                columns: new[] { "MatchId", "SetNumber" },
                unique: true);

            // Copy parsed values out of RawRecord for matches already stored. This inserts child rows only.
            // home_points and away_points stay in RawRecord. Reserved statistic columns stay null.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION tennis_parse_count(value text) RETURNS integer
                LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE
                  cleaned text;
                BEGIN
                  IF value IS NULL THEN
                    RETURN NULL;
                  END IF;
                  cleaned := btrim(value);
                  IF cleaned ~ '^[0-9]{1,9}$' THEN
                    RETURN cleaned::integer;
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE FUNCTION tennis_parse_percent(value text) RETURNS numeric
                LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE
                  cleaned text;
                  parsed numeric;
                BEGIN
                  IF value IS NULL THEN
                    RETURN NULL;
                  END IF;
                  cleaned := btrim(value);
                  IF right(cleaned, 1) = '%' THEN
                    cleaned := btrim(left(cleaned, length(cleaned) - 1));
                  END IF;
                  IF cleaned ~ '^[0-9]+(\.[0-9]+)?$' THEN
                    parsed := round(cleaned::numeric, 3);
                    IF parsed >= 10000 THEN
                      RETURN NULL;
                    END IF;
                    RETURN parsed;
                  END IF;
                  RETURN NULL;
                END;
                $$;

                INSERT INTO "MatchPlayerStatistics" (
                    "Id",
                    "MatchPlayerId",
                    "Aces",
                    "DoubleFaults",
                    "ServicePointsWonPercent",
                    "ReturnPointsWonPercent",
                    "BreakPointsWonPercent",
                    "BreakPointsSavedPercent")
                SELECT
                    gen_random_uuid(),
                    mp."Id",
                    tennis_parse_count(m."RawRecord"->>(mp."Side" || '_aces')),
                    tennis_parse_count(m."RawRecord"->>(mp."Side" || '_double_faults')),
                    tennis_parse_percent(m."RawRecord"->>(mp."Side" || '_service_points_won_perc')),
                    tennis_parse_percent(m."RawRecord"->>(mp."Side" || '_return_points_won_perc')),
                    COALESCE(
                        tennis_parse_percent(m."RawRecord"->>(mp."Side" || '_break_points_won_perc')),
                        tennis_parse_percent(m."RawRecord"->>(mp."Side" || '_break_points_converted_perc'))),
                    tennis_parse_percent(m."RawRecord"->>(mp."Side" || '_break_points_saved_perc'))
                FROM "MatchPlayers" mp
                INNER JOIN "Matches" m ON m."Id" = mp."MatchId"
                WHERE mp."Side" IN ('home', 'away');

                INSERT INTO "MatchSets" ("Id", "MatchId", "SetNumber", "HomeGames", "AwayGames", "HomeTiebreak", "AwayTiebreak")
                SELECT
                    gen_random_uuid(),
                    m."Id",
                    s.set_number,
                    tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_score')),
                    tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_score')),
                    COALESCE(
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tiebreak')),
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tb')),
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tb_score')),
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tiebreak_score'))),
                    COALESCE(
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tiebreak')),
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tb')),
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tb_score')),
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tiebreak_score')))
                FROM "Matches" m
                CROSS JOIN (VALUES (1), (2), (3), (4), (5)) AS s(set_number)
                WHERE tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_score')) IS NOT NULL
                   OR tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_score')) IS NOT NULL
                   OR COALESCE(
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tiebreak')),
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tb')),
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tb_score')),
                        tennis_parse_count(m."RawRecord"->>('home_set_' || s.set_number || '_tiebreak_score'))) IS NOT NULL
                   OR COALESCE(
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tiebreak')),
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tb')),
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tb_score')),
                        tennis_parse_count(m."RawRecord"->>('away_set_' || s.set_number || '_tiebreak_score'))) IS NOT NULL;

                DROP FUNCTION tennis_parse_count(text);
                DROP FUNCTION tennis_parse_percent(text);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPlayerStatistics");

            migrationBuilder.DropTable(
                name: "MatchSets");
        }
    }
}
