import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { matchQuery } from "../api";
import type { MatchPlayer } from "../types";
import { QueryState, dash } from "../components/QueryState";

export function MatchDetailPage() {
  const { id = "" } = useParams();
  const [showRaw, setShowRaw] = useState(false);
  const query = useQuery({ queryKey: ["match", id], queryFn: () => matchQuery(id) });
  const match = query.data;
  const home = match?.players.find((player) => player.side === "home");
  const away = match?.players.find((player) => player.side === "away");
  const homeStats = parseObject(home?.statistics);
  const awayStats = parseObject(away?.statistics);
  const keys = [...new Set([...Object.keys(homeStats), ...Object.keys(awayStats)])].sort();
  const serveKeys = keys.filter((key) => key.toLowerCase().includes("serve"));
  const breakKeys = keys.filter((key) => key.toLowerCase().includes("break"));

  return (
    <QueryState isPending={query.isPending} error={query.error}>
      {match ? (
        <>
          <section className="panel">
            <h2>General</h2>
            <p>
              Season {match.sourceYear}. Match date {dash(match.playedOn)}. Tournament {dash(match.tournamentName)}. Round {dash(match.round)}. Surface{" "}
              {dash(match.surface)}. Level {dash(match.level)}. Score {dash(match.score)}. Winner {winnerText(match.winnerCode)}.
            </p>
            <p className="muted">Source key {match.sourceKey}. Completeness {match.statCompleteness === null ? "no statistic columns" : `${Math.round(match.statCompleteness * 100)}%`}.</p>
          </section>
          <section className="split">
            <SideCard player={home} title="Home" />
            <SideCard player={away} title="Away" />
          </section>
          <StatBlock title="Serve and return" keys={serveKeys.length > 0 ? serveKeys : keys} home={homeStats} away={awayStats} empty="No serve or return columns are stored on this match." />
          <StatBlock title="Breakpoints" keys={breakKeys} home={homeStats} away={awayStats} empty="No breakpoint columns are stored on this match." />
          <section className="panel">
            <h2>Rankings at match time</h2>
            <p>Home {dash(home?.ranking)}. Away {dash(away?.ranking)}. Ranking is stored on the match side, not on the player.</p>
            <h2>Provenance</h2>
            <p className="muted">Odds JSON is kept per side and stays empty until a published odds column is read.</p>
          </section>
          <section className="panel">
            <button type="button" onClick={() => setShowRaw((value) => !value)}>
              {showRaw ? "Hide raw JSON" : "Show raw JSON"}
            </button>
            {showRaw ? <pre className="raw">{match.rawRecord ?? "No raw record."}</pre> : null}
          </section>
        </>
      ) : null}
    </QueryState>
  );
}

function SideCard({ player, title }: { player: MatchPlayer | undefined; title: string }) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {player?.playerId && player.name ? <Link to={`/players/${player.playerId}`}>{player.name}</Link> : <p>{player?.name ?? "Player not linked"}</p>}
      <p className="muted">Winner {player?.isWinner === null || player?.isWinner === undefined ? "—" : player.isWinner ? "yes" : "no"}</p>
    </section>
  );
}

function StatBlock({
  title,
  keys,
  home,
  away,
  empty,
}: {
  title: string;
  keys: string[];
  home: Record<string, unknown>;
  away: Record<string, unknown>;
  empty: string;
}) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {keys.length === 0 ? (
        <p className="muted">{empty}</p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Column</th>
                <th>Home</th>
                <th>Away</th>
              </tr>
            </thead>
            <tbody>
              {keys.map((key) => (
                <tr key={key}>
                  <td>{key}</td>
                  <td>{dash(formatCell(home[key]))}</td>
                  <td>{dash(formatCell(away[key]))}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function parseObject(value: string | undefined): Record<string, unknown> {
  if (!value) {
    return {};
  }
  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function formatCell(value: unknown) {
  if (value === null || value === undefined) {
    return null;
  }
  return typeof value === "string" ? value : JSON.stringify(value);
}

function winnerText(code: number | null) {
  if (code === 1) {
    return "home";
  }
  if (code === 2) {
    return "away";
  }
  return "—";
}
