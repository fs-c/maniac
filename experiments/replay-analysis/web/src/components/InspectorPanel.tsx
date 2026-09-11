import { useState, type ReactNode } from "react";
import type { Judgement, MatchSummary, NoteHit, ReplayInspector } from "../../../src/shared/types.ts";

type Props = {
  data: ReplayInspector;
  loadingMore: boolean;
  onClose: () => void;
  onMoreEvents: () => void;
  onMoreHits: () => void;
};

type Tab = "hits" | "events";

export function InspectorPanel({ data, loadingMore, onClose, onMoreEvents, onMoreHits }: Props) {
  const [tab, setTab] = useState<Tab>("hits");
  const moreEvents = data.events.length < data.eventTotal;
  const moreHits = data.hits.length < data.hitTotal;

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
      <MatchBlock summary={data.match} error={data.matchError} />
      <div className="flex gap-1 border-t border-white/10 px-3 pt-2">
        <TabButton active={tab === "hits"} onClick={() => setTab("hits")}>
          Hits {data.hitTotal > 0 ? data.hitTotal : ""}
        </TabButton>
        <TabButton active={tab === "events"} onClick={() => setTab("events")}>
          Events {data.eventTotal}
        </TabButton>
      </div>
      <div className="min-h-0 flex-1 overflow-auto">
        {tab === "hits" ? <HitsTable hits={data.hits} /> : <EventsTable events={data.events} />}
      </div>
      <div className="flex items-center justify-between gap-3 border-t border-white/10 px-4 py-3">
        <span className="text-xs text-zinc-500">
          {tab === "hits"
            ? `${data.hits.length} / ${data.hitTotal} hits`
            : `${data.events.length} / ${data.eventTotal} events`}
        </span>
        <button
          type="button"
          disabled={loadingMore || (tab === "hits" ? !moreHits : !moreEvents)}
          onClick={tab === "hits" ? onMoreHits : onMoreEvents}
          className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-zinc-200 hover:bg-white/5 disabled:opacity-40"
        >
          {loadingMore ? "Loading…" : "Load more"}
        </button>
      </div>
    </aside>
  );
}

function MatchBlock({ summary, error }: { summary?: MatchSummary; error?: string }) {
  if (error) {
    return (
      <p className="px-4 pb-3 text-xs text-amber-200">
        Match skipped: {error}
      </p>
    );
  }
  if (!summary)
    return null;
  const w = summary.windows;
  return (
    <div className="space-y-2 px-4 pb-3 text-xs">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <Meta label="Matched" value={`${summary.matchedHeads}/${summary.matchedHeads + summary.missedHeads} heads`} />
        <Meta label="Mean error" value={fmtError(summary.meanError)} />
        <Meta label="UR" value={summary.unstableRate !== undefined ? summary.unstableRate.toFixed(1) : "—"} />
        <Meta label="Ghost taps" value={String(summary.ghostTaps)} />
        <Meta label="Windows" value={`${w.classic ? "classic" : "lazer"} ×${w.rate}`} />
        <Meta label="300 window" value={`±${w.perfect} ms`} />
      </div>
      <p className="text-[11px] text-zinc-500">
        {JUDGEMENT_ORDER.map((j) => `${j} ${summary.judgements[j]}`).join(" · ")}
      </p>
    </div>
  );
}

function HitsTable({ hits }: { hits: NoteHit[] }) {
  if (hits.length === 0) {
    return <p className="px-4 py-6 text-xs text-zinc-500">No matched hits to show.</p>;
  }
  return (
    <table className="w-full text-left text-xs tabular-nums">
      <thead className="sticky top-0 bg-zinc-900 text-zinc-500">
        <tr>
          <th className="px-3 py-2 font-medium">Note</th>
          <th className="px-3 py-2 font-medium">Col</th>
          <th className="px-3 py-2 font-medium">Kind</th>
          <th className="px-3 py-2 font-medium">Error</th>
          <th className="px-3 py-2 font-medium">Judge</th>
        </tr>
      </thead>
      <tbody>
        {hits.map((h, i) => (
          <tr key={`${h.noteIndex}-${h.kind}-${i}`} className="border-t border-white/5">
            <td className="px-3 py-1.5 text-zinc-300">{h.noteStart.toFixed(0)}</td>
            <td className="px-3 py-1.5 text-zinc-300">{h.column}</td>
            <td className="px-3 py-1.5 text-zinc-400">{kindLabel(h.kind)}</td>
            <td className="px-3 py-1.5 text-zinc-300">{h.error === undefined ? "—" : signed(h.error)}</td>
            <td className={`px-3 py-1.5 ${judgementClass(h.judgement)}`}>{h.judgement}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function EventsTable({ events }: { events: ReplayInspector["events"] }) {
  return (
    <table className="w-full text-left text-xs tabular-nums">
      <thead className="sticky top-0 bg-zinc-900 text-zinc-500">
        <tr>
          <th className="px-3 py-2 font-medium">Time</th>
          <th className="px-3 py-2 font-medium">Col</th>
          <th className="px-3 py-2 font-medium">Event</th>
        </tr>
      </thead>
      <tbody>
        {events.map((e, i) => (
          <tr key={`${e.time}-${e.column}-${e.down}-${i}`} className="border-t border-white/5">
            <td className="px-3 py-1.5 text-zinc-300">{e.time.toFixed(0)}</td>
            <td className="px-3 py-1.5 text-zinc-300">{e.column}</td>
            <td className="px-3 py-1.5 text-zinc-400">{e.down ? "down" : "up"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-t-lg px-3 py-1.5 text-xs font-medium ${active ? "bg-white/10 text-zinc-100" : "text-zinc-500 hover:text-zinc-300"}`}
    >
      {children}
    </button>
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

const JUDGEMENT_ORDER: Judgement[] = ["perfect", "great", "good", "ok", "meh", "miss"];

function kindLabel(kind: NoteHit["kind"]): string {
  if (kind === "holdHead")
    return "head";
  if (kind === "holdTail")
    return "tail";
  return "tap";
}

function fmtError(v: number | undefined): string {
  if (v === undefined)
    return "—";
  if (Math.abs(v) < 0.05)
    return "0.0 ms";
  return `${signed(v)} ms`;
}

function signed(v: number): string {
  return `${v > 0 ? "+" : ""}${v.toFixed(1)}`;
}

function judgementClass(j: Judgement): string {
  switch (j) {
    case "perfect": return "text-violet-300";
    case "great": return "text-emerald-400";
    case "good": return "text-sky-400";
    case "ok": return "text-amber-300";
    case "meh": return "text-orange-400";
    case "miss": return "text-rose-400";
  }
}
