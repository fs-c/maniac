import { serve } from "@hono/node-server";
import { Hono } from "hono";
import { streamSSE } from "hono/streaming";
import type { FetchRequest, FetchStream, LeaderboardPick, RankedSearch } from "../shared/types.ts";
import { Auth } from "./auth.ts";
import { Catalog } from "./catalog.ts";
import { createJob, getJob } from "./jobs.ts";
import { beatmapDir, dataDir, ensureDataDirs, replayDir } from "./paths.ts";
import { readdirSync } from "node:fs";

ensureDataDirs();
const auth = new Auth();
const catalog = new Catalog(auth);

const app = new Hono();

app.get("/api/status", (c) => {
  const replays = readdirSync(replayDir).filter((f) => f.toLowerCase().endsWith(".osr")).length;
  const beatmaps = readdirSync(beatmapDir).filter((f) => f.toLowerCase().endsWith(".osu")).length;
  return c.json({
    auth: auth.status(),
    replayCount: replays,
    beatmapCount: beatmaps,
    dataDir,
  });
});

app.post("/api/login", async (c) => {
  try {
    const body = (await c.req.json().catch(() => ({}))) as { returnTo?: string };
    const url = await auth.startLogin(body.returnTo ?? c.req.header("origin"));
    return c.json({ authorizeUrl: url, auth: auth.status() });
  } catch (err) {
    return c.json({ error: err instanceof Error ? err.message : String(err), auth: auth.status() }, 400);
  }
});

app.get("/api/catalog", (c) => c.json(catalog.catalog()));

app.get("/api/replays/:file", (c) => {
  const file = c.req.param("file");
  const eventOffset = Number(c.req.query("offset") ?? 0) || 0;
  const eventLimit = Math.min(2000, Math.max(0, Number(c.req.query("limit") ?? 400) || 0));
  const hitOffset = Number(c.req.query("hitsOffset") ?? 0) || 0;
  const hitLimit = Math.min(2000, Math.max(0, Number(c.req.query("hitsLimit") ?? 400) || 0));
  try {
    return c.json(catalog.inspector(file, eventOffset, eventLimit, hitOffset, hitLimit));
  } catch (err) {
    return c.json({ error: err instanceof Error ? err.message : String(err) }, 404);
  }
});

app.post("/api/fetch", async (c) => {
  const body = (await c.req.json().catch(() => ({}))) as Partial<FetchRequest>;
  let req: FetchRequest;
  try {
    req = normalizeFetch(body);
  } catch (err) {
    return c.json({ error: err instanceof Error ? err.message : String(err) }, 400);
  }
  const job = createJob();
  void runFetch(job.id, req);
  return c.json(job.snapshot());
});

app.get("/api/jobs/:id", (c) => {
  const job = getJob(c.req.param("id"));
  if (!job)
    return c.json({ error: "job not found" }, 404);
  return c.json(job.snapshot());
});

app.get("/api/jobs/:id/stream", (c) => {
  const job = getJob(c.req.param("id"));
  if (!job)
    return c.json({ error: "job not found" }, 404);
  return streamSSE(c, async (stream) => {
    for (const line of job.logs)
      await stream.writeSSE({ data: line, event: "log" });
    await stream.writeSSE({ data: JSON.stringify(job.snapshot()), event: "status" });
    if (job.status !== "running") {
      await stream.writeSSE({ data: JSON.stringify(job.snapshot()), event: "done" });
      return;
    }
    await new Promise<void>((resolve) => {
      const onLog = (line: string) => {
        void stream.writeSSE({ data: line, event: "log" });
      };
      const onDone = () => {
        job.off("log", onLog);
        void stream.writeSSE({ data: JSON.stringify(job.snapshot()), event: "done" }).then(() => resolve());
      };
      job.on("log", onLog);
      job.once("done", onDone);
    });
  });
});

function normalizeFetch(body: Partial<FetchRequest>): FetchRequest {
  const stream = body.stream;
  if (!stream || typeof stream !== "object" || !("type" in stream))
    throw new Error("fetch requires stream.type");
  return {
    replace: Boolean(body.replace),
    stream: normalizeStream(stream),
  };
}

function normalizeStream(stream: FetchStream): FetchStream {
  switch (stream.type) {
    case "map-leaderboards": {
      const ids = (stream.beatmapIds ?? []).map(Number).filter((n) => Number.isFinite(n) && n > 0);
      const search = ids.length === 0 && stream.search ? normalizeSearch(stream.search) : undefined;
      if (ids.length === 0 && !search)
        throw new Error("map-leaderboards needs beatmapIds or search");
      return {
        type: "map-leaderboards",
        beatmapIds: ids,
        search,
        scores: normalizeLeaderboardPick(stream.scores),
      };
    }
    case "ranking-recents": {
      const pages = (stream.pages ?? []).map(Number).filter((n) => Number.isFinite(n) && n > 0);
      if (pages.length === 0)
        throw new Error("ranking-recents needs pages");
      return {
        type: "ranking-recents",
        variant: stream.variant === "7k" ? "7k" : "4k",
        pages,
        usersPerPage: clampInt(stream.usersPerPage, 6, 1, 50),
        maxReplays: clampInt(stream.maxReplays, 12, 1, 50),
        includeFails: stream.includeFails !== false,
      };
    }
    case "user-best":
      return {
        type: "user-best",
        limit: clampInt(stream.limit, 20, 1, 100),
      };
    default:
      throw new Error(`unknown stream ${(stream as { type: string }).type}`);
  }
}

function normalizeSearch(search: RankedSearch): RankedSearch {
  const keys = (search.keys ?? [4, 7]).map(Number).filter((k) => k === 4 || k === 7);
  if (keys.length === 0)
    throw new Error("ranked search needs keys 4 and/or 7");
  const q = typeof search.q === "string" ? search.q.trim() : "";
  const s = search.s;
  const category = s === "any" || s === "leaderboard" || s === "qualified" || s === "loved" ? s : "ranked";
  return {
    q: q || undefined,
    s: category,
    keys,
    minStars: Math.max(0, Number(search.minStars) || 0),
    minPlaycount: Math.max(0, Math.trunc(Number(search.minPlaycount) || 0)),
    take: clampInt(search.take, 4, 1, 20),
    sort: typeof search.sort === "string" && search.sort.length > 0 ? search.sort : "difficulty_desc",
    sample: search.sample === "order" ? "order" : "random",
  };
}

function normalizeLeaderboardPick(pick: LeaderboardPick | undefined): LeaderboardPick {
  const rankFrom = clampInt(pick?.rankFrom, 1, 1, 50);
  const rankTo = clampInt(pick?.rankTo, 50, rankFrom, 50);
  return {
    take: clampInt(pick?.take, 8, 1, 50),
    rankFrom,
    rankTo,
    sample: pick?.sample === "even" ? "even" : "random",
  };
}

function clampInt(v: unknown, fallback: number, min: number, max: number): number {
  const n = Number(v);
  if (!Number.isFinite(n))
    return fallback;
  return Math.min(max, Math.max(min, Math.trunc(n)));
}

async function runFetch(id: string, req: FetchRequest): Promise<void> {
  const job = getJob(id);
  if (!job)
    return;
  try {
    job.log("fetch   starting");
    job.saved = await catalog.fetch(req, (line) => job.log(line));
    job.log(`fetched ${job.saved} replays`);
    job.finish(true);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    job.log(message);
    job.finish(false, message);
  }
}

const port = Number(process.env.PORT ?? 8787);
serve({ fetch: app.fetch, port, hostname: "127.0.0.1" }, (info) => {
  console.log(`replay-analysis api  http://127.0.0.1:${info.port}`);
  console.log(`data                 ${dataDir}`);
});
