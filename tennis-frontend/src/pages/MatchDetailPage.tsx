import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { matchQuery } from "../api";
import { QueryState, dash } from "../components/QueryState";
import type { MatchConditions, MatchPlayer, MatchRow, MatchSetScore, PlayerStatistics } from "../types";

const STATS: { key: keyof PlayerStatistics; label: string; kind: "count" | "percent" }[] = [
  { key: "aces", label: "Aces", kind: "count" },
  { key: "doubleFaults", label: "Double faults", kind: "count" },
  { key: "servicePointsWonPercent", label: "Service points won", kind: "percent" },
  { key: "returnPointsWonPercent", label: "Return points won", kind: "percent" },
  { key: "breakPointsWonPercent", label: "Break points won", kind: "percent" },
  { key: "breakPointsSavedPercent", label: "Break points saved", kind: "percent" },
  { key: "firstServePercent", label: "First serve", kind: "percent" },
  { key: "firstServePointsWonPercent", label: "First-serve points won", kind: "percent" },
  { key: "secondServePointsWonPercent", label: "Second-serve points won", kind: "percent" },
  { key: "breakPointsFaced", label: "Break points faced", kind: "count" },
  { key: "breakPointsSaved", label: "Break points saved (count)", kind: "count" },
  { key: "breakPointChances", label: "Break-point chances", kind: "count" },
  { key: "breakPointsConverted", label: "Break points converted", kind: "count" },
  { key: "winners", label: "Winners", kind: "count" },
  { key: "unforcedErrors", label: "Unforced errors", kind: "count" },
  { key: "netPointsWon", label: "Net points won", kind: "count" },
  { key: "totalPointsWon", label: "Total points won", kind: "count" },
];

const CONDITIONS: { key: keyof MatchConditions; label: string }[] = [
  { key: "durationMinutes", label: "Duration" },
  { key: "actualStart", label: "Actual start" },
  { key: "completedAt", label: "Completion" },
  { key: "roof", label: "Roof" },
  { key: "venue", label: "Venue" },
  { key: "interruptions", label: "Interruptions" },
];

export function MatchDetailPage() {
  const { id = "" } = useParams();
  const [showRaw, setShowRaw] = useState(false);
  const query = useQuery({ queryKey: ["match", id], queryFn: () => matchQuery(id) });
  const match = query.data;
  const home = match?.players.find((player) => player.side === "home");
  const away = match?.players.find((player) => player.side === "away");

  return (
    <QueryState isPending={query.isPending} error={query.error}>
      {match ? (
        <>
          <Overview match={match} home={home} away={away} />
          <Statistics home={home} away={away} />
          <Sets match={match} home={home} away={away} />
          <PointByPoint match={match} />
          <section className="panel">
            <h2>Source record</h2>
            <p className="muted">Original season-file cells. Parsed statistics and set scores are stored separately.</p>
            <button type="button" onClick={() => setShowRaw((value) => !value)}>
              {showRaw ? "Hide raw record" : "Show raw record"}
            </button>
            {showRaw ? <pre className="raw">{match.rawRecord ?? "No raw record."}</pre> : null}
          </section>
        </>
      ) : null}
    </QueryState>
  );
}

function Overview({ match, home, away }: { match: MatchRow; home: MatchPlayer | undefined; away: MatchPlayer | undefined }) {
  const conditions = match.conditions;
  return (
    <>
      <div className="detail-head">
        <div>
          <h2>
            <PlayerName player={home} fallback="Home" /> vs <PlayerName player={away} fallback="Away" />
          </h2>
          <p className="muted">
            Winner {winnerName(match, home, away)}. Score {dash(match.score)}.
          </p>
        </div>
      </div>
      <section className="cards">
        <article className="card">
          Date<strong>{dash(match.playedOn)}</strong>
        </article>
        <article className="card">
          Tournament
          <strong>
            {match.tournamentId ? <Link to={`/tournaments/${match.tournamentId}`}>{dash(match.tournamentName)}</Link> : dash(match.tournamentName)}
          </strong>
        </article>
        <article className="card">
          Round<strong>{dash(match.round)}</strong>
        </article>
        <article className="card">
          Surface<strong>{dash(match.surface)}</strong>
        </article>
        <article className="card">
          Level<strong>{dash(match.level)}</strong>
        </article>
        <article className="card">
          Status<strong>{dash(statusLabel(match.status, match.statusExtra))}</strong>
        </article>
      </section>
      <section className="split">
        <SideCard player={home} title="Home" />
        <SideCard player={away} title="Away" />
      </section>
      <section className="panel">
        <h2>Conditions</h2>
        <p className="muted">Duration, actual start, completion, roof, venue, and interruptions are unavailable in the stored season file.</p>
        <div className="conditions">
          {CONDITIONS.map((item) => (
            <p key={item.key}>
              <span className="muted">{item.label}</span>
              <br />
              {conditionText(conditions, item.key)}
            </p>
          ))}
        </div>
      </section>
    </>
  );
}

function SideCard({ player, title }: { player: MatchPlayer | undefined; title: string }) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {player?.playerId && player.name ? <Link to={`/players/${player.playerId}`}>{player.name}</Link> : <p>{player?.name ?? "Player not linked"}</p>}
      <p className="muted">Result {player?.isWinner === null || player?.isWinner === undefined ? "—" : player.isWinner ? "Win" : "Loss"}</p>
      <p className="muted">Ranking {dash(player?.ranking)}</p>
      <p className="muted">Ranking points {dash(player?.rankingPoints)}</p>
    </section>
  );
}

function Statistics({ home, away }: { home: MatchPlayer | undefined; away: MatchPlayer | undefined }) {
  const homeStats = home?.playerStatistics ?? null;
  const awayStats = away?.playerStatistics ?? null;
  const rows = STATS.filter((row) => stored(homeStats?.[row.key]) || stored(awayStats?.[row.key]));
  return (
    <section className="panel">
      <h2>Statistics</h2>
      <p className="muted">Stored match statistics only. Percentages are percentage points: 80 means 80%.</p>
      {rows.length === 0 ? (
        <p className="muted">No match statistics are stored for this match.</p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Statistic</th>
                <th>{sideHeading(home, "Home")}</th>
                <th>{sideHeading(away, "Away")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.key}>
                  <td>{row.label}</td>
                  <td>{statCell(homeStats?.[row.key], row.kind)}</td>
                  <td>{statCell(awayStats?.[row.key], row.kind)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function Sets({ match, home, away }: { match: MatchRow; home: MatchPlayer | undefined; away: MatchPlayer | undefined }) {
  const sets = match.sets ?? [];
  const showTiebreak = sets.some((set) => stored(set.homeTiebreak) || stored(set.awayTiebreak));
  return (
    <section className="panel">
      <h2>Sets</h2>
      <p className="muted">Games won by each player. Tiebreak points appear when the season file stored them.</p>
      {sets.length === 0 ? (
        <p className="muted">No set scores are stored for this match.</p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Set</th>
                <th>{sideHeading(home, "Home")}</th>
                <th>{sideHeading(away, "Away")}</th>
                {showTiebreak ? <th>Tiebreak</th> : null}
              </tr>
            </thead>
            <tbody>
              {sets.map((set) => (
                <tr key={set.setNumber}>
                  <td>{set.setNumber}</td>
                  <td>{dash(set.homeGames)}</td>
                  <td>{dash(set.awayGames)}</td>
                  {showTiebreak ? <td>{tiebreak(set)}</td> : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function PointByPoint({ match }: { match: MatchRow }) {
  const state = match.pointByPoint?.state ?? "Unavailable";
  const message =
    match.pointByPoint?.message ??
    "Point by point is unavailable. The season file has no game or point sequence, so this match has no stored games or points.";
  return (
    <section className="panel">
      <h2>Point by Point</h2>
      <div className="notice">
        <strong>{state}</strong>
        <p>{message}</p>
      </div>
    </section>
  );
}

function PlayerName({ player, fallback }: { player: MatchPlayer | undefined; fallback: string }) {
  if (player?.playerId && player.name) {
    return <Link to={`/players/${player.playerId}`}>{player.name}</Link>;
  }
  return <>{player?.name ?? fallback}</>;
}

function winnerName(match: MatchRow, home: MatchPlayer | undefined, away: MatchPlayer | undefined) {
  const winner = match.players.find((player) => player.isWinner);
  if (winner?.name) {
    return winner.playerId ? <Link to={`/players/${winner.playerId}`}>{winner.name}</Link> : winner.name;
  }
  if (match.winnerCode === 1) {
    return sideHeading(home, "Home");
  }
  if (match.winnerCode === 2) {
    return sideHeading(away, "Away");
  }
  return "—";
}

function sideHeading(player: MatchPlayer | undefined, fallback: string) {
  return player?.name ?? fallback;
}

function statusLabel(status: string | null, extra: string | null) {
  if (!status && !extra) {
    return null;
  }
  if (status && extra && status.toLowerCase() !== extra.toLowerCase()) {
    return `${status} (${extra})`;
  }
  return status ?? extra;
}

function conditionText(conditions: MatchConditions | null, key: keyof MatchConditions) {
  const value = conditions?.[key];
  if (key === "durationMinutes" && typeof value === "number") {
    return `${value} min`;
  }
  return dash(value);
}

function stored(value: number | null | undefined) {
  return value !== null && value !== undefined;
}

function statCell(value: number | null | undefined, kind: "count" | "percent") {
  if (!stored(value)) {
    return "—";
  }
  if (kind === "percent") {
    const width = Math.max(0, Math.min(100, value as number));
    return (
      <span className="stat-percent">
        <span className="bar" aria-hidden="true">
          <span style={{ width: `${width}%` }} />
        </span>
        <span>{formatPercent(value as number)}</span>
      </span>
    );
  }
  return String(value);
}

function formatPercent(value: number) {
  const rounded = Math.round(value * 1000) / 1000;
  return `${rounded}%`;
}

function tiebreak(set: MatchSetScore) {
  if (!stored(set.homeTiebreak) && !stored(set.awayTiebreak)) {
    return "—";
  }
  return `${set.homeTiebreak ?? "—"}–${set.awayTiebreak ?? "—"}`;
}
