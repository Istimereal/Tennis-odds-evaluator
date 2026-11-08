import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { playerQuery } from "../api";
import { QueryState, dash } from "../components/QueryState";

export function PlayerDetailPage() {
  const { id = "" } = useParams();
  const query = useQuery({ queryKey: ["player", id], queryFn: () => playerQuery(id) });

  return (
    <QueryState isPending={query.isPending} error={query.error}>
      {query.data ? (
        <>
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
            <h2>Recent matches</h2>
            {query.data.recentMatches.length === 0 ? (
              <p className="muted">No recent matches.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Season</th>
                      <th>Match date</th>
                      <th>Tournament start</th>
                      <th>Tournament</th>
                      <th>Surface</th>
                      <th>Result</th>
                      <th>Score</th>
                    </tr>
                  </thead>
                  <tbody>
                    {query.data.recentMatches.map((match) => (
                      <tr key={match.id}>
                        <td>{match.sourceYear}</td>
                        <td>{dash(match.matchDate)}</td>
                        <td>{dash(match.tournamentStart)}</td>
                        <td>{dash(match.tournamentName)}</td>
                        <td>{dash(match.surface)}</td>
                        <td>{match.isWinner === null ? "—" : match.isWinner ? "Win" : "Loss"}</td>
                        <td>
                          <Link to={`/matches/${match.id}`}>{dash(match.score)}</Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      ) : null}
    </QueryState>
  );
}
