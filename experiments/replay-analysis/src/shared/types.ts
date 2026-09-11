export type AuthStatus = {
  hasV2: boolean;
  hasUserToken: boolean;
  secretsPath: string;
  login: "idle" | "waiting" | "ok" | "error";
  loginError?: string;
  authorizeUrl?: string;
};

export type FetchRequest = {
  replace: boolean;
  stream: FetchStream;
};

export type RankedSearch = {
  /** Passed through to `q` on /beatmapsets/search (stars, keys, creator, …). */
  q?: string;
  s?: "any" | "leaderboard" | "ranked" | "qualified" | "loved";
  keys: number[];
  minStars: number;
  minPlaycount: number;
  take: number;
  sort: string;
  sample: "order" | "random";
};

/** How to pick scores from one beatmap’s global leaderboard (API max 50). */
export type LeaderboardPick = {
  take: number;
  /** 1-based inclusive rank in the API list (#1 is first). */
  rankFrom: number;
  rankTo: number;
  sample: "random" | "even";
};

export type MapLeaderboardsStream = {
  type: "map-leaderboards";
  /** If non-empty, these IDs are used and `search` is ignored. */
  beatmapIds: number[];
  search?: RankedSearch;
  scores: LeaderboardPick;
};

export type RankingRecentsStream = {
  type: "ranking-recents";
  variant: "4k" | "7k";
  pages: number[];
  usersPerPage: number;
  maxReplays: number;
  includeFails: boolean;
};

export type UserBestStream = {
  type: "user-best";
  limit: number;
};

export type FetchStream = MapLeaderboardsStream | RankingRecentsStream | UserBestStream;

export type JobStatus = {
  id: string;
  status: "running" | "ok" | "error";
  saved: number;
  error?: string;
  logs: string[];
};

export type CatalogReplay = {
  file: string;
  player: string;
  beatmapMd5: string;
  map?: string;
  mods: string;
  mode: string;
  source: string;
  leaderboardRank?: number;
  accuracy?: number;
  stars?: number;
  size: number;
  parseOk: boolean;
  parseError?: string;
  isLazer: boolean;
  frameCount?: number;
  eventCount?: number;
  onlineId?: number;
  timestamp?: string;
  columns?: number;
  /** Fetch runs that produced or re-counted this file. Empty for dropped-in locals. */
  runIds: string[];
};

/** One fetch job: the search/recipe plus which files it produced. */
export type FetchRun = {
  id: string;
  at: string;
  label: string;
  replace: boolean;
  saved: number;
  files: string[];
  beatmapIds: number[];
  stream: FetchStream;
};

export type CatalogResponse = {
  auth: AuthStatus;
  replays: CatalogReplay[];
  runs: FetchRun[];
  replayCount: number;
  beatmapCount: number;
  dataDir: string;
};

export type ReplayEvent = {
  time: number;
  column: number;
  down: boolean;
};

export type Judgement = "miss" | "meh" | "ok" | "good" | "great" | "perfect";
export type HitKind = "tap" | "holdHead" | "holdTail";

export type HitWindowsInfo = {
  classic: boolean;
  rate: number;
  od: number;
  convert: boolean;
  perfect: number;
  great: number;
  good: number;
  ok: number;
  meh: number;
  miss: number;
};

export type NoteHit = {
  noteIndex: number;
  noteStart: number;
  noteEnd: number;
  column: number;
  kind: HitKind;
  pressTime?: number;
  releaseTime?: number;
  /** Action time − note time. Negative is early. */
  error?: number;
  judgement: Judgement;
  hit: boolean;
};

export type MatchSummary = {
  noteCount: number;
  columns: number;
  matchedHeads: number;
  missedHeads: number;
  ghostTaps: number;
  extraReleases: number;
  meanError?: number;
  stdError?: number;
  unstableRate?: number;
  judgements: Record<Judgement, number>;
  windows: HitWindowsInfo;
};

export type ReplayInspector = {
  file: string;
  player: string;
  beatmapMd5: string;
  map?: string;
  mods: string;
  mode: string;
  isLazer: boolean;
  version: number;
  onlineId?: number;
  timestamp?: string;
  clientVersion?: string;
  count300: number;
  count100: number;
  count50: number;
  countMiss: number;
  totalScore: number;
  maxCombo: number;
  frameCount: number;
  eventCount: number;
  parseError?: string;
  events: ReplayEvent[];
  eventOffset: number;
  eventTotal: number;
  matchError?: string;
  match?: MatchSummary;
  hits: NoteHit[];
  hitOffset: number;
  hitTotal: number;
};
