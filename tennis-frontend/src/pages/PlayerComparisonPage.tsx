import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { playerComparisonQuery, playerQuery } from "../api";
import { PlayerSelect } from "../components/PlayerSelect";
import { QueryState } from "../components/QueryState";
import type { Player } from "../types";

const technicalStatuses = new Set(["DATABASE_ERROR", "CALCULATION_ERROR", "PLAYER_NOT_FOUND", "INVALID_PLAYER_SELECTION"]);

export function PlayerComparisonPage() {
  const [params] = useSearchParams();
  const presetA = params.get("playerA") ?? "";
  const presetB = params.get("playerB") ?? "";
  const [playerA, setPlayerA] = useState<Player | null>(null);
  const [playerB, setPlayerB] = useState<Player | null>(null);
  const [pair, setPair] = useState<{ a: string; b: string } | null>(null);
  const appliedA = useRef<string | null>(null);
  const appliedB = useRef<string | null>(null);
  const presetAQuery = useQuery({
    queryKey: ["player", presetA],
    queryFn: () => playerQuery(presetA),
    enabled: presetA.length > 0,
  });
  const presetBQuery = useQuery({
    queryKey: ["player", presetB],
    queryFn: () => playerQuery(presetB),
    enabled: presetB.length > 0,
  });

  useEffect(() => {
    if (presetAQuery.data && appliedA.current !== presetA) {
      appliedA.current = presetA;
      setPlayerA(presetAQuery.data);
    }
  }, [presetA, presetAQuery.data]);

  useEffect(() => {
    if (presetBQuery.data && appliedB.current !== presetB) {
      appliedB.current = presetB;
      setPlayerB(presetBQuery.data);
    }
  }, [presetB, presetBQuery.data]);

  useEffect(() => {
    setPair(null);
  }, [playerA?.id, playerB?.id]);

  const samePlayer = playerA !== null && playerB !== null && playerA.id === playerB.id;
  const canCompare = playerA !== null && playerB !== null && !samePlayer;
  const comparison = useQuery({
    queryKey: ["player-comparison", pair?.a, pair?.b],
    queryFn: () => playerComparisonQuery(pair!.a, pair!.b),
    enabled: pair !== null && pair.a !== pair.b,
  });

  return (
    <>
      <section className="split">
        <section className="panel">
          <h2>Player A</h2>
          <PlayerSelect label="Search player A by name" selected={playerA} onSelect={setPlayerA} />
          {presetAQuery.isError ? <p className="error">Player A could not be loaded.</p> : null}
          {playerA ? <PlayerFacts player={playerA} /> : <p className="muted">No player selected.</p>}
        </section>
        <section className="panel">
          <h2>Player B</h2>
          <PlayerSelect label="Search player B by name" selected={playerB} onSelect={setPlayerB} />
          {presetBQuery.isError ? <p className="error">Player B could not be loaded.</p> : null}
          {playerB ? <PlayerFacts player={playerB} /> : <p className="muted">No player selected.</p>}
        </section>
      </section>
      <div className="compare-actions">
        <button type="button" disabled={!canCompare} onClick={() => playerA && playerB && setPair({ a: playerA.id, b: playerB.id })}>
          Compare
        </button>
        {samePlayer ? <p className="error">Select two different players.</p> : null}
      </div>
      {pair && !samePlayer ? (
        <QueryState isPending={comparison.isPending} error={comparison.error}>
          {comparison.data ? <ComparisonResult data={comparison.data} /> : null}
        </QueryState>
      ) : null}
    </>
  );
}

function ComparisonResult({ data }: { data: NonNullable<Awaited<ReturnType<typeof playerComparisonQuery>>> }) {
  const odds = data.odds;
  const ready = data.status === "OK";
  return (
    <>
      <section className="split">
        <PlayerCard title="Player A" player={data.playerA} />
        <PlayerCard title="Player B" player={data.playerB} />
      </section>
      <section className="panel">
        <h2>Head-to-head</h2>
        <Outcome status={data.status} message={data.message} />
        {ready ? (
          <p>
            Total matches {data.totalMatches}. {data.playerA?.name ?? "Player A"} wins {data.playerAWins} ({percent(data.playerAWinPercentage)}).{" "}
            {data.playerB?.name ?? "Player B"} wins {data.playerBWins} ({percent(data.playerBWinPercentage)}).
          </p>
        ) : data.status === "INSUFFICIENT_DATA" ? (
          <p className="muted">Win totals stay blank until completed meetings can be counted.</p>
        ) : null}
      </section>
      <section className="panel">
        <h2>Previous matches</h2>
        {technicalStatuses.has(data.status) ? (
          <p className="error" role="alert">
            {data.message}
          </p>
        ) : data.meetings.length === 0 ? (
          data.status === "NO_HEAD_TO_HEAD" ? (
            <div className="notice" role="status">
              <strong>No head-to-head</strong>
              <p>No matching possible</p>
            </div>
          ) : (
            <p className="muted">No completed meetings to list.</p>
          )
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Time</th>
                  <th>Tournament</th>
                  <th>Surface</th>
                  <th>Round</th>
                  <th>Venue</th>
                  <th>Winner</th>
                  <th>Score</th>
                </tr>
              </thead>
              <tbody>
                {data.meetings.map((meeting) => (
                  <tr key={meeting.sourceKey || meeting.matchId}>
                    <td>
                      <Link to={`/matches/${meeting.matchId}`}>{unavailable(meeting.date)}</Link>
                    </td>
                    <td>{unavailable(meeting.time)}</td>
                    <td>{unavailable(meeting.tournament)}</td>
                    <td>{unavailable(meeting.surface)}</td>
                    <td>{unavailable(meeting.round)}</td>
                    <td>{unavailable(meeting.venue)}</td>
                    <td>
                      {meeting.winnerId && meeting.winnerName ? (
                        <Link to={`/players/${meeting.winnerId}`}>{meeting.winnerName}</Link>
                      ) : (
                        unavailable(meeting.winnerName)
                      )}
                    </td>
                    <td>{unavailable(meeting.score)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <section className="panel">
        <h2>Odds Generator</h2>
        <p className="muted">These are model-generated fair odds, not bookmaker offers.</p>
        <p>Method {odds.method}.</p>
        <Outcome status={odds.status} message={odds.message} />
        {odds.status === "OK" ? (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Player</th>
                  <th>Probability</th>
                  <th>Fair decimal odds</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>{odds.playerAName ?? "Player A"}</td>
                  <td>{probability(odds.playerAProbability)}</td>
                  <td>{decimalOdds(odds.playerADecimalOdds)}</td>
                </tr>
                <tr>
                  <td>{odds.playerBName ?? "Player B"}</td>
                  <td>{probability(odds.playerBProbability)}</td>
                  <td>{decimalOdds(odds.playerBDecimalOdds)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        ) : null}
        <p className="muted">
          Match context {odds.matchContext ? "is attached to this request." : "is unavailable. This is a generic comparison, not an upcoming match."}
        </p>
      </section>
    </>
  );
}

function Outcome({ status, message }: { status: string; message: string }) {
  if (status === "NO_HEAD_TO_HEAD") {
    return (
      <div className="notice" role="status">
        <strong>No head-to-head</strong>
        <p>No matching possible</p>
      </div>
    );
  }
  if (technicalStatuses.has(status)) {
    return (
      <p className="error" role="alert">
        {message}
      </p>
    );
  }
  if (status === "INSUFFICIENT_DATA") {
    return <p className="warning">{message}</p>;
  }
  return <p>{message}</p>;
}

function PlayerCard({ title, player }: { title: string; player: PlayerComparisonPlayer | null }) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {player ? <PlayerFacts player={player} /> : <p>Unavailable</p>}
    </section>
  );
}

type PlayerComparisonPlayer = {
  id: string;
  name: string;
  sourceId: string | null;
  nationality: string | null;
  matchCount: number;
  wins: number;
  losses: number;
};

function PlayerFacts({ player }: { player: PlayerComparisonPlayer }) {
  return (
    <p>
      <Link to={`/players/${player.id}`}>{player.name}</Link>. Source id {unavailable(player.sourceId)}. Nationality {unavailable(player.nationality)}. Matches{" "}
      {player.matchCount}. Wins {player.wins}. Losses {player.losses}.
    </p>
  );
}

function unavailable(value: string | number | null | undefined) {
  if (value === null || value === undefined || value === "") {
    return "Unavailable";
  }
  return String(value);
}

function percent(value: number | null) {
  if (value === null) {
    return "Unavailable";
  }
  return `${value.toFixed(2)}%`;
}

function probability(value: number | null) {
  if (value === null) {
    return "Unavailable";
  }
  return `${(value * 100).toFixed(2)}%`;
}

function decimalOdds(value: number | null) {
  if (value === null) {
    return "Unavailable";
  }
  return value.toFixed(4);
}
