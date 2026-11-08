import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { playersQuery } from "../api";
import { EmptyNote, Pager, QueryState, dash } from "../components/QueryState";

export function PlayersPage() {
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState("");
  const [name, setName] = useState("");
  const query = useQuery({ queryKey: ["players", page, name], queryFn: () => playersQuery(page, name) });

  return (
    <>
      <form
        className="filters"
        onSubmit={(event) => {
          event.preventDefault();
          setPage(1);
          setName(draft);
        }}
      >
        <input value={draft} onChange={(event) => setDraft(event.target.value)} placeholder="Search name" aria-label="Search name" />
        <button type="submit">Search</button>
      </form>
      <QueryState isPending={query.isPending} error={query.error}>
        {query.data && query.data.totalCount === 0 ? (
          <EmptyNote>
            <p>No players are stored.</p>
            <p className="muted">Player id and name columns have not been read from a season file, so rows are not created from a guessed name.</p>
          </EmptyNote>
        ) : query.data ? (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Nationality</th>
                    <th>Matches</th>
                    <th>Wins</th>
                    <th>Losses</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((player) => (
                    <tr key={player.id}>
                      <td>
                        <Link to={`/players/${player.id}`}>{player.name}</Link>
                      </td>
                      <td>{dash(player.nationality)}</td>
                      <td>{player.matchCount}</td>
                      <td>{player.wins}</td>
                      <td>{player.losses}</td>
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
