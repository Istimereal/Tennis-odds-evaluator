import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { tournamentsQuery } from "../api";
import { EmptyNote, Pager, QueryState, dash } from "../components/QueryState";

export function TournamentsPage() {
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState({ name: "", year: "", surface: "", level: "" });
  const [filters, setFilters] = useState<Record<string, string>>({});
  const query = useQuery({ queryKey: ["tournaments", page, filters], queryFn: () => tournamentsQuery(filters, page) });

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
        <input aria-label="Year" placeholder="Year" value={draft.year} onChange={(event) => setDraft({ ...draft, year: event.target.value })} />
        <input aria-label="Surface" placeholder="Surface" value={draft.surface} onChange={(event) => setDraft({ ...draft, surface: event.target.value })} />
        <input aria-label="Level" placeholder="Level" value={draft.level} onChange={(event) => setDraft({ ...draft, level: event.target.value })} />
        <button type="submit">Filter</button>
      </form>
      <QueryState isPending={query.isPending} error={query.error}>
        {query.data && query.data.totalCount === 0 ? (
          <EmptyNote>No tournament editions are stored. Name, level, and surface stay empty until those columns are read from a season file.</EmptyNote>
        ) : query.data ? (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Year</th>
                    <th>Level</th>
                    <th>Surface</th>
                    <th>Start</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((row) => (
                    <tr key={`${row.id}-${row.year ?? "none"}`}>
                      <td>
                        <Link to={`/tournaments/${row.id}`}>{row.name}</Link>
                      </td>
                      <td>{dash(row.year)}</td>
                      <td>{dash(row.level)}</td>
                      <td>{dash(row.surface)}</td>
                      <td>{dash(row.startDate)}</td>
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
