export type Page<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type Player = {
  id: string;
  sourceId: string | null;
  name: string;
  nationality: string | null;
  matchCount: number;
  wins: number;
  losses: number;
};

export type SurfaceRecord = {
  surface: string;
  wins: number;
  losses: number;
};

export type RecentMatch = {
  id: string;
  sourceYear: number;
  matchDate: string | null;
  tournamentStart: string | null;
  winnerCode: number | null;
  isWinner: boolean | null;
  tournamentName: string | null;
  surface: string | null;
  level: string | null;
  score: string | null;
};

export type PlayerDetail = Player & {
  surfaceRecords: SurfaceRecord[];
  recentMatches: RecentMatch[];
};

export type MatchPlayer = {
  playerId: string | null;
  sourceId: string | null;
  name: string | null;
  side: string;
  isWinner: boolean | null;
  ranking: number | null;
  statistics: string;
  odds: string;
};

export type MatchRow = {
  id: string;
  sourceKey: string;
  sourceYear: number;
  winnerCode: number | null;
  playedOn: string | null;
  tournamentId: string | null;
  tournamentName: string | null;
  surface: string | null;
  level: string | null;
  players: MatchPlayer[];
  rawRecord: string | null;
  round: string | null;
  score: string | null;
  statCompleteness: number | null;
};

export type TournamentRow = {
  id: string;
  sourceId: string | null;
  name: string;
  year: number | null;
  surface: string | null;
  level: string | null;
  startDate: string | null;
};

export type TournamentEdition = {
  id: string;
  year: number;
  name: string | null;
  surface: string | null;
  level: string | null;
  startDate: string | null;
  matchCount: number;
};

export type TournamentDetail = {
  id: string;
  sourceId: string | null;
  name: string;
  editions: TournamentEdition[];
};

export type ImportStatus = {
  id: string;
  mode: string;
  startedAt: string;
  finishedAt: string | null;
  filesProcessed: number;
  matchesInserted: number;
  invalidRowsSkipped: number;
  error: string | null;
};

export type Completeness = {
  matches: number;
  matchesWithWinner: number;
  matchesWithMatchDate: number;
  matchesWithTournamentStart: number;
  matchesWithSurface: number;
  matchesWithLevel: number;
  missingStatisticCells: number;
};

export type NamedCount = { name: string; count: number };
export type YearCount = { year: number; count: number };

export type Dashboard = {
  players: number;
  matches: number;
  tournaments: number;
  importedSeasons: number;
  latestBatch: ImportStatus | null;
  latestSuccessfulImport: ImportStatus | null;
  failedFiles: number;
  batchesWithErrors: number;
  completeness: Completeness;
  matchesByYear: YearCount[];
  matchesBySurface: NamedCount[];
  matchesByLevel: NamedCount[];
};

export type ImportFile = {
  id: string;
  sourceYear: number | null;
  originalFileName: string;
  sha256: string;
  status: string;
  rowsRead: number;
  matchesInserted: number;
  duplicatesSkipped: number;
  invalidRowsSkipped: number;
  missingStatisticCells: number;
  unmappedColumns: string[];
  error: string | null;
  importedAt: string;
  detectedHeaders: string[];
};

export type ImportBatch = {
  id: string;
  mode: string;
  provenance: string;
  startedAt: string;
  finishedAt: string | null;
  filesProcessed: number;
  rowsRead: number;
  matchesInserted: number;
  playersInserted: number;
  duplicatesSkipped: number;
  invalidRowsSkipped: number;
  missingStatisticCells: number;
  error: string | null;
  files: ImportFile[];
};

export type ImportSummary = {
  downloadAutomated: boolean;
  filesProcessed: number;
  rowsRead: number;
  matchesInserted: number;
  playersInserted: number;
  duplicatesSkipped: number;
  invalidRowsSkipped: number;
  missingStatisticCells: number;
  unmappedColumns: string[];
  unmappedFields: { field: string; reason: string }[];
  message: string;
};

export type SeasonCoverage = {
  year: number;
  filesImported: number;
  rowsRead: number;
  matchesInserted: number;
  duplicatesSkipped: number;
  invalidRowsSkipped: number;
  missingStatisticCells: number;
  unmappedColumns: string[];
};

export type Coverage = {
  seasons: SeasonCoverage[];
  blockedMappings: { field: string; reason: string }[];
};
