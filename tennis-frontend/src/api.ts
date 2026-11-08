import type { Coverage, Dashboard, ImportBatch, ImportSummary, MatchRow, Page, Player, PlayerDetail, TournamentDetail, TournamentRow } from "./types";

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080").replace(/\/$/, "");

async function readError(response: Response): Promise<string> {
  const text = (await response.text()).trim();
  return text.length > 0 ? text : `Request failed (${response.status}).`;
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`);
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as T;
}

export function dashboardQuery() {
  return apiGet<Dashboard>("/api/dashboard");
}

export function coverageQuery() {
  return apiGet<Coverage>("/api/coverage");
}

export function playersQuery(page: number, name: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: "25" });
  if (name.trim()) {
    params.set("name", name.trim());
  }
  return apiGet<Page<Player>>(`/api/players?${params}`);
}

export function playerQuery(id: string) {
  return apiGet<PlayerDetail>(`/api/players/${id}`);
}

export function matchesQuery(filters: Record<string, string>, page: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: "25" });
  for (const [key, value] of Object.entries(filters)) {
    if (value.trim()) {
      params.set(key, value.trim());
    }
  }
  return apiGet<Page<MatchRow>>(`/api/matches?${params}`);
}

export function matchQuery(id: string) {
  return apiGet<MatchRow>(`/api/matches/${id}`);
}

export function tournamentsQuery(filters: Record<string, string>, page: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: "25" });
  for (const [key, value] of Object.entries(filters)) {
    if (value.trim()) {
      params.set(key, value.trim());
    }
  }
  return apiGet<Page<TournamentRow>>(`/api/tournaments?${params}`);
}

export function tournamentQuery(id: string) {
  return apiGet<TournamentDetail>(`/api/tournaments/${id}`);
}

export function importsQuery(page: number) {
  return apiGet<Page<ImportBatch>>(`/api/imports?page=${page}&pageSize=20`);
}

export async function uploadSeasons(files: File[], mode: string): Promise<ImportSummary> {
  const body = new FormData();
  for (const file of files) {
    body.append("files", file);
  }
  const response = await fetch(`${API_BASE_URL}/api/import/upload?mode=${encodeURIComponent(mode)}`, {
    method: "POST",
    body,
  });
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as ImportSummary;
}

export async function importLocal(mode: string): Promise<ImportSummary> {
  const response = await fetch(`${API_BASE_URL}/api/import/local`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ mode, years: null }),
  });
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as ImportSummary;
}
