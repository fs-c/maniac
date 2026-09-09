import { useEffect, useRef, useState } from "react";
import type { FetchRequest, FetchStream } from "../../../src/shared/types.ts";

type Props = {
  busy: boolean;
  needsLogin: boolean;
  logs: string[];
  onFetch: (req: FetchRequest) => void;
  onRefresh: () => void;
};

type MapSource = "search" | "ids";

export function FetchPanel({ busy, needsLogin, logs, onFetch, onRefresh }: Props) {
  const [replace, setReplace] = useState(false);
  const logRef = useRef<HTMLPreElement>(null);

  const [mapSource, setMapSource] = useState<MapSource>("search");
  const [beatmapIds, setBeatmapIds] = useState("");
  const [key4, setKey4] = useState(true);
  const [key7, setKey7] = useState(true);
  const [minStars, setMinStars] = useState(6);
  const [minPlaycount, setMinPlaycount] = useState(3000);
  const [mapTake, setMapTake] = useState(4);
  const [mapSort, setMapSort] = useState("difficulty_desc");
  const [mapStatus, setMapStatus] = useState<"ranked" | "loved" | "qualified" | "leaderboard">("ranked");
  const [mapQ, setMapQ] = useState("");
  const [mapRandom, setMapRandom] = useState(true);

  const [scoreTake, setScoreTake] = useState(8);
  const [rankFrom, setRankFrom] = useState(1);
  const [rankTo, setRankTo] = useState(50);
  const [scoreRandom, setScoreRandom] = useState(true);

  const [variant, setVariant] = useState<"4k" | "7k">("4k");
  const [pages, setPages] = useState("50, 150, 400");
  const [usersPerPage, setUsersPerPage] = useState(6);
  const [maxReplays, setMaxReplays] = useState(12);
  const [includeFails, setIncludeFails] = useState(true);

  useEffect(() => {
    const el = logRef.current;
    if (el)
      el.scrollTop = el.scrollHeight;
  }, [logs]);

  function run(stream: FetchStream) {
    onFetch({ replace, stream });
  }

  function runMapsThenScores() {
    const ids = beatmapIds.split(/[\s,]+/).map(Number).filter((n) => Number.isFinite(n) && n > 0);
    const keys = [...(key4 ? [4] : []), ...(key7 ? [7] : [])];
    run({
      type: "map-leaderboards",
      beatmapIds: mapSource === "ids" ? ids : [],
      search: mapSource === "search"
        ? {
            q: mapQ.trim() || undefined,
            s: mapStatus,
            keys,
            minStars,
            minPlaycount,
            take: mapTake,
            sort: mapSort,
            sample: mapRandom ? "random" : "order",
          }
        : undefined,
      scores: {
        take: scoreTake,
        rankFrom,
        rankTo,
        sample: scoreRandom ? "random" : "even",
      },
    });
  }

  function runRecents() {
    const parsed = pages.split(",").map((s) => Number(s.trim())).filter((n) => Number.isFinite(n) && n > 0);
    run({
      type: "ranking-recents",
      variant,
      pages: parsed,
      usersPerPage,
      maxReplays,
      includeFails,
    });
  }

  const mapsReady = mapSource === "ids"
    ? beatmapIds.split(/[\s,]+/).some((s) => Number(s) > 0)
    : (key4 || key7) && mapTake > 0;
  const recentsReady = pages.split(",").some((s) => Number(s.trim()) > 0);

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-sm font-semibold text-violet-300">Sources</h2>
        <div className="flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-2 text-sm text-zinc-300">
            <input type="checkbox" checked={replace} onChange={(e) => setReplace(e.target.checked)} className="accent-violet-400" />
            Replace existing .osr
          </label>
          <button type="button" onClick={onRefresh} className="rounded-lg border border-white/10 px-3 py-1.5 text-sm text-zinc-200 hover:bg-white/5">
            Refresh catalog
          </button>
        </div>
      </div>

      {needsLogin && (
        <p className="rounded-lg border border-amber-500/20 bg-amber-500/10 px-3 py-2 text-sm text-amber-200">
          Replay download needs a user token. Listing endpoints work with client credentials.
        </p>
      )}

      <article className="rounded-2xl border border-white/10 bg-zinc-900/50 p-4 sm:p-5">
        <h3 className="text-sm font-semibold text-zinc-100">Maps → scores</h3>
        <p className="mt-1 text-sm text-zinc-400">
          Take N beatmaps, then for each take M scores from that map’s global leaderboard (API cap 50).
        </p>

        <div className="mt-4 grid gap-4 lg:grid-cols-2">
          <div className="rounded-xl border border-white/10 bg-zinc-950/40 p-4">
            <h4 className="text-xs font-semibold uppercase tracking-wide text-zinc-500">1. Beatmaps</h4>
            <div className="mt-3 flex gap-4 text-sm text-zinc-200">
              <label className="flex items-center gap-2">
                <input type="radio" name="map-source" checked={mapSource === "search"} onChange={() => setMapSource("search")} className="accent-violet-400" />
                Search
              </label>
              <label className="flex items-center gap-2">
                <input type="radio" name="map-source" checked={mapSource === "ids"} onChange={() => setMapSource("ids")} className="accent-violet-400" />
                Beatmap IDs
              </label>
            </div>

            {mapSource === "search" ? (
              <div className="mt-3 space-y-3">
                <p className="font-mono text-[11px] text-zinc-500">GET /beatmapsets/search?m=3 then client-side CS/stars/plays filter. One set → highest-star match. First search page only.</p>
                <div className="grid grid-cols-2 gap-2">
                  <NumberField label="N maps" value={mapTake} min={1} max={20} onChange={setMapTake} />
                  <label className="mt-3 flex items-center gap-2 text-sm text-zinc-200">
                    <input type="checkbox" checked={mapRandom} onChange={(e) => setMapRandom(e.target.checked)} className="accent-violet-400" />
                    Sample randomly
                  </label>
                  <label className="block text-xs text-zinc-400">
                    s
                    <select value={mapStatus} onChange={(e) => setMapStatus(e.target.value as typeof mapStatus)} className="mt-1 w-full rounded-lg border border-white/10 bg-zinc-950 px-2.5 py-2 text-sm text-zinc-100">
                      <option value="ranked">ranked</option>
                      <option value="loved">loved</option>
                      <option value="qualified">qualified</option>
                      <option value="leaderboard">leaderboard</option>
                    </select>
                  </label>
                  <label className="block text-xs text-zinc-400">
                    sort
                    <select value={mapSort} onChange={(e) => setMapSort(e.target.value)} className="mt-1 w-full rounded-lg border border-white/10 bg-zinc-950 px-2.5 py-2 text-sm text-zinc-100">
                      <option value="difficulty_desc">difficulty_desc</option>
                      <option value="plays_desc">plays_desc</option>
                      <option value="ranked_desc">ranked_desc</option>
                      <option value="rating_desc">rating_desc</option>
                    </select>
                  </label>
                  <NumberField label="Min stars" value={minStars} min={0} max={12} step={0.1} onChange={setMinStars} />
                  <NumberField label="Min playcount" value={minPlaycount} min={0} max={1_000_000} onChange={setMinPlaycount} />
                </div>
                <div className="flex gap-4 text-sm text-zinc-200">
                  <label className="flex items-center gap-1.5"><input type="checkbox" checked={key4} onChange={(e) => setKey4(e.target.checked)} className="accent-violet-400" /> CS=4</label>
                  <label className="flex items-center gap-1.5"><input type="checkbox" checked={key7} onChange={(e) => setKey7(e.target.checked)} className="accent-violet-400" /> CS=7</label>
                </div>
                <label className="block text-xs text-zinc-400">
                  q (optional)
                  <input
                    value={mapQ}
                    onChange={(e) => setMapQ(e.target.value)}
                    placeholder='e.g. stars>=6 keys=4'
                    className="mt-1 w-full rounded-lg border border-white/10 bg-zinc-950 px-2.5 py-2 font-mono text-sm text-zinc-100 placeholder:text-zinc-600"
                  />
                </label>
              </div>
            ) : (
              <label className="mt-3 block text-xs text-zinc-400">
                IDs
                <textarea
                  value={beatmapIds}
                  onChange={(e) => setBeatmapIds(e.target.value)}
                  placeholder="id, id, …"
                  rows={4}
                  className="mt-1 w-full rounded-lg border border-white/10 bg-zinc-950 px-2.5 py-2 font-mono text-sm text-zinc-100 placeholder:text-zinc-600"
                />
              </label>
            )}
          </div>

          <div className="rounded-xl border border-white/10 bg-zinc-950/40 p-4">
            <h4 className="text-xs font-semibold uppercase tracking-wide text-zinc-500">2. Scores per map</h4>
            <p className="mt-2 font-mono text-[11px] text-zinc-500">GET /beatmaps/{"{id}"}/scores?mode=mania&type=global</p>
            <p className="mt-2 text-xs text-zinc-400">
              Draw M scores uniformly at random (or evenly spaced) from leaderboard places <span className="text-zinc-200">rankFrom–rankTo</span> among the returned top 50. Unusable mods are dropped first; place is the API order.
            </p>
            <div className="grid grid-cols-2 gap-2">
              <NumberField label="M scores" value={scoreTake} min={1} max={50} onChange={setScoreTake} />
              <label className="mt-3 flex items-center gap-2 text-sm text-zinc-200">
                <input type="checkbox" checked={scoreRandom} onChange={(e) => setScoreRandom(e.target.checked)} className="accent-violet-400" />
                Sample randomly
              </label>
              <NumberField label="Rank from" value={rankFrom} min={1} max={50} onChange={setRankFrom} />
              <NumberField label="Rank to" value={rankTo} min={1} max={50} onChange={setRankTo} />
            </div>
          </div>
        </div>

        <Run busy={busy} disabled={!mapsReady} onClick={runMapsThenScores} />
      </article>

      <article className="flex flex-col rounded-2xl border border-white/10 bg-zinc-900/50 p-4">
        <h3 className="text-sm font-semibold text-zinc-100">Ranking recents</h3>
        <p className="mt-1 font-mono text-[11px] leading-4 text-zinc-500">
          GET /rankings/mania/performance?page=P&variant={variant}
        </p>
        <p className="mt-2 text-xs text-zinc-400">
          50 users per page: page P is ranks <span className="text-zinc-200">(P−1)×50+1 … P×50</span>. For each of the first N users on that page, take one usable mania recent.
        </p>
        <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
          <label className="block text-xs text-zinc-400">
            Variant
            <select value={variant} onChange={(e) => setVariant(e.target.value as "4k" | "7k")} className="mt-1 w-full rounded-lg border border-white/10 bg-zinc-950 px-2.5 py-2 text-sm text-zinc-100">
              <option value="4k">4k</option>
              <option value="7k">7k</option>
            </select>
          </label>
          <NumberField label="Users / page" value={usersPerPage} min={1} max={50} onChange={setUsersPerPage} />
          <NumberField label="Max replays" value={maxReplays} min={1} max={50} onChange={setMaxReplays} />
          <label className="mt-5 flex items-center gap-2 text-sm text-zinc-200">
            <input type="checkbox" checked={includeFails} onChange={(e) => setIncludeFails(e.target.checked)} className="accent-violet-400" />
            include_fails
          </label>
        </div>
        <label className="mt-3 block text-xs text-zinc-400">
          Pages
          <input value={pages} onChange={(e) => setPages(e.target.value)} className="mt-1 w-full rounded-lg border border-white/10 bg-zinc-950 px-2.5 py-2 font-mono text-sm text-zinc-100" />
        </label>
        <Run busy={busy} disabled={!recentsReady} onClick={runRecents} />
      </article>

      <pre ref={logRef} className="max-h-56 overflow-auto rounded-2xl border border-white/10 bg-zinc-950 px-3 py-3 font-mono text-xs leading-5 text-zinc-400 whitespace-pre-wrap">
        {logs.length > 0 ? logs.join("\n") : "Job output."}
      </pre>
    </section>
  );
}

function Run({ busy, disabled, onClick }: { busy: boolean; disabled?: boolean; onClick: () => void }) {
  return (
    <div className="pt-4">
      <button
        type="button"
        disabled={busy || disabled}
        onClick={onClick}
        className="rounded-lg bg-violet-400 px-3 py-1.5 text-sm font-semibold text-zinc-950 hover:bg-violet-300 disabled:cursor-not-allowed disabled:opacity-40"
      >
        {busy ? "Running…" : "Run"}
      </button>
    </div>
  );
}

function NumberField({
  label,
  value,
  min,
  max,
  step,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  onChange: (n: number) => void;
}) {
  return (
    <label className="mt-3 block text-xs text-zinc-400">
      {label}
      <input
        type="number"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        className="mt-1 w-full rounded-lg border border-white/10 bg-zinc-950 px-2.5 py-2 text-sm text-zinc-100"
      />
    </label>
  );
}
