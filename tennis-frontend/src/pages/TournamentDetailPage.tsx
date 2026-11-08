import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { tournamentQuery } from "../api";
import { QueryState, dash } from "../components/QueryState";

export function TournamentDetailPage() {
  const { id = "" } = useParams();
  const query = useQuery({ queryKey: ["tournament", id], queryFn: () => tournamentQuery(id) });

  return (
    <QueryState isPending={query.isPending} error={query.error}>
      {query.data ? (
        <section className="panel">
          <h2>{query.data.name}</h2>
          <p className="muted">Source id {dash(query.data.sourceId)}</p>
          {query.data.editions.length === 0 ? (
            <p>No editions are stored.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Edition</th>
                    <th>Year</th>
                    <th>Level</th>
                    <th>Surface</th>
                    <th>Start</th>
                    <th>Matches</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.editions.map((edition) => (
                    <tr key={edition.id}>
                      <td>{dash(edition.name)}</td>
                      <td>{edition.year}</td>
                      <td>{dash(edition.level)}</td>
                      <td>{dash(edition.surface)}</td>
                      <td>{dash(edition.startDate)}</td>
                      <td>{edition.matchCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : null}
    </QueryState>
  );
}
