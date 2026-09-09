import { useCallback, useEffect, useState } from "react";
import type {
  AuthStatus,
  CatalogReplay,
  FetchRequest,
  FetchRun,
  ReplayInspector,
} from "../../src/shared/types.ts";
import {
  followJob,
  loadCatalog,
  loadReplay,
  startFetch,
  startLogin,
} from "./api.ts";
import { CatalogTable } from "./components/CatalogTable.tsx";
import { FetchPanel } from "./components/FetchPanel.tsx";
import { InspectorPanel } from "./components/InspectorPanel.tsx";
import { RunsPanel, type RunFilter } from "./components/RunsPanel.tsx";
import { TopBar } from "./components/TopBar.tsx";

type Banner = { kind: "ok" | "error"; text: string };

export function App() {
  const [auth, setAuth] = useState<AuthStatus>();
  const [replays, setReplays] = useState<CatalogReplay[]>([]);
  const [runs, setRuns] = useState<FetchRun[]>([]);
  const [runFilter, setRunFilter] = useState<RunFilter>("all");
  const [replayCount, setReplayCount] = useState(0);
  const [beatmapCount, setBeatmapCount] = useState(0);
  const [logs, setLogs] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [loggingIn, setLoggingIn] = useState(false);
  const [banner, setBanner] = useState<Banner>();
  const [selected, setSelected] = useState<string>();
  const [inspector, setInspector] = useState<ReplayInspector>();
  const [loadingMore, setLoadingMore] = useState(false);

  const refresh = useCallback(async () => {
    const catalog = await loadCatalog();
    setAuth(catalog.auth);
    setReplays(catalog.replays);
    setRuns(catalog.runs);
    setReplayCount(catalog.replayCount);
    setBeatmapCount(catalog.beatmapCount);
    return catalog;
  }, []);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const login = params.get("login");
    if (login === "ok")
      setBanner({
        kind: "ok",
        text: "Signed in. Replay downloads are enabled.",
      });
    else if (login === "error")
      setBanner({
        kind: "error",
        text: params.get("reason") || "Login failed.",
      });
    if (login) {
      const url = new URL(window.location.href);
      url.searchParams.delete("login");
      url.searchParams.delete("reason");
      window.history.replaceState({}, "", url.pathname + url.search);
    }
    void refresh().catch((err) =>
      setBanner({
        kind: "error",
        text: err instanceof Error ? err.message : String(err),
      }),
    );
  }, [refresh]);

  useEffect(() => {
    if (runFilter === "all" || runFilter === "none")
      return;
    if (!runs.some((run) => run.id === runFilter))
      setRunFilter("all");
  }, [runs, runFilter]);

  async function login() {
    setLoggingIn(true);
    try {
      const { authorizeUrl } = await startLogin(`${window.location.origin}/`);
      window.location.assign(authorizeUrl);
    } catch (err) {
      setLoggingIn(false);
      setBanner({
        kind: "error",
        text: err instanceof Error ? err.message : String(err),
      });
    }
  }

  async function fetchReplays(req: FetchRequest) {
    setBusy(true);
    setLogs([]);
    try {
      const job = await startFetch(req);
      await followJob(job.id, (line) => setLogs((prev) => [...prev, line]));
      const catalog = await refresh();
      if (catalog.runs[0])
        setRunFilter(catalog.runs[0].id);
    } catch (err) {
      setLogs((prev) => [
        ...prev,
        err instanceof Error ? err.message : String(err),
      ]);
      const catalog = await refresh().catch(() => undefined);
      if (catalog?.runs[0])
        setRunFilter(catalog.runs[0].id);
    } finally {
      setBusy(false);
    }
  }

  async function openInspector(file: string) {
    setSelected(file);
    const data = await loadReplay(file, 0, 400);
    setInspector(data);
  }

  async function loadMore() {
    if (!selected || !inspector) return;
    setLoadingMore(true);
    try {
      const next = await loadReplay(selected, inspector.events.length, 400);
      setInspector({
        ...inspector,
        events: inspector.events.concat(next.events),
        eventTotal: next.eventTotal,
      });
    } finally {
      setLoadingMore(false);
    }
  }

  const visible = filterReplays(replays, runFilter);
  const gridCols = inspector
    ? "xl:grid-cols-[18rem_minmax(0,1fr)_22rem]"
    : "xl:grid-cols-[18rem_minmax(0,1fr)]";

  return (
    <div className="min-h-screen">
      <TopBar
        auth={auth}
        replayCount={replayCount}
        beatmapCount={beatmapCount}
        loggingIn={loggingIn}
        onLogin={() => void login()}
      />
      <main className="mx-auto max-w-7xl space-y-6 px-4 py-6 sm:px-6">
        {banner && (
          <div
            className={`rounded-xl px-4 py-3 text-sm ${banner.kind === "ok" ? "border border-emerald-500/20 bg-emerald-500/10 text-emerald-200" : "border border-rose-500/20 bg-rose-500/10 text-rose-200"}`}
          >
            {banner.text}
          </div>
        )}
        <FetchPanel
          busy={busy}
          needsLogin={Boolean(auth?.hasV2 && !auth.hasUserToken)}
          logs={logs}
          onFetch={(req) => void fetchReplays(req)}
          onRefresh={() => void refresh()}
        />
        <section className={`grid items-start gap-4 ${gridCols}`}>
          <RunsPanel
            runs={runs}
            filter={runFilter}
            unassignedCount={replays.filter((row) => row.runIds.length === 0).length}
            presentCount={(run) => replays.filter((row) => run.files.includes(row.file)).length}
            onSelect={setRunFilter}
          />
          <div>
            <div className="mb-3 flex items-end justify-between gap-3">
              <h2 className="text-sm font-semibold text-violet-300">Catalog</h2>
              <p className="text-xs text-zinc-500">
                {catalogHint(visible, runFilter, runs)}
              </p>
            </div>
            <CatalogTable
              replays={visible}
              selected={selected}
              onSelect={(file) => void openInspector(file)}
            />
          </div>
          {inspector && (
            <InspectorPanel
              data={inspector}
              loadingMore={loadingMore}
              onClose={() => {
                setInspector(undefined);
                setSelected(undefined);
              }}
              onMore={() => void loadMore()}
            />
          )}
        </section>
      </main>
    </div>
  );
}

function filterReplays(replays: CatalogReplay[], filter: RunFilter): CatalogReplay[] {
  if (filter === "all")
    return replays;
  if (filter === "none")
    return replays.filter((row) => row.runIds.length === 0);
  return replays.filter((row) => row.runIds.includes(filter));
}

function catalogHint(visible: CatalogReplay[], filter: RunFilter, runs: FetchRun[]): string {
  const maps = new Set(visible.map((row) => row.beatmapMd5).filter(Boolean)).size;
  const counts = `${visible.length} replay${visible.length === 1 ? "" : "s"} · ${maps} map${maps === 1 ? "" : "s"}`;
  if (filter === "all")
    return `${counts} · select a run to subset`;
  if (filter === "none")
    return `${counts} · not tied to a fetch`;
  const run = runs.find((row) => row.id === filter);
  if (run)
    return `${counts} · this search`;
  return counts;
}
