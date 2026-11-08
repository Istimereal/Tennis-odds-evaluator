import type { ReactNode } from "react";

export function QueryState({
  isPending,
  error,
  children,
}: {
  isPending: boolean;
  error: Error | null;
  children: ReactNode;
}) {
  if (isPending) {
    return <p className="muted">Loading from PostgreSQL…</p>;
  }
  if (error) {
    return <p className="error">{error.message}</p>;
  }
  return children;
}

export function EmptyNote({ children }: { children: ReactNode }) {
  return <div className="empty">{children}</div>;
}

export function Pager({
  page,
  pageSize,
  total,
  onPage,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="pager">
      <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Previous
      </button>
      <span className="muted">
        Page {page} of {pages} · {total} rows
      </span>
      <button type="button" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        Next
      </button>
    </div>
  );
}

export function dash(value: string | number | null | undefined) {
  if (value === null || value === undefined || value === "") {
    return "—";
  }
  return String(value);
}
