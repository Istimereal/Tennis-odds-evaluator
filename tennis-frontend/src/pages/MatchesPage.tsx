import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { matchesQuery } from "../api";
import { PlayerSelect } from "../components/PlayerSelect";
import { EmptyNote, Pager, QueryState, dash } from "../components/QueryState";
import type { MatchPlayer, Player } from "../types";

const emptyDraft = {
  year: "",
  from: "",
  to: "",
  tournamentId: "",
  surface: "",
  level: "",
};

export function MatchesPage() {
  const [page, setPage] = useState(1);
  const [player, setPlayer] = useState<Player | null>(null);
  const [draft, setDraft] = useState(emptyDraft);
  const [filters, setFilters] = useState<Record<string, string>>({});
  const query = useQuery({ queryKey: ["matches", page, filters], queryFn: () => matchesQuery(filters, page) });

  return (
    <>
      <form
        className="filters"
        onSubmit={(event) => {
          event.preventDefault();
          setPage(1);
          const next: Record<string, string> = { ...draft };
          if (player) {
            next.playerId = player.id;
          }
          setFilters(next);
        }}
      >
        <PlayerSelect label="Player name" selected={player} onSelect={setPlayer} />
        <input aria-label="Year" placeholder="Year" value={draft.year} onChange={(event) => setDraft({ ...draft, year: event.target.value })} />
        <input aria-label="From" type="date" value={draft.from} onChange={(event) => setDraft({ ...draft, from: event.target.value })} />
        <input aria-label="To" type="date" value={draft.to} onChange={(event) => setDraft({ ...draft, to: event.target.value })} />
        <input
          aria-label="Tournament id"
          placeholder="Tournament id"
          value={draft.tournamentId}
          onChange={(event) => setDraft({ ...draft, tournamentId: event.target.value })}
        />
        <input aria-label="Surface" placeholder="Surface" value={draft.surface} onChange={(event) => setDraft({ ...draft, surface: event.target.value })} />
        <input aria-label="Level" placeholder="Level" value={draft.level} onChange={(event) => setDraft({ ...draft, level: event.target.value })} />
        <button type="submit">Filter</button>
      </form>
      <QueryState isPending={query.isPending} error={query.error}>
        {query.data && query.data.totalCount === 0 ? (
          <EmptyNote>No matches match this filter. An empty database has no fabricated rows.</EmptyNote>
        ) : query.data ? (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Match</th>
                    <th>Players</th>
                    <th>Winner</th>
                    <th>Tournament</th>
                    <th>Round</th>
                    <th>Surface</th>
                    <th>Score</th>
                    <th>Match date</th>
                    <th>Stats</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((match) => (
                    <tr key={match.id}>
                      <td>
                        <Link to={`/matches/${match.id}`}>Details</Link>
                      </td>
                      <td>
                        <PlayersCell players={match.players} />
                      </td>
                      <td>{winnerLabel(match.winnerCode, match.players)}</td>
                      <td>{dash(match.tournamentName)}</td>
                      <td>{dash(match.round)}</td>
                      <td>{dash(match.surface)}</td>
                      <td>{dash(match.score)}</td>
                      <td>
                        <Link to={`/matches/${match.id}`}>{dash(match.playedOn)}</Link>
                      </td>
                      <td>{match.statCompleteness === null ? "—" : `${Math.round(match.statCompleteness * 100)}%`}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pager page={query.data.page} pageSize={query.data.pageSize} total={query.data.totalCount} onPage={setPage} />
          </>
        ) : null}
      </QueryState>
    </>
  );
}

function PlayersCell({ players }: { players: MatchPlayer[] }) {
  if (players.length === 0) {
    return <>Sides not stored</>;
  }
  return (
    <>
      {players.map((player, index) => (
        <span key={`${player.side}-${player.playerId ?? index}`}>
          {index > 0 ? " vs " : null}
          {player.playerId ? <Link to={`/players/${player.playerId}`}>{player.name ?? "Unavailable"}</Link> : (player.name ?? "Unavailable")}
        </span>
      ))}
    </>
  );
}

function winnerLabel(code: number | null, players: MatchPlayer[]) {
  const winner = players.find((player) => player.isWinner);
  if (winner?.playerId && winner.name) {
    return <Link to={`/players/${winner.playerId}`}>{winner.name}</Link>;
  }
  if (winner?.name) {
    return winner.name;
  }
  if (code === 1) {
    return "Home";
  }
  if (code === 2) {
    return "Away";
  }
  return "—";
}
