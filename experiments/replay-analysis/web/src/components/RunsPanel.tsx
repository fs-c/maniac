import type { FetchRun } from "../../../src/shared/types.ts";

export type RunFilter = "all" | "none" | string;

type Props = {
  runs: FetchRun[];
  filter: RunFilter;
  unassignedCount: number;
  presentCount: (run: FetchRun) => number;
  onSelect: (filter: RunFilter) => void;
};

export function RunsPanel({ runs, filter, unassignedCount, presentCount, onSelect }: Props) {
  return (
    <section className="rounded-2xl border border-white/10 bg-zinc-900/50">
      <div className="border-b border-white/10 px-4 py-3">
        <h2 className="text-sm font-semibold text-violet-300">Runs</h2>
        <p className="mt-1 text-xs text-zinc-500">
          Each fetch is a row. Filter the catalog to one search before analysis.
        </p>
      </div>
      <ul className="max-h-[28rem] overflow-auto p-2">
        <li>
          <RunButton
            active={filter === "all"}
            title="All replays"
            detail="No filter"
            onClick={() => onSelect("all")}
          />
        </li>
        {unassignedCount > 0 && (
          <li>
            <RunButton
              active={filter === "none"}
              title="No run"
              detail={`${unassignedCount} dropped-in or leftover`}
              onClick={() => onSelect("none")}
            />
          </li>
        )}
        {runs.map((run, index) => {
          const present = presentCount(run);
          const maps = run.beatmapIds.length;
          return (
            <li key={run.id}>
              <RunButton
                active={filter === run.id}
                title={index === 0 ? `Latest · ${formatWhen(run.at)}` : formatWhen(run.at)}
                detail={`${present}/${run.files.length} replays · ${maps} map${maps === 1 ? "" : "s"}`}
                label={run.label}
                onClick={() => onSelect(run.id)}
              />
            </li>
          );
        })}
        {runs.length === 0 && (
          <li className="px-3 py-4 text-xs text-zinc-500">
            No recorded searches yet. Fetch a batch and it will show up here.
          </li>
        )}
      </ul>
    </section>
  );
}

function RunButton({
  active,
  title,
  detail,
  label,
  onClick,
}: {
  active: boolean;
  title: string;
  detail: string;
  label?: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`w-full rounded-xl px-3 py-2.5 text-left transition ${active ? "bg-violet-500/20 ring-1 ring-violet-400/40" : "hover:bg-white/5"}`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-sm font-medium text-zinc-100">{title}</span>
        <span className="shrink-0 text-[11px] text-zinc-500">{detail}</span>
      </div>
      {label && (
        <p className="mt-1 font-mono text-[11px] leading-4 text-zinc-400" title={label}>
          {label}
        </p>
      )}
    </button>
  );
}

function formatWhen(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime()))
    return iso;
  return date.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
