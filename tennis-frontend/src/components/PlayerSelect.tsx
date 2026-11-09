import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { playersQuery } from "../api";
import type { Player } from "../types";

export function PlayerSelect({
  label,
  selected,
  onSelect,
}: {
  label: string;
  selected: Player | null;
  onSelect: (player: Player | null) => void;
}) {
  const [draft, setDraft] = useState(selected?.name ?? "");
  const [term, setTerm] = useState("");
  const [open, setOpen] = useState(false);
  const closeTimer = useRef<number | null>(null);

  useEffect(() => {
    return () => {
      if (closeTimer.current !== null) {
        window.clearTimeout(closeTimer.current);
      }
    };
  }, []);

  useEffect(() => {
    if (selected) {
      setDraft(selected.name);
    }
  }, [selected]);

  useEffect(() => {
    const handle = window.setTimeout(() => setTerm(draft.trim()), 200);
    return () => window.clearTimeout(handle);
  }, [draft]);

  const searching = open && term.length > 0;
  const query = useQuery({
    queryKey: ["player-search", term],
    queryFn: () => playersQuery(1, term),
    enabled: searching,
  });

  return (
    <div className="player-select">
      <input
        aria-label={label}
        placeholder={label}
        value={draft}
        autoComplete="off"
        onFocus={() => {
          if (closeTimer.current !== null) {
            window.clearTimeout(closeTimer.current);
          }
          setOpen(true);
        }}
        onBlur={() => {
          closeTimer.current = window.setTimeout(() => setOpen(false), 150);
        }}
        onChange={(event) => {
          const value = event.target.value;
          setDraft(value);
          setOpen(true);
          if (selected && value !== selected.name) {
            onSelect(null);
          }
        }}
      />
      {selected ? (
        <button
          type="button"
          className="text-button"
          aria-label={`Clear ${label}`}
          onClick={() => {
            onSelect(null);
            setDraft("");
            setTerm("");
            setOpen(false);
          }}
        >
          Clear
        </button>
      ) : null}
      {searching && query.isPending ? <p className="muted picker-status">Searching…</p> : null}
      {searching && query.isError ? <p className="error picker-status">{query.error.message}</p> : null}
      {searching && query.data && query.data.items.length === 0 ? <p className="muted picker-status">No players match that name.</p> : null}
      {searching && query.data && query.data.items.length > 0 ? (
        <ul className="search-results picker-results">
          {query.data.items.map((player) => (
            <li key={player.id}>
              <button
                type="button"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => {
                  onSelect(player);
                  setDraft(player.name);
                  setOpen(false);
                }}
              >
                {player.name}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
      {searching && query.data && query.data.totalCount > query.data.items.length ? (
        <p className="muted picker-status">
          Showing {query.data.items.length} of {query.data.totalCount}. Type more of the name.
        </p>
      ) : null}
    </div>
  );
}
