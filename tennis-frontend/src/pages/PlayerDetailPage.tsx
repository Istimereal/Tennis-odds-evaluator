import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { playerMatchesQuery, playerQuery } from "../api";
import { Pager, QueryState, dash } from "../components/QueryState";

export function PlayerDetailPage() {
  const { id = "" } = useParams();
  const [page, setPage] = useState(1);
  useEffect(() => {
    setPage(1);
  }, [id]);
  const query = useQuery({ queryKey: ["player", id], queryFn: () => playerQuery(id) });
  const history = useQuery({
    queryKey: ["player-matches", id, page],
    queryFn: () => playerMatchesQuery(id, page),
    enabled: id.length > 0,
  });

  return (
    <QueryState isPending={query.isPending} error={query.error}>
      {query.data ? (
        <>
          <div className="detail-head">
            <div>
              <h2>{query.data.name}</h2>
              <CopyId id={query.data.id} />
            </div>
            <Link className="button-link" to={`/compare?playerA=${query.data.id}`}>
              Compare with another player
            </Link>
          </div>
          <section className="cards">
            <article className="card">
              Name<strong>{query.data.name}</strong>
            </article>
            <article className="card">
              Nationality<strong>{dash(query.data.nationality)}</strong>
            </article>
            <article className="card">
              Record<strong>
                {query.data.wins}–{query.data.losses}
              </strong>
            </article>
            <article className="card">
              Matches<strong>{query.data.matchCount}</strong>
            </article>
          </section>
          <section className="panel">
            <h2>Surface records</h2>
            {query.data.surfaceRecords.length === 0 ? (
              <p className="muted">No linked matches. Surface stays unmapped until a season column is read.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Surface</th>
                      <th>Wins</th>
                      <th>Losses</th>
                    </tr>
                  </thead>
                  <tbody>
                    {query.data.surfaceRecords.map((row) => (
                      <tr key={row.surface}>
                        <td>{row.surface}</td>
                        <td>{row.wins}</td>
                        <td>{row.losses}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
          <section className="panel">
            <h2>Matches</h2>
            <p className="muted">Newest first. Ten matches per page.</p>
            <QueryState isPending={history.isPending} error={history.error}>
              {history.data && history.data.totalCount === 0 ? (
                <p className="muted">No matches are stored for this player.</p>
              ) : history.data ? (
                <>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Opponent</th>
                          <th>Date</th>
                          <th>Tournament</th>
                          <th>Round</th>
                          <th>Surface</th>
                          <th>Score</th>
                          <th>Result</th>
                        </tr>
                      </thead>
                      <tbody>
                        {history.data.items.map((match) => (
                          <tr key={match.id}>
                            <td>
                              {match.opponentId ? (
                                <Link to={`/players/${match.opponentId}`}>{match.opponentName ?? "Unavailable"}</Link>
                              ) : (
                                unavailable(match.opponentName)
                              )}
                            </td>
                            <td>
                              <Link to={`/matches/${match.id}`}>{unavailable(match.playedOn)}</Link>
                            </td>
                            <td>{unavailable(match.tournamentName)}</td>
                            <td>{unavailable(match.round)}</td>
                            <td>{unavailable(match.surface)}</td>
                            <td>{unavailable(match.score)}</td>
                            <td>{resultLabel(match.isWinner)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  <Pager page={history.data.page} pageSize={history.data.pageSize} total={history.data.totalCount} onPage={setPage} />
                </>
              ) : null}
            </QueryState>
          </section>
        </>
      ) : null}
    </QueryState>
  );
}

function CopyId({ id }: { id: string }) {
  const [copied, setCopied] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  return (
    <label className="id-copy">
      <span className="muted">Internal id</span>
      <input ref={input} readOnly value={id} aria-label="Internal player id" spellCheck={false} onFocus={(event) => event.currentTarget.select()} />
      <button
        type="button"
        onClick={() => {
          if (navigator.clipboard?.writeText) {
            void navigator.clipboard.writeText(id).then(
              () => setCopied(true),
              () => input.current?.select(),
            );
            return;
          }
          input.current?.select();
        }}
      >
        {copied ? "Copied" : "Copy"}
      </button>
    </label>
  );
}

function unavailable(value: string | null | undefined) {
  if (value === null || value === undefined || value === "") {
    return "Unavailable";
  }
  return value;
}

function resultLabel(isWinner: boolean | null) {
  if (isWinner === null) {
    return "Unavailable";
  }
  return isWinner ? "Win" : "Loss";
}
