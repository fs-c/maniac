import { randomUUID } from "node:crypto";
import { existsSync, readdirSync, readFileSync, statSync, unlinkSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { summarizeFetchStream } from "../shared/runLabel.ts";
import type {
  CatalogReplay,
  CatalogResponse,
  FetchRequest,
  FetchRun,
  FetchStream,
  MapLeaderboardsStream,
  RankingRecentsStream,
  RankedSearch,
  LeaderboardPick,
  ReplayInspector,
  UserBestStream,
} from "../shared/types.ts";
import type { Auth } from "./auth.ts";
import { asArray, asRecord, int, num, OsuClient, str } from "./osu.ts";
import {
  GameMode,
  GameModeName,
  isLazer,
  looksLikeOsu,
  modsLabel,
  parseOsr,
  parseOsuMeta,
  type OsrReplay,
  type OsuMeta,
} from "./osr.ts";
import { beatmapDir, dataDir, ensureDataDirs, replayDir } from "./paths.ts";

const SKIP_MODS = new Set(["RD", "AU", "CN", "RX", "AP", "4K", "5K", "6K", "7K", "8K", "9K"]);

export type ScoreMeta = {
  leaderboardRank?: number;
  accuracy: number;
  stars: number;
  source: string;
  userGlobalRank?: number;
  beatmapId: number;
  player: string;
};

type ScoreRef = {
  id: number;
  beatmapId: number;
  player: string;
  accuracy: number;
  mods: string[];
  leaderboardRank?: number;
  stars: number;
  source: string;
  userGlobalRank?: number;
};

type MapRef = { id: number; stars: number; keys: number };

export class Catalog {
  private manifest = new Map<number, ScoreMeta>();
  private beatmaps = new Map<string, OsuMeta>();
  private runs: FetchRun[] = [];
  readonly osu: OsuClient;

  constructor(readonly auth: Auth) {
    ensureDataDirs();
    this.osu = new OsuClient(auth);
    this.loadManifest();
    this.loadRuns();
    this.indexBeatmaps();
  }

  catalog(): CatalogResponse {
    const replays = this.listReplays();
    return {
      auth: this.auth.status(),
      replays,
      runs: this.runs,
      replayCount: replays.length,
      beatmapCount: readdirSync(beatmapDir).filter((f) => f.endsWith(".osu")).length,
      dataDir,
    };
  }

  inspector(file: string, offset = 0, limit = 400): ReplayInspector {
    const path = join(replayDir, file);
    if (!existsSync(path))
      throw new Error(`replay not found: ${file}`);
    const replay = parseOsr(path);
    const map = this.beatmaps.get(replay.beatmapMd5.toLowerCase());
    const events = replay.events;
    const slice = events.slice(offset, offset + limit);
    return {
      file,
      player: replay.player,
      beatmapMd5: replay.beatmapMd5,
      map: map?.displayName,
      mods: modsLabel(replay),
      mode: GameModeName[replay.mode] ?? String(replay.mode),
      isLazer: isLazer(replay),
      version: replay.version,
      onlineId: replay.onlineId || undefined,
      timestamp: replay.timestamp.toISOString(),
      clientVersion: replay.clientVersion,
      count300: replay.count300,
      count100: replay.count100,
      count50: replay.count50,
      countMiss: replay.countMiss,
      totalScore: replay.totalScore,
      maxCombo: replay.maxCombo,
      frameCount: replay.frames.length,
      eventCount: events.length,
      parseError: replay.frameError,
      events: slice.map((e) => ({ time: e.time, column: e.column, down: e.down })),
      eventOffset: offset,
      eventTotal: events.length,
    };
  }

  async fetch(req: FetchRequest, log: (line: string) => void): Promise<number> {
    if (!this.auth.hasV2)
      throw new Error(this.auth.credentialHelp());

    if (req.replace)
      this.clearReplays(log);

    const run = this.beginRun(req);
    const prevLog = this.osu.log;
    this.osu.log = log;
    try {
      let saved = 0;
      switch (req.stream.type) {
        case "map-leaderboards":
          saved = await this.fetchMapLeaderboards(req.stream, run, log);
          break;
        case "ranking-recents":
          saved = await this.fetchRankingRecents(req.stream, run, log);
          break;
        case "user-best":
          saved = await this.fetchUserBest(req.stream, run, log);
          break;
      }
      run.saved = saved;
      if (run.files.length > 0) {
        this.finishRun(run);
        log(`run     ${run.label}  ${run.files.length} files  ${run.beatmapIds.length} maps`);
      }
      this.saveManifest();
      return saved;
    } catch (err) {
      if (run.files.length > 0) {
        run.saved = run.files.length;
        this.finishRun(run);
        this.saveManifest();
      }
      throw err;
    } finally {
      this.osu.log = prevLog;
    }
  }

  private listReplays(): CatalogReplay[] {
    this.indexBeatmaps();
    const files = readdirSync(replayDir).filter((f) => f.toLowerCase().endsWith(".osr")).sort();
    const rows: CatalogReplay[] = [];
    for (const file of files) {
      const path = join(replayDir, file);
      const size = statSync(path).size;
      try {
        const replay = parseOsr(path);
        const meta = this.metaFor(replay, file);
        const map = this.beatmaps.get(replay.beatmapMd5.toLowerCase());
        rows.push({
          file,
          player: replay.player,
          beatmapMd5: replay.beatmapMd5,
          map: map?.displayName,
          mods: modsLabel(replay),
          mode: GameModeName[replay.mode] ?? String(replay.mode),
          source: meta?.source ?? "local",
          leaderboardRank: meta?.leaderboardRank,
          accuracy: meta?.accuracy,
          stars: meta?.stars,
          size,
          parseOk: replay.mode === GameMode.Mania && !replay.frameError,
          parseError: replay.frameError ?? (replay.mode === GameMode.Mania ? undefined : "not mania"),
          isLazer: isLazer(replay),
          frameCount: replay.frameError ? undefined : replay.frames.length,
          eventCount: replay.frameError ? undefined : replay.events.length,
          onlineId: replay.onlineId || undefined,
          timestamp: replay.timestamp.toISOString(),
          columns: map?.columns,
          runIds: this.runIdsForFile(file),
        });
      } catch (err) {
        rows.push({
          file,
          player: "",
          beatmapMd5: "",
          mods: "",
          mode: "",
          source: "local",
          size,
          parseOk: false,
          parseError: err instanceof Error ? err.message : String(err),
          isLazer: false,
          runIds: this.runIdsForFile(file),
        });
      }
    }
    return rows;
  }

  private metaFor(replay: OsrReplay, file: string): ScoreMeta | undefined {
    if (replay.onlineId > 0 && this.manifest.has(replay.onlineId))
      return this.manifest.get(replay.onlineId);
    const dash = file.indexOf("-");
    if (dash > 0) {
      const fromName = Number(file.slice(0, dash));
      if (Number.isFinite(fromName) && this.manifest.has(fromName))
        return this.manifest.get(fromName);
    }
    return undefined;
  }

  private runIdsForFile(file: string): string[] {
    return this.runs.filter((run) => run.files.includes(file)).map((run) => run.id);
  }

  private beginRun(req: FetchRequest): FetchRun {
    return {
      id: randomUUID(),
      at: new Date().toISOString(),
      label: summarizeFetchStream(req.stream),
      replace: req.replace,
      saved: 0,
      files: [],
      beatmapIds: [],
      stream: req.stream,
    };
  }

  private finishRun(run: FetchRun): void {
    this.runs = [run, ...this.runs.filter((existing) => existing.id !== run.id)];
    this.saveRuns();
  }

  private attachToRun(run: FetchRun, file: string, beatmapId: number): void {
    if (file && !run.files.includes(file))
      run.files.push(file);
    if (beatmapId > 0 && !run.beatmapIds.includes(beatmapId))
      run.beatmapIds.push(beatmapId);
  }

  private clearReplays(log: (line: string) => void): void {
    for (const file of readdirSync(replayDir).filter((f) => f.endsWith(".osr")))
      unlinkSync(join(replayDir, file));
    this.manifest.clear();
    this.runs = [];
    this.saveManifest();
    this.saveRuns();
    log("cleared old replays and runs");
  }

  private manifestPath(): string {
    return join(dataDir, "manifest.json");
  }

  private loadManifest(): void {
    const path = this.manifestPath();
    if (!existsSync(path))
      return;
    try {
      const loaded = JSON.parse(readFileSync(path, "utf8")) as Record<string, ScoreMeta & {
        leaderboard_rank?: number;
        user_global_rank?: number;
        beatmap_id?: number;
      }>;
      for (const [key, value] of Object.entries(loaded)) {
        const id = Number(key);
        if (!Number.isFinite(id))
          continue;
        this.manifest.set(id, {
          leaderboardRank: value.leaderboardRank ?? value.leaderboard_rank,
          accuracy: value.accuracy ?? 0,
          stars: value.stars ?? 0,
          source: value.source ?? "",
          userGlobalRank: value.userGlobalRank ?? value.user_global_rank,
          beatmapId: value.beatmapId ?? value.beatmap_id ?? 0,
          player: value.player ?? "",
        });
      }
    } catch {
      // start empty
    }
  }

  private saveManifest(): void {
    const boxed: Record<string, ScoreMeta> = {};
    for (const [id, meta] of this.manifest)
      boxed[String(id)] = meta;
    writeFileSync(this.manifestPath(), JSON.stringify(boxed, null, 2));
  }

  private runsPath(): string {
    return join(dataDir, "runs.json");
  }

  private loadRuns(): void {
    const path = this.runsPath();
    if (!existsSync(path))
      return;
    try {
      const loaded = JSON.parse(readFileSync(path, "utf8")) as unknown;
      if (!Array.isArray(loaded))
        return;
      this.runs = loaded.filter(isFetchRun);
    } catch {
      this.runs = [];
    }
  }

  private saveRuns(): void {
    writeFileSync(this.runsPath(), JSON.stringify(this.runs, null, 2));
  }

  private indexBeatmaps(): void {
    this.beatmaps.clear();
    for (const file of readdirSync(beatmapDir).filter((f) => f.endsWith(".osu"))) {
      try {
        const bytes = new Uint8Array(readFileSync(join(beatmapDir, file)));
        const meta = parseOsuMeta(bytes);
        this.beatmaps.set(meta.md5.toLowerCase(), meta);
        const stem = file.replace(/\.osu$/i, "").toLowerCase();
        if (stem !== meta.md5.toLowerCase())
          this.beatmaps.set(stem, meta);
      } catch {
        // skip
      }
    }
  }

  private async fetchMapLeaderboards(stream: MapLeaderboardsStream, run: FetchRun, log: (line: string) => void): Promise<number> {
    const maps = stream.beatmapIds.length > 0
      ? stream.beatmapIds.map((id) => ({ id, stars: 0, keys: 0 }))
      : stream.search
        ? await this.searchRankedManiaMaps(stream.search, log)
        : [];
    if (maps.length === 0)
      throw new Error("no beatmaps to fetch");
    log(`maps    ${maps.length}  then ${stream.scores.take} scores each  ranks ${stream.scores.rankFrom}–${stream.scores.rankTo}  ${stream.scores.sample}`);
    for (const map of maps)
      this.attachToRun(run, "", map.id);
    let saved = 0;
    for (const map of maps) {
      if (map.stars)
        log(`map     #${map.id}  ${map.stars.toFixed(1)}* ${map.keys}K`);
      saved += await this.fetchLeaderboardScores(map.id, map.stars, stream.scores, run, log);
    }
    return saved;
  }

  private async fetchRankingRecents(stream: RankingRecentsStream, run: FetchRun, log: (line: string) => void): Promise<number> {
    let saved = 0;
    for (const page of stream.pages) {
      if (saved >= stream.maxReplays)
        return saved;
      const lo = (page - 1) * 50 + 1;
      const hi = page * 50;
      log(`ranking page ${page}  ranks ${lo}–${hi}`);
      let users: number[];
      try {
        users = await this.rankingUserIds(page, stream.variant);
      } catch (err) {
        log(`rankings page ${page}: ${err instanceof Error ? err.message : String(err)}`);
        continue;
      }
      for (const userId of users.slice(0, stream.usersPerPage)) {
        if (saved >= stream.maxReplays)
          return saved;
        try {
          const doc = await this.osu.getUserScores(userId, "recent", {
            mode: "mania",
            limit: 20,
            include_fails: stream.includeFails ? 1 : 0,
          });
          for (const s of asArray(doc)) {
            const parsed = parseV2Score(s, 0);
            if (!parsed || !usableScore(parsed))
              continue;
            parsed.source = "ranking-recent";
            parsed.userGlobalRank = page * 50;
            const rec = asRecord(s);
            const bm = rec ? asRecord(rec.beatmap) : null;
            if (bm)
              parsed.stars = num(bm, "difficulty_rating");
            if (await this.trySaveScore(parsed, run, log)) {
              saved++;
              break;
            }
          }
        } catch (err) {
          log(`recent ${userId}: ${err instanceof Error ? err.message : String(err)}`);
        }
      }
    }
    return saved;
  }

  private async fetchUserBest(stream: UserBestStream, run: FetchRun, log: (line: string) => void): Promise<number> {
    const mine = await this.fetchMyScores(stream.limit, log);
    let saved = 0;
    for (const score of mine) {
      score.source = "user-best";
      saved += (await this.trySaveScore(score, run, log)) ? 1 : 0;
    }
    return saved;
  }

  private async searchRankedManiaMaps(search: RankedSearch, log: (line: string) => void): Promise<MapRef[]> {
    log(`filter  keys=${search.keys.join(",")}  stars>=${search.minStars}  plays>=${search.minPlaycount}  take=${search.take}  sample=${search.sample}  (one set → highest-star match)`);
    const doc = asRecord(await this.osu.searchBeatmapsets({
      m: 3,
      s: search.s ?? "ranked",
      sort: search.sort,
      q: search.q,
    }));
    const found: MapRef[] = [];
    for (const set of asArray(doc?.beatmapsets)) {
      const setObj = asRecord(set);
      if (!setObj)
        continue;
      let best: MapRef | undefined;
      for (const b of asArray(setObj.beatmaps)) {
        const bm = asRecord(b);
        if (!bm)
          continue;
        if (str(bm, "mode") !== "mania" || bm.convert === true)
          continue;
        const keys = Math.round(num(bm, "cs"));
        if (!search.keys.includes(keys))
          continue;
        const stars = num(bm, "difficulty_rating");
        if (stars < search.minStars) 
          continue;
        if (num(bm, "playcount") < search.minPlaycount)
          continue;
        const id = int(bm, "id") ?? 0;
        if (id <= 0)
          continue;
        if (!best || stars > best.stars)
          best = { id, stars, keys };
      }
      if (best)
        found.push(best);
    }
    const picked = search.sample === "random" ? shuffle(found).slice(0, search.take) : found.slice(0, search.take);
    log(`search  ${picked.length}/${found.length} maps from this search page`);
    return picked;
  }

  private async fetchLeaderboardScores(beatmapId: number, stars: number, pick: LeaderboardPick, run: FetchRun, log: (line: string) => void): Promise<number> {
    const all = await this.listScores(beatmapId, log);
    const picked = pickScores(all, pick);
    log(`scores  #${beatmapId}  pool ${pick.rankFrom}–${pick.rankTo}  ${picked.length}/${all.length} usable`);
    let saved = 0;
    for (const score of picked) {
      score.stars = stars;
      score.source = "leaderboard";
      if (await this.trySaveScore(score, run, log))
        saved++;
    }
    return saved;
  }

  private async rankingUserIds(page: number, variant: "4k" | "7k"): Promise<number[]> {
    const doc = asRecord(await this.osu.getRankings("mania", "performance", { page, variant }));
    const ids: number[] = [];
    for (const row of asArray(doc?.ranking)) {
      const rec = asRecord(row);
      const user = rec ? asRecord(rec.user) : null;
      if (!user)
        continue;
      const id = int(user, "id");
      if (id && id > 0)
        ids.push(id);
    }
    return ids;
  }

  private async listScores(beatmapId: number, log: (line: string) => void): Promise<ScoreRef[]> {
    const list: ScoreRef[] = [];
    try {
      const doc = asRecord(await this.osu.getBeatmapScores(beatmapId, { mode: "mania", type: "global" }));
      let place = 0;
      for (const s of asArray(doc?.scores)) {
        const parsed = parseV2Score(s, beatmapId);
        if (!parsed)
          continue;
        place++;
        parsed.leaderboardRank = place;
        if (usableScore(parsed))
          list.push(parsed);
      }
    } catch (err) {
      log(`v2 scores ${beatmapId}: ${err instanceof Error ? err.message : String(err)}`);
    }
    return list;
  }

  private async fetchMyScores(take: number, log: (line: string) => void): Promise<ScoreRef[]> {
    await this.auth.ensureToken(true);
    if (!this.auth.hasUserToken)
      throw new Error("user-best needs a user token (Log in).");
    const me = asRecord(await this.osu.getMe());
    const userId = me ? int(me, "id") : undefined;
    if (!userId)
      throw new Error("could not read /me");
    log(`user    ${userId}`);
    const doc = await this.osu.getUserScores(userId, "best", { mode: "mania", limit: Math.min(100, take) }, true);
    const list: ScoreRef[] = [];
    for (const s of asArray(doc)) {
      const parsed = parseV2Score(s, 0);
      if (parsed && usableScore(parsed))
        list.push(parsed);
      if (list.length >= take)
        break;
    }
    return list;
  }

  private async trySaveScore(score: ScoreRef, run: FetchRun, log: (line: string) => void): Promise<boolean> {
    const file = `${score.id}-${safe(score.player)}.osr`;
    const dest = join(replayDir, file);
    if (existsSync(dest)) {
      this.attachToRun(run, file, score.beatmapId);
      return true;
    }

    let bytes: Uint8Array | null = null;
    try {
      bytes = await this.downloadV2Replay(score.id);
    } catch (err) {
      if (err instanceof Error && err.message.includes("user token"))
        throw err;
      log(`v2 replay ${score.id}: ${err instanceof Error ? err.message : String(err)}`);
    }
    if (!bytes || bytes.length < 16)
      return false;

    writeFileSync(dest, bytes);
    this.manifest.set(score.id, {
      leaderboardRank: score.leaderboardRank,
      accuracy: score.accuracy,
      stars: score.stars,
      source: score.source,
      userGlobalRank: score.userGlobalRank,
      beatmapId: score.beatmapId,
      player: score.player,
    });
    this.attachToRun(run, file, score.beatmapId);
    if (score.beatmapId > 0)
      await this.downloadBeatmap(score.beatmapId, log);
    const rank = score.leaderboardRank?.toString() ?? "-";
    log(`  saved ${file}  ${score.player}  ${(score.accuracy * 100).toFixed(1)}%  rank=${rank}  ${score.source}  ${score.mods.join("")}`);
    return true;
  }

  private async downloadV2Replay(scoreId: number): Promise<Uint8Array | null> {
    const { status, bytes } = await this.osu.downloadScore(scoreId);
    if (status === 401 || status === 403) {
      if (!this.auth.hasUserToken)
        throw new Error("Replay download needs a user token. Use Log in after creating an OAuth app.");
      return null;
    }
    return bytes;
  }

  private async downloadBeatmap(beatmapId: number, log: (line: string) => void): Promise<OsuMeta | undefined> {
    const urls = [
      `https://osu.direct/api/osu/${beatmapId}`,
      `https://catboy.best/osu/${beatmapId}`,
    ];
    for (const url of urls) {
      try {
        const { bytes } = await this.osu.getBytes(url, { authorize: false, accept: "*/*" });
        if (!bytes || bytes.length < 32 || !looksLikeOsu(bytes))
          continue;
        const map = parseOsuMeta(bytes);
        if (!map.isMania)
          continue;
        const path = join(beatmapDir, `${map.md5}.osu`);
        writeFileSync(path, bytes);
        this.beatmaps.set(map.md5.toLowerCase(), map);
        return map;
      } catch (err) {
        log(`beatmap ${beatmapId} via ${url}: ${err instanceof Error ? err.message : String(err)}`);
      }
    }
    return undefined;
  }
}

function isFetchRun(value: unknown): value is FetchRun {
  if (!value || typeof value !== "object")
    return false;
  const row = value as Partial<FetchRun>;
  return typeof row.id === "string"
    && typeof row.at === "string"
    && typeof row.label === "string"
    && Array.isArray(row.files)
    && Array.isArray(row.beatmapIds)
    && row.stream != null
    && typeof (row.stream as FetchStream).type === "string";
}

function parseV2Score(raw: unknown, beatmapId: number): ScoreRef | null {
  const s = asRecord(raw);
  if (!s)
    return null;
  if (s.has_replay === false)
    return null;
  const id = int(s, "id") ?? 0;
  if (id === 0)
    return null;
  const mods: string[] = [];
  for (const m of asArray(s.mods)) {
    const rec = asRecord(m);
    if (rec && str(rec, "acronym"))
      mods.push(str(rec, "acronym"));
  }
  let bid = beatmapId;
  const bid2 = int(s, "beatmap_id");
  if (bid2 && bid2 > 0)
    bid = bid2;
  else {
    const be = asRecord(s.beatmap);
    const bid3 = be ? int(be, "id") : undefined;
    if (bid3 && bid3 > 0)
      bid = bid3;
  }
  const user = asRecord(s.user);
  return {
    id,
    beatmapId: bid,
    player: user ? str(user, "username") : "",
    accuracy: num(s, "accuracy"),
    mods,
    source: "leaderboard",
    stars: 0,
  };
}

function usableScore(score: ScoreRef): boolean {
  return !score.mods.some((m) => SKIP_MODS.has(m));
}

function pickScores(all: ScoreRef[], pick: LeaderboardPick): ScoreRef[] {
  const pool = all.filter((s) => {
    const rank = s.leaderboardRank ?? 0;
    return rank >= pick.rankFrom && rank <= pick.rankTo;
  });
  if (pool.length === 0 || pick.take <= 0)
    return [];
  if (pick.sample === "even")
    return spread(pool, Math.min(pick.take, pool.length));
  return shuffle(pool).slice(0, Math.min(pick.take, pool.length));
}

function spread(all: ScoreRef[], take: number): ScoreRef[] {
  if (all.length === 0 || take <= 0)
    return [];
  if (all.length <= take)
    return all;
  const picked: ScoreRef[] = [];
  const used = new Set<number>();
  for (let i = 0; i < take; i++) {
    let idx = Math.round(i * (all.length - 1) / (take - 1));
    while (used.has(idx) && idx < all.length - 1)
      idx++;
    used.add(idx);
    picked.push(all[idx]);
  }
  return picked;
}

function shuffle<T>(items: T[]): T[] {
  const a = items.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function safe(name: string): string {
  const cleaned = name.replace(/[<>:"/\\|?*\u0000-\u001f]/g, "_");
  return cleaned.trim() ? cleaned : "unknown";
}
