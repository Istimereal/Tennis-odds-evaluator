import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { playerStatisticsQuery, type PlayerStatisticsFilters } from "../api";
import { QueryState } from "../components/QueryState";
import type { PlayerPerformance, ResultsSummary, StatLine } from "../types";

const surfaces = ["hard", "indoor hard", "clay", "grass"];
const rankingGroups = ["1-3", "top 5", "top 10", "top 10 without top 3", "11-20", "21-40", "41-70", "71-120", "121-200"];

const emptyFilters: PlayerStatisticsFilters = {
  asOfDate: "",
  from: "",
  to: "",
  surfaces: [],
  level: "",
  rankingGroup: "",
  recent: "",
};

export function PlayerStatisticsSection({ playerId }: { playerId: string }) {
  const [draft, setDraft] = useState<PlayerStatisticsFilters>(emptyFilters);
  const [filters, setFilters] = useState<PlayerStatisticsFilters>(emptyFilters);
  const [surfaceNote, setSurfaceNote] = useState("");
  const query = useQuery({
    queryKey: ["player-statistics", playerId, filters],
    queryFn: () => playerStatisticsQuery(playerId, filters),
    enabled: playerId.length > 0,
  });

  return (
    <section className="panel">
      <h2>Statistics</h2>
      <p className="muted">Completed matches only. A match on the cutoff date stays out when its finish time is not stored.</p>
      <form
        className="filters stat-filters"
        onSubmit={(event) => {
          event.preventDefault();
          setFilters(draft);
        }}
      >
        <label>
          As of
          <input aria-label="As of date" type="date" value={draft.asOfDate} onChange={(event) => setDraft({ ...draft, asOfDate: event.target.value })} />
        </label>
        <label>
          From
          <input aria-label="From date" type="date" value={draft.from} onChange={(event) => setDraft({ ...draft, from: event.target.value })} />
        </label>
        <label>
          To
          <input aria-label="To date" type="date" value={draft.to} onChange={(event) => setDraft({ ...draft, to: event.target.value })} />
        </label>
        {surfaces.map((surface) => (
          <label key={surface}>
            <input
              type="checkbox"
              checked={draft.surfaces.includes(surface)}
              onChange={() => {
                if (draft.surfaces.includes(surface)) {
                  setSurfaceNote("");
                  setDraft({ ...draft, surfaces: draft.surfaces.filter((item) => item !== surface) });
                  return;
                }
                if (draft.surfaces.length >= 2) {
                  setSurfaceNote("Select one or two surfaces.");
                  return;
                }
                setSurfaceNote("");
                setDraft({ ...draft, surfaces: [...draft.surfaces, surface] });
              }}
            />
            {labelFor(surface)}
          </label>
        ))}
        <select aria-label="Tournament level" value={draft.level} onChange={(event) => setDraft({ ...draft, level: event.target.value })}>
          <option value="">All levels</option>
          <option value="ATP Tour">ATP Tour</option>
          <option value="Challenger">Challenger</option>
        </select>
        <select aria-label="Opponent ranking group" value={draft.rankingGroup} onChange={(event) => setDraft({ ...draft, rankingGroup: event.target.value })}>
          <option value="">All ranking groups</option>
          {rankingGroups.map((group) => (
            <option key={group} value={group}>
              {labelFor(group)}
            </option>
          ))}
        </select>
        <label>
          Recent
          <input
            aria-label="Recent match count"
            type="number"
            min={1}
            max={500}
            placeholder="Matches"
            value={draft.recent}
            onChange={(event) => setDraft({ ...draft, recent: event.target.value })}
          />
        </label>
        <button type="submit">Apply</button>
      </form>
      {surfaceNote ? <p className="warning">{surfaceNote}</p> : null}
      <QueryState isPending={query.isPending} error={query.error}>
        {query.data ? <StatisticsReport report={query.data} /> : null}
      </QueryState>
    </section>
  );
}

function StatisticsReport({ report }: { report: PlayerPerformance }) {
  const missing = missingLines(report);
  return (
    <>
      <h3>Overall</h3>
      <ResultsTable rows={[{ label: "All completed matches", results: report.overall }]} />
      <p className="muted">
        Average opponent ranking {report.opponentRanking.average === null ? "—" : report.opponentRanking.average.toFixed(1)} from{" "}
        {report.opponentRanking.sampleSize} stored ranks.
      </p>
      <h3>Surface</h3>
      <ResultsTable rows={report.surfaces.map((row) => ({ label: labelFor(row.surface), results: row.results }))} />
      <h3>Tournament level</h3>
      <ResultsTable rows={report.levels.map((row) => ({ label: row.level, results: row.results }))} />
      <h3>Opponent ranking</h3>
      <p className="muted">Groups overlap. Rank 2 counts in 1-3, top 5, and top 10.</p>
      <ResultsTable rows={report.rankingGroups.map((row) => ({ label: labelFor(row.group), results: row.results }))} />
      <h3>Recent form</h3>
      <ResultsTable rows={report.recentForm.map((row) => ({ label: row.label, results: row.results }))} />
      <h3>Serve and return</h3>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Statistic</th>
              <th>Value</th>
              <th>Basis</th>
              <th>Sample</th>
            </tr>
          </thead>
          <tbody>
            {report.statistics.map((line) => (
              <tr key={line.key}>
                <td>{line.label}</td>
                <td>{formatStat(line)}</td>
                <td>{line.sampleSize === 0 ? "No stored values" : line.basis}</td>
                <td>{line.sampleSize}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h3>Missing data</h3>
      {missing.length === 0 ? (
        <p className="muted">The stored fields used above are present for every match in this sample.</p>
      ) : (
        <ul className="missing">
          {missing.map((line) => (
            <li key={line}>{line}</li>
          ))}
        </ul>
      )}
    </>
  );
}

function ResultsTable({ rows }: { rows: { label: string; results: ResultsSummary }[] }) {
  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Group</th>
            <th>Matches</th>
            <th>Wins</th>
            <th>Losses</th>
            <th>Win %</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.label}>
              <td>{row.label}</td>
              <td>{row.results.matches}</td>
              <td>{row.results.wins}</td>
              <td>{row.results.losses}</td>
              <td>{row.results.winPercentage === null ? "—" : `${row.results.winPercentage.toFixed(1)}%`}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function formatStat(line: StatLine) {
  if (line.sampleSize === 0 || line.value === null) {
    return "—";
  }
  if (line.kind === "count") {
    const perMatch = line.perMatch === null ? "" : ` (${line.perMatch.toFixed(1)} per match)`;
    return `${line.total ?? line.value}${perMatch}`;
  }
  const ratio = line.numerator !== null && line.denominator !== null ? ` (${line.numerator}/${line.denominator})` : "";
  return `${line.value.toFixed(1)}%${ratio}`;
}

function missingLines(report: PlayerPerformance) {
  const coverage = report.coverage;
  const lines: string[] = [];
  if (coverage.excludedMatches > 0) {
    lines.push(`${coverage.excludedMatches} exhibition, cancelled, unfinished, or invalid matches left out`);
  }
  if (coverage.undecidedResults > 0) {
    lines.push(`${coverage.undecidedResults} matches without a win or loss left out`);
  }
  if (coverage.missingOpponentRanking > 0) {
    lines.push(`Opponent ranking missing on ${coverage.missingOpponentRanking} matches`);
  }
  if (coverage.missingOrOtherSurface > 0) {
    lines.push(`Surface missing or outside hard, indoor hard, clay, and grass on ${coverage.missingOrOtherSurface} matches`);
  }
  pushMissing(lines, "Aces", coverage.missingAces);
  pushMissing(lines, "Double faults", coverage.missingDoubleFaults);
  pushMissing(lines, "Service points won", coverage.missingServicePointsWon);
  pushMissing(lines, "Return points won", coverage.missingReturnPointsWon);
  pushMissing(lines, "Break points won", coverage.missingBreakPointsWon);
  pushMissing(lines, "Break points saved", coverage.missingBreakPointsSaved);
  for (const line of report.statistics) {
    if (line.note) {
      lines.push(`${line.label}: ${line.note}`);
    }
  }
  return lines;
}

function pushMissing(lines: string[], label: string, count: number) {
  if (count > 0) {
    lines.push(`${label} missing on ${count} matches`);
  }
}

function labelFor(value: string) {
  if (value.length === 0) {
    return value;
  }
  return value.charAt(0).toUpperCase() + value.slice(1);
}
