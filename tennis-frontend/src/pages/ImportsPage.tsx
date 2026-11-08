import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { coverageQuery, importLocal, importsQuery, uploadSeasons } from "../api";
import { Pager, QueryState } from "../components/QueryState";

export function ImportsPage() {
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [mode, setMode] = useState("backfill");
  const [files, setFiles] = useState<File[]>([]);
  const history = useQuery({ queryKey: ["imports", page], queryFn: () => importsQuery(page) });
  const coverage = useQuery({ queryKey: ["coverage"], queryFn: coverageQuery });
  const refresh = async () => {
    await client.invalidateQueries({ queryKey: ["imports"] });
    await client.invalidateQueries({ queryKey: ["coverage"] });
    await client.invalidateQueries({ queryKey: ["dashboard"] });
    await client.invalidateQueries({ queryKey: ["players"] });
    await client.invalidateQueries({ queryKey: ["matches"] });
    await client.invalidateQueries({ queryKey: ["tournaments"] });
  };
  const upload = useMutation({
    mutationFn: () => uploadSeasons(files, mode),
    onSuccess: refresh,
  });
  const local = useMutation({
    mutationFn: () => importLocal(mode),
    onSuccess: refresh,
  });
  const summary = upload.data ?? local.data;
  const headers = history.data?.items.flatMap((batch) => batch.files.flatMap((file) => file.detectedHeaders)) ?? [];
  const uniqueHeaders = [...new Set(headers)];

  return (
    <>
      <section className="panel">
        <p>Upload yearly ATP files named yyyy-atp-season.csv. The browser sends the file. The API parses it.</p>
        <form
          className="filters"
          onSubmit={(event) => {
            event.preventDefault();
            upload.mutate();
          }}
        >
          <input
            type="file"
            accept=".csv,text/csv"
            multiple
            aria-label="Season CSV files"
            onChange={(event) => setFiles([...(event.target.files ?? [])])}
          />
          <select aria-label="Import mode" value={mode} onChange={(event) => setMode(event.target.value)}>
            <option value="backfill">backfill</option>
            <option value="incremental">incremental</option>
            <option value="refresh">refresh</option>
          </select>
          <button type="submit" disabled={upload.isPending || files.length === 0}>
            Start import
          </button>
          <button type="button" disabled={local.isPending} onClick={() => local.mutate()}>
            Import files already in Data/source
          </button>
        </form>
        {upload.isPending || local.isPending ? <p className="muted">Import running…</p> : null}
        {upload.error || local.error ? <p className="error">{(upload.error ?? local.error)?.message}</p> : null}
        {summary ? (
          <p>
            Inserted {summary.matchesInserted}. Players inserted {summary.playersInserted}. Skipped duplicates {summary.duplicatesSkipped}. Invalid{" "}
            {summary.invalidRowsSkipped}. Unmapped columns {summary.unmappedColumns.length}. {summary.message}
          </p>
        ) : null}
        <p className="muted">
          Re-import is safe: an unchanged file is skipped by its SHA-256. Updated is not counted, because a stable match id column has not been seen, so a changed row cannot be matched in place.
        </p>
      </section>
      <section className="panel">
        <h2>Detected headers</h2>
        {uniqueHeaders.length === 0 ? <p className="muted">No header has been stored yet.</p> : <p>{uniqueHeaders.join(", ")}</p>}
      </section>
      <section className="panel">
        <h2>History</h2>
        <QueryState isPending={history.isPending} error={history.error}>
          {history.data ? (
            <>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Started</th>
                      <th>Mode</th>
                      <th>Status</th>
                      <th>File</th>
                      <th>Inserted</th>
                      <th>Skipped</th>
                      <th>Invalid</th>
                      <th>Unmapped</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.data.items.flatMap((batch) =>
                      batch.files.length === 0
                        ? [
                            <tr key={batch.id}>
                              <td>{batch.startedAt}</td>
                              <td>{batch.mode}</td>
                              <td>{batch.error ?? "finished"}</td>
                              <td>—</td>
                              <td>{batch.matchesInserted}</td>
                              <td>{batch.duplicatesSkipped}</td>
                              <td>{batch.invalidRowsSkipped}</td>
                              <td>—</td>
                            </tr>,
                          ]
                        : batch.files.map((file) => (
                            <tr key={file.id}>
                              <td>{batch.startedAt}</td>
                              <td>{batch.mode}</td>
                              <td>{file.status}</td>
                              <td>{file.originalFileName}</td>
                              <td>{file.matchesInserted}</td>
                              <td>{file.duplicatesSkipped}</td>
                              <td>{file.invalidRowsSkipped}</td>
                              <td>{file.unmappedColumns.join(", ") || "—"}</td>
                            </tr>
                          )),
                    )}
                  </tbody>
                </table>
              </div>
              <Pager page={history.data.page} pageSize={history.data.pageSize} total={history.data.totalCount} onPage={setPage} />
            </>
          ) : null}
        </QueryState>
      </section>
      <section className="panel">
        <h2>Coverage by season</h2>
        <QueryState isPending={coverage.isPending} error={coverage.error}>
          {coverage.data ? (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Season</th>
                    <th>Files</th>
                    <th>Rows</th>
                    <th>Inserted</th>
                    <th>Skipped</th>
                    <th>Invalid</th>
                  </tr>
                </thead>
                <tbody>
                  {coverage.data.seasons.map((season) => (
                    <tr key={season.year}>
                      <td>{season.year}</td>
                      <td>{season.filesImported}</td>
                      <td>{season.rowsRead}</td>
                      <td>{season.matchesInserted}</td>
                      <td>{season.duplicatesSkipped}</td>
                      <td>{season.invalidRowsSkipped}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </QueryState>
      </section>
    </>
  );
}
