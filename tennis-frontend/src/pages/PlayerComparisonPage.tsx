import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { playerComparisonQuery, playersQuery } from "../api";
import { QueryState } from "../components/QueryState";
import type { Player } from "../types";

export function PlayerComparisonPage() {
  const [playerA, setPlayerA] = useState<Player | null>(null);
  const [playerB, setPlayerB] = useState<Player | null>(null);
  const samePlayer = playerA !== null && playerB !== null && playerA.id === playerB.id;
  const comparison = useQuery({
    queryKey: ["player-comparison", playerA?.id, playerB?.id],
    queryFn: () => playerComparisonQuery(playerA!.id, playerB!.id),
    enabled: playerA !== null && playerB !== null && !samePlayer,
  });

  return (
    <>
      <section className="split">
        <PlayerSearch label="Player A" selected={playerA} onSelect={setPlayerA} />
        <PlayerSearch label="Player B" selected={playerB} onSelect={setPlayerB} />
      </section>
      {samePlayer ? (
        <section className="panel">
          <p className="error">Select two different players.</p>
        </section>
      ) : null}
      {playerA && playerB && !samePlayer ? (
        <QueryState isPending={comparison.isPending} error={comparison.error}>
          {comparison.data ? <ComparisonResult data={comparison.data} /> : null}
        </QueryState>
      ) : null}
    </>
  );
}

function PlayerSearch({ label, selected, onSelect }: { label: string; selected: Player | null; onSelect: (player: Player) => void }) {
  const [draft, setDraft] = useState("");
  const [name, setName] = useState("");
  const query = useQuery({
    queryKey: ["player-search", label, name],
    queryFn: () => playersQuery(1, name),
    enabled: name.trim().length > 0,
  });

  return (
    <section className="panel">
      <h2>{label}</h2>
      <form
        className="filters"
        onSubmit={(event) => {
          event.preventDefault();
          setName(draft);
        }}
      >
        <input value={draft} onChange={(event) => setDraft(event.target.value)} placeholder="Search name" aria-label={`${label} search`} />
        <button type="submit">Search</button>
      </form>
      {selected ? (
        <PlayerFacts
          name={selected.name}
          sourceId={selected.sourceId}
          nationality={selected.nationality}
          matchCount={selected.matchCount}
          wins={selected.wins}
          losses={selected.losses}
        />
      ) : (
        <p className="muted">No player selected.</p>
      )}
      {query.isPending ? <p className="muted">Searching…</p> : null}
      {query.data && query.data.items.length === 0 ? <p className="muted">No players match that name.</p> : null}
      {query.data && query.data.items.length > 0 ? (
        <ul className="search-results">
          {query.data.items.map((player) => (
            <li key={player.id}>
              <button type="button" onClick={() => onSelect(player)}>
                {player.name}
              </button>
              <span className="muted"> {unavailable(player.sourceId)}</span>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
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
        {data.status === "NO_HEAD_TO_HEAD" ? <p>No matching possible</p> : <p>{data.message}</p>}
        {ready ? (
          <p>
            Total matches {data.totalMatches}. {data.playerA?.name ?? "Player A"} wins {data.playerAWins} ({percent(data.playerAWinPercentage)}).{" "}
            {data.playerB?.name ?? "Player B"} wins {data.playerBWins} ({percent(data.playerBWinPercentage)}).
          </p>
        ) : (
          <p className="muted">Win totals stay blank until completed meetings can be counted.</p>
        )}
      </section>
      <section className="panel">
        <h2>Previous matches</h2>
        {data.meetings.length === 0 ? (
          <p className="muted">{data.status === "NO_HEAD_TO_HEAD" ? "No matching possible" : "No completed meetings to list."}</p>
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
                    <td>{unavailable(meeting.date)}</td>
                    <td>{unavailable(meeting.time)}</td>
                    <td>{unavailable(meeting.tournament)}</td>
                    <td>{unavailable(meeting.surface)}</td>
                    <td>{unavailable(meeting.round)}</td>
                    <td>{unavailable(meeting.venue)}</td>
                    <td>{unavailable(meeting.winnerName)}</td>
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
        {odds.status === "NO_HEAD_TO_HEAD" ? <p>No matching possible</p> : <p>{odds.message}</p>}
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

function PlayerCard({ title, player }: { title: string; player: PlayerComparisonPlayer | null }) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {player ? (
        <PlayerFacts
          name={player.name}
          sourceId={player.sourceId}
          nationality={player.nationality}
          matchCount={player.matchCount}
          wins={player.wins}
          losses={player.losses}
        />
      ) : (
        <p>Unavailable</p>
      )}
    </section>
  );
}

type PlayerComparisonPlayer = {
  name: string;
  sourceId: string | null;
  nationality: string | null;
  matchCount: number;
  wins: number;
  losses: number;
};

function PlayerFacts(player: PlayerComparisonPlayer) {
  return (
    <p>
      {player.name}. Source id {unavailable(player.sourceId)}. Nationality {unavailable(player.nationality)}. Matches {player.matchCount}. Wins {player.wins}. Losses{" "}
      {player.losses}.
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
