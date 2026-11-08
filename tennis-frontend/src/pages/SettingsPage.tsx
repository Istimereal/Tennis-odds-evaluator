import { useQuery } from "@tanstack/react-query";
import { API_BASE_URL, coverageQuery } from "../api";
import { QueryState } from "../components/QueryState";

export function SettingsPage() {
  const coverage = useQuery({ queryKey: ["coverage"], queryFn: coverageQuery });

  return (
    <>
      <section className="panel">
        <h2>API</h2>
        <p>
          Base URL <code>{API_BASE_URL}</code>
        </p>
        <p className="muted">Override it with VITE_API_BASE_URL. The tennis UI runs on port 5174 so it does not replace the Cold Room app in frontend/.</p>
      </section>
      <section className="panel">
        <h2>Coverage</h2>
        <p>ATP season files are expected for 2021 through 2026. 2020 is an explicit gap. Nationality is not a stored player column.</p>
        <QueryState isPending={coverage.isPending} error={coverage.error}>
          {coverage.data ? (
            <ul>
              {coverage.data.blockedMappings.map((field) => (
                <li key={field.field}>
                  <strong>{field.field}.</strong> {field.reason}
                </li>
              ))}
            </ul>
          ) : null}
        </QueryState>
      </section>
    </>
  );
}
