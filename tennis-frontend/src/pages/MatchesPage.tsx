import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { matchesQuery } from "../api";
import { EmptyNote, Pager, QueryState, dash } from "../components/QueryState";

const fields = [
  ["year", "Year"],
  ["from", "From"],
  ["to", "To"],
  ["playerId", "Player id"],
  ["tournamentId", "Tournament id"],
  ["surface", "Surface"],
  ["level", "Level"],
] as const;

export function MatchesPage() {
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [filters, setFilters] = useState<Record<string, string>>({});
  const query = useQuery({ queryKey: ["matches", page, filters], queryFn: () => matchesQuery(filters, page) });

  return (
    <>
      <form
        className="filters"
        onSubmit={(event) => {
          event.preventDefault();
          setPage(1);
          setFilters(draft);
        }}
      >
        {fields.map(([key, label]) => (
          <input
            key={key}
            aria-label={label}
            placeholder={label}
            value={draft[key] ?? ""}
            onChange={(event) => setDraft({ ...draft, [key]: event.target.value })}
          />
        ))}
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
                        <Link to={`/matches/${match.id}`}>{playerLabel(match.players)}</Link>
                      </td>
                      <td>{winnerLabel(match.winnerCode, match.players)}</td>
                      <td>{dash(match.tournamentName)}</td>
                      <td>{dash(match.round)}</td>
                      <td>{dash(match.surface)}</td>
                      <td>{dash(match.score)}</td>
                      <td>{dash(match.playedOn)}</td>
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

function playerLabel(players: { side: string; name: string | null }[]) {
  if (players.length === 0) {
    return "Sides not stored";
  }
  return players.map((player) => player.name ?? player.side).join(" vs ");
}

function winnerLabel(code: number | null, players: { side: string; name: string | null; isWinner: boolean | null }[]) {
  const winner = players.find((player) => player.isWinner);
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
