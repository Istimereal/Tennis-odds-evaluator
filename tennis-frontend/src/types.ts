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
  opponentId: string | null;
  opponentName: string | null;
  round: string | null;
};

export type PlayerMatchHistory = {
  id: string;
  playedOn: string | null;
  opponentId: string | null;
  opponentName: string | null;
  tournamentName: string | null;
  round: string | null;
  surface: string | null;
  score: string | null;
  isWinner: boolean | null;
};

export type PlayerDetail = Player & {
  surfaceRecords: SurfaceRecord[];
  recentMatches: RecentMatch[];
};

export type ResultsSummary = {
  matches: number;
  wins: number;
  losses: number;
  winPercentage: number | null;
};

export type StatLine = {
  key: string;
  label: string;
  kind: string;
  value: number | null;
  basis: string;
  sampleSize: number;
  total: number | null;
  perMatch: number | null;
  numerator: number | null;
  denominator: number | null;
  note: string | null;
};

export type PlayerPerformance = {
  overall: ResultsSummary;
  surfaces: { surface: string; results: ResultsSummary }[];
  levels: { level: string; results: ResultsSummary }[];
  rankingGroups: { group: string; results: ResultsSummary }[];
  opponentRanking: { average: number | null; sampleSize: number; missing: number };
  recentForm: { key: string; label: string; seasonYear: number | null; results: ResultsSummary }[];
  statistics: StatLine[];
  coverage: {
    matches: number;
    missingOpponentRanking: number;
    missingOrOtherSurface: number;
    missingAces: number;
    missingDoubleFaults: number;
    missingServicePointsWon: number;
    missingReturnPointsWon: number;
    missingBreakPointsWon: number;
    missingBreakPointsSaved: number;
    undecidedResults: number;
    excludedMatches: number;
  };
  filters: {
    asOfDate: string | null;
    surfaces: string[];
    level: string | null;
    from: string | null;
    to: string | null;
    rankingGroup: string | null;
    recentMatchCount: number | null;
    excludeMatchId: string | null;
    seasonYear: number | null;
  };
};

export type PlayerStatistics = {
  aces: number | null;
  doubleFaults: number | null;
  servicePointsWonPercent: number | null;
  returnPointsWonPercent: number | null;
  breakPointsWonPercent: number | null;
  breakPointsSavedPercent: number | null;
  firstServePercent: number | null;
  firstServePointsWonPercent: number | null;
  secondServePointsWonPercent: number | null;
  breakPointsFaced: number | null;
  breakPointsSaved: number | null;
  breakPointChances: number | null;
  breakPointsConverted: number | null;
  winners: number | null;
  unforcedErrors: number | null;
  netPointsWon: number | null;
  totalPointsWon: number | null;
};

export type MatchSetScore = {
  setNumber: number;
  homeGames: number | null;
  awayGames: number | null;
  homeTiebreak: number | null;
  awayTiebreak: number | null;
};

export type MatchConditions = {
  durationMinutes: number | null;
  actualStart: string | null;
  completedAt: string | null;
  roof: string | null;
  venue: string | null;
  interruptions: string | null;
};

export type PointByPoint = {
  available: boolean;
  state: string;
  message: string;
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
  rankingPoints: number | null;
  playerStatistics: PlayerStatistics | null;
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
  status: string | null;
  statusExtra: string | null;
  sets: MatchSetScore[] | null;
  conditions: MatchConditions | null;
  pointByPoint: PointByPoint | null;
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

export type OddsMethodEstimate = {
  method: string;
  status: string;
  message: string;
  headToHeadMatchCount: number;
  playerAProbability: number | null;
  playerBProbability: number | null;
  playerADecimalOdds: number | null;
  playerBDecimalOdds: number | null;
};

export type OddsGeneratorResult = {
  playerAId: string;
  playerAName: string | null;
  playerBId: string;
  playerBName: string | null;
  playerAProbability: number | null;
  playerBProbability: number | null;
  playerADecimalOdds: number | null;
  playerBDecimalOdds: number | null;
  method: string;
  headToHeadMatchCount: number;
  status: string;
  message: string;
  matchContext: {
    tournament: string | null;
    startsAt: string | null;
    round: string | null;
    surface: string | null;
    venue: string | null;
  } | null;
  methods: OddsMethodEstimate[];
  combined: OddsMethodEstimate | null;
};

export type ComparisonPlayer = {
  id: string;
  name: string;
  sourceId: string | null;
  nationality: string | null;
  matchCount: number;
  wins: number;
  losses: number;
};

export type HeadToHeadMeeting = {
  matchId: string;
  sourceKey: string;
  date: string | null;
  tournament: string | null;
  surface: string | null;
  round: string | null;
  winnerId: string | null;
  winnerName: string | null;
  score: string | null;
  venue: string | null;
  time: string | null;
};

export type PlayerComparison = {
  status: string;
  message: string;
  playerA: ComparisonPlayer | null;
  playerB: ComparisonPlayer | null;
  totalMatches: number;
  playerAWins: number;
  playerBWins: number;
  playerAWinPercentage: number | null;
  playerBWinPercentage: number | null;
  meetings: HeadToHeadMeeting[];
  odds: OddsGeneratorResult;
};
