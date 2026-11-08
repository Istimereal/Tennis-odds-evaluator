import { useQuery } from "@tanstack/react-query";
import { dashboardQuery } from "../api";
import { EmptyNote, QueryState, dash } from "../components/QueryState";

export function DashboardPage() {
  const query = useQuery({ queryKey: ["dashboard"], queryFn: dashboardQuery });

  return (
    <QueryState isPending={query.isPending} error={query.error}>
      {query.data && query.data.players === 0 && query.data.matches === 0 && query.data.tournaments === 0 ? (
        <EmptyNote>
          <p>The tennis database has no players, matches, or tournaments.</p>
          <p className="muted">
            There is nothing to plot. Place yearly files named yyyy-atp-season.csv on Data Imports.
            2020 is a coverage gap. Latest import: {query.data.latestBatch ? query.data.latestBatch.startedAt : "none"}. Failed files:{" "}
            {query.data.failedFiles}. Batches with errors: {query.data.batchesWithErrors}.
          </p>
        </EmptyNote>
      ) : query.data ? (
        <>
          <section className="cards">
            <article className="card">
              Players<strong>{query.data.players}</strong>
            </article>
            <article className="card">
              Matches<strong>{query.data.matches}</strong>
            </article>
            <article className="card">
              Tournaments<strong>{query.data.tournaments}</strong>
            </article>
            <article className="card">
              Imported seasons<strong>{query.data.importedSeasons}</strong>
            </article>
          </section>
          <section className="panel">
            <p>
              Latest batch: {dash(query.data.latestBatch?.startedAt)} · Latest successful import:{" "}
              {dash(query.data.latestSuccessfulImport?.finishedAt)}
            </p>
            <p className="muted">
              Failed files {query.data.failedFiles}. Batches with an error {query.data.batchesWithErrors}. Match date stored on{" "}
              {query.data.completeness.matchesWithMatchDate} of {query.data.completeness.matches}. Tournament start stored on{" "}
              {query.data.completeness.matchesWithTournamentStart}. Winner code on {query.data.completeness.matchesWithWinner}. Surface on{" "}
              {query.data.completeness.matchesWithSurface}. Level on {query.data.completeness.matchesWithLevel}. Missing statistic cells{" "}
              {query.data.completeness.missingStatisticCells}.
            </p>
          </section>
          <Chart title="Matches by year" rows={query.data.matchesByYear.map((row) => ({ label: String(row.year), count: row.count }))} />
          <Chart title="Matches by surface" rows={query.data.matchesBySurface.map((row) => ({ label: row.name, count: row.count }))} />
          <Chart title="Matches by level" rows={query.data.matchesByLevel.map((row) => ({ label: row.name, count: row.count }))} />
        </>
      ) : null}
    </QueryState>
  );
}

function Chart({ title, rows }: { title: string; rows: { label: string; count: number }[] }) {
  const max = Math.max(1, ...rows.map((row) => row.count));
  return (
    <section className="panel">
      <h2>{title}</h2>
      {rows.length === 0 ? (
        <p className="muted">No rows.</p>
      ) : (
        <div className="bars">
          {rows.map((row) => (
            <div className="bar-row" key={row.label}>
              <span>{row.label}</span>
              <div className="bar">
                <span style={{ width: `${(row.count / max) * 100}%` }} />
              </div>
              <span>{row.count}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
