import type { Auth } from "./auth.ts";

export const OSU_API = "https://osu.ppy.sh/api/v2";

/** Ruleset ids for `m` on beatmapset search, and `mode` names elsewhere. */
export const RulesetId = { osu: 0, taiko: 1, fruits: 2, mania: 3 } as const;
export type RulesetName = "osu" | "taiko" | "fruits" | "mania";

/**
 * GET /api/v2/beatmapsets/search
 *
 * Params match osu-web / `SearchBeatmapSetsRequest`. Official docs only mention
 * `cursor_string`; the rest is what the listing UI actually sends.
 *
 * `q` also accepts tokens: ar, artist, bpm, created, creator, cs, dr, keys,
 * length, ranked, stars, status. Example: `stars>=6 keys=4`.
 *
 * @see https://osu.ppy.sh/docs/index.html#search-beatmapset
 */
export type BeatmapsetSearchQuery = {
  q?: string;
  /** Dotted general filters (`c`): recommended, converts, follows, spotlights, featured_artists */
  c?: string | string[];
  /** 0 osu, 1 taiko, 2 fruits, 3 mania */
  m?: 0 | 1 | 2 | 3;
  s?: "any" | "leaderboard" | "ranked" | "qualified" | "loved" | "favourites" | "pending" | "wip" | "graveyard" | "mine";
  /**
   * Genre id (`g`): 0 any, 1 unspecified, 2 video game, 3 anime, 4 rock, 5 pop,
   * 6 other, 7 novelty, 9 hip hop, 10 electronic, 11 metal, 12 classical, 13 folk, 14 jazz.
   */
  g?: number;
  /** Language id (`l`) from the listing UI. 0 = any. */
  l?: number;
  /**
   * `{criteria}_{asc|desc}`. Criteria: title, artist, difficulty, updated,
   * ranked, rating, plays, favourites, relevance, nominations.
   */
  sort?: string;
  /** video, storyboard (dotted if both) */
  e?: string | Array<"video" | "storyboard">;
  /** Achieved ranks to require (`r`), dotted: XH, X, SH, S, A, B, C, D */
  r?: string | string[];
  played?: "played" | "unplayed";
  nsfw?: boolean;
  cursor_string?: string;
};

/**
 * GET /api/v2/rankings/{mode}/{type}
 * @see https://osu.ppy.sh/docs/index.html#get-ranking
 */
export type RankingsQuery = {
  page?: number;
  country?: string;
  cursor?: string;
  filter?: "all" | "friends";
  spotlight?: string | number;
  /** mania only, and only for type=performance (docs: type of `global`) */
  variant?: "4k" | "7k";
};

export type RankingType = "performance" | "score" | "country" | "charts";

/**
 * GET /api/v2/beatmaps/{id}/scores
 * @see https://osu.ppy.sh/docs/index.html#get-beatmap-scores
 */
export type BeatmapScoresQuery = {
  mode?: RulesetName;
  /** Usually `global`. Also country / friend depending on API version. */
  type?: string;
  mods?: string | string[];
  legacy_only?: 0 | 1;
};

/**
 * GET /api/v2/users/{user}/scores/{type}
 * type: best | firsts | recent | pinned
 * @see https://osu.ppy.sh/docs/index.html#get-user-scores
 */
export type UserScoresQuery = {
  mode?: RulesetName;
  limit?: number;
  offset?: number;
  /** recent only. 1 include fails, 0 exclude (API default 0). */
  include_fails?: 0 | 1;
  legacy_only?: 0 | 1;
};

/**
 * GET /api/v2/beatmaps/lookup
 * @see https://osu.ppy.sh/docs/index.html#lookup-beatmap
 */
export type BeatmapLookupQuery = {
  checksum?: string;
  filename?: string;
  id?: number;
};

export class OsuClient {
  private nextCall = 0;

  constructor(
    readonly auth: Auth,
    public log: (line: string) => void = () => {},
  ) {}

  // --- generic ---

  async getJson(url: string, user = false): Promise<unknown> {
    this.log(`GET ${url}`);
    await this.auth.ensureToken(user);
    await this.pace();
    const resp = await fetch(url, {
      headers: {
        Authorization: this.auth.accessToken ? `Bearer ${this.auth.accessToken}` : "",
        "User-Agent": "maniac-lazer-replay-analysis/0.1",
        "x-api-version": "20220705",
        Accept: "application/json",
      },
    });
    const body = await resp.text();
    if (!resp.ok)
      throw new Error(`${resp.status} ${url}: ${trim(body)}`);
    return JSON.parse(body) as unknown;
  }

  async getBytes(
    url: string,
    opts: { user?: boolean; accept?: string; authorize?: boolean } = {},
  ): Promise<{ status: number; bytes: Uint8Array | null }> {
    this.log(`GET ${url}`);
    const authorize = opts.authorize !== false;
    if (authorize)
      await this.auth.ensureToken(Boolean(opts.user));
    await this.pace();
    const headers: Record<string, string> = {
      "User-Agent": "maniac-lazer-replay-analysis/0.1",
      Accept: opts.accept ?? "application/octet-stream",
    };
    if (authorize) {
      headers["x-api-version"] = "20220705";
      if (this.auth.accessToken)
        headers.Authorization = `Bearer ${this.auth.accessToken}`;
    }
    const resp = await fetch(url, { headers });
    if (!resp.ok)
      return { status: resp.status, bytes: null };
    return { status: resp.status, bytes: new Uint8Array(await resp.arrayBuffer()) };
  }

  async get(path: string, query?: QueryValue, user = false): Promise<unknown> {
    return this.getJson(osuUrl(path, query), user);
  }

  // --- beatmapsets ---

  /** GET /beatmapsets/search */
  searchBeatmapsets(query: BeatmapsetSearchQuery = {}): Promise<unknown> {
    return this.get("/beatmapsets/search", query);
  }

  // --- beatmaps ---

  /** GET /beatmaps/{id} */
  getBeatmap(id: number): Promise<unknown> {
    return this.get(`/beatmaps/${id}`);
  }

  /** GET /beatmaps/lookup */
  lookupBeatmap(query: BeatmapLookupQuery): Promise<unknown> {
    return this.get("/beatmaps/lookup", query);
  }

  /** GET /beatmaps/{id}/scores */
  getBeatmapScores(beatmapId: number, query: BeatmapScoresQuery = {}): Promise<unknown> {
    return this.get(`/beatmaps/${beatmapId}/scores`, query);
  }

  // --- rankings ---

  /** GET /rankings/{mode}/{type} */
  getRankings(mode: RulesetName, type: RankingType, query: RankingsQuery = {}): Promise<unknown> {
    return this.get(`/rankings/${mode}/${type}`, query);
  }

  // --- users / me ---

  /** GET /me — needs a user token */
  getMe(): Promise<unknown> {
    return this.get("/me", undefined, true);
  }

  /** GET /users/{user}/scores/{type} */
  getUserScores(
    userId: number,
    type: "best" | "firsts" | "recent" | "pinned",
    query: UserScoresQuery = {},
    user = false,
  ): Promise<unknown> {
    return this.get(`/users/${userId}/scores/${type}`, query, user);
  }

  // --- scores ---

  /** GET /scores/{id} */
  getScore(scoreId: number): Promise<unknown> {
    return this.get(`/scores/${scoreId}`);
  }

  /** GET /scores/{id}/download — replay bytes; 401/403 if the token cannot download. */
  async downloadScore(scoreId: number): Promise<{ status: number; bytes: Uint8Array | null }> {
    return this.getBytes(`${OSU_API}/scores/${scoreId}/download`, { user: this.auth.hasUserToken });
  }

  private async pace(): Promise<void> {
    const wait = this.nextCall - Date.now();
    if (wait > 0)
      await sleep(wait);
    this.nextCall = Date.now() + 1100;
  }
}

type QueryValue = Record<string, string | number | boolean | undefined | null | Array<string | number>>;

export function osuUrl(path: string, query?: QueryValue): string {
  const p = path.startsWith("/") ? path : `/${path}`;
  return `${OSU_API}${p}${buildQuery(query)}`;
}

/** Serializes a query object the way osu!web wants (dotted multi-values, bools as true/false). */
export function buildQuery(query?: QueryValue): string {
  if (!query)
    return "";
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === "")
      continue;
    if (Array.isArray(value)) {
      if (value.length === 0)
        continue;
      params.set(key, value.map(String).join("."));
      continue;
    }
    if (typeof value === "boolean")
      params.set(key, value ? "true" : "false");
    else
      params.set(key, String(value));
  }
  const s = params.toString();
  return s ? `?${s}` : "";
}

export function asRecord(v: unknown): Record<string, unknown> | null {
  return v !== null && typeof v === "object" && !Array.isArray(v) ? v as Record<string, unknown> : null;
}

export function asArray(v: unknown): unknown[] {
  return Array.isArray(v) ? v : [];
}

export function num(obj: Record<string, unknown>, name: string): number {
  const v = obj[name];
  return typeof v === "number" && Number.isFinite(v) ? v : 0;
}

export function str(obj: Record<string, unknown>, name: string): string {
  const v = obj[name];
  return typeof v === "string" ? v : "";
}

export function int(obj: Record<string, unknown>, name: string): number | undefined {
  const v = obj[name];
  if (typeof v === "number" && Number.isFinite(v))
    return Math.trunc(v);
  return undefined;
}

export function trim(s: string): string {
  return s.length <= 180 ? s : s.slice(0, 180);
}

export function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
