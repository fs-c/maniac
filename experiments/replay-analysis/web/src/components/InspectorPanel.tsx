import type { ReplayInspector } from "../../../src/shared/types.ts";

type Props = {
  data: ReplayInspector;
  loadingMore: boolean;
  onClose: () => void;
  onMore: () => void;
};

export function InspectorPanel({ data, loadingMore, onClose, onMore }: Props) {
  const more = data.events.length < data.eventTotal;
  return (
    <aside className="flex max-h-[70vh] flex-col rounded-2xl border border-white/10 bg-zinc-900/50 xl:max-h-[calc(100vh-7rem)]">
      <div className="flex items-start justify-between gap-3 border-b border-white/10 px-4 py-3">
        <div className="min-w-0">
          <h2 className="text-sm font-semibold text-violet-300">Inspector</h2>
          <p className="truncate text-sm text-zinc-200">{data.player}</p>
          <p className="truncate text-xs text-zinc-500">{data.map ?? data.beatmapMd5}</p>
        </div>
        <button type="button" onClick={onClose} className="rounded-lg px-2 py-1 text-sm text-zinc-400 hover:bg-white/5 hover:text-zinc-100">
          Close
        </button>
      </div>
      <div className="grid grid-cols-2 gap-3 px-4 py-3 text-xs sm:grid-cols-3">
        <Meta label="Mods" value={data.mods} />
        <Meta label="Mode" value={`${data.mode} · ${data.isLazer ? "lazer" : "stable"}`} />
        <Meta label="Score" value={String(data.totalScore)} />
        <Meta label="Combo" value={String(data.maxCombo)} />
        <Meta label="300/100/50/miss" value={`${data.count300}/${data.count100}/${data.count50}/${data.countMiss}`} />
        <Meta label="Frames" value={String(data.frameCount)} />
      </div>
      {data.parseError && (
        <p className="px-4 pb-2 text-xs text-rose-300">{data.parseError}</p>
      )}
      <div className="min-h-0 flex-1 overflow-auto border-t border-white/10">
        <table className="w-full text-left text-xs tabular-nums">
          <thead className="sticky top-0 bg-zinc-900 text-zinc-500">
            <tr>
              <th className="px-3 py-2 font-medium">Time</th>
              <th className="px-3 py-2 font-medium">Col</th>
              <th className="px-3 py-2 font-medium">Event</th>
            </tr>
          </thead>
          <tbody>
            {data.events.map((e, i) => (
              <tr key={`${e.time}-${e.column}-${e.down}-${i}`} className="border-t border-white/5">
                <td className="px-3 py-1.5 text-zinc-300">{e.time.toFixed(0)}</td>
                <td className="px-3 py-1.5 text-zinc-300">{e.column}</td>
                <td className="px-3 py-1.5 text-zinc-400">{e.down ? "down" : "up"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="flex items-center justify-between gap-3 border-t border-white/10 px-4 py-3">
        <span className="text-xs text-zinc-500">{data.events.length} / {data.eventTotal} events</span>
        <button
          type="button"
          disabled={!more || loadingMore}
          onClick={onMore}
          className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-zinc-200 hover:bg-white/5 disabled:opacity-40"
        >
          {loadingMore ? "Loading…" : "Load more"}
        </button>
      </div>
    </aside>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="uppercase tracking-wide text-zinc-500">{label}</div>
      <div className="truncate font-medium text-zinc-200" title={value}>{value}</div>
    </div>
  );
}
