import type { CatalogReplay } from "../../../src/shared/types.ts";

type Props = {
  replays: CatalogReplay[];
  selected?: string;
  onSelect: (file: string) => void;
};

export function CatalogTable({ replays, selected, onSelect }: Props) {
  if (replays.length === 0) {
    return (
      <div className="rounded-2xl border border-dashed border-white/15 px-6 py-16 text-center">
        <p className="text-sm font-medium text-zinc-200">No replays yet</p>
        <p className="mt-1 text-sm text-zinc-500">Fetch a batch, or copy <code className="text-zinc-300">.osr</code> files into the data folder and refresh.</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-2xl border border-white/10">
      <table className="min-w-full text-left text-sm">
        <thead className="bg-zinc-900/80 text-xs uppercase tracking-wide text-zinc-500">
          <tr>
            <th className="px-3 py-2 font-medium">Player</th>
            <th className="px-3 py-2 font-medium">Map</th>
            <th className="hidden px-3 py-2 font-medium sm:table-cell">Src</th>
            <th className="hidden px-3 py-2 font-medium lg:table-cell">#</th>
            <th className="hidden px-3 py-2 font-medium md:table-cell">*</th>
            <th className="hidden px-3 py-2 font-medium lg:table-cell">Acc</th>
            <th className="hidden px-3 py-2 font-medium md:table-cell">Mods</th>
            <th className="hidden px-3 py-2 font-medium xl:table-cell">Client</th>
            <th className="hidden px-3 py-2 font-medium lg:table-cell">Frames</th>
            <th className="px-3 py-2 font-medium">Ok</th>
          </tr>
        </thead>
        <tbody>
          {replays.map((row) => {
            const active = row.file === selected;
            return (
              <tr
                key={row.file}
                tabIndex={0}
                onClick={() => onSelect(row.file)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    onSelect(row.file);
                  }
                }}
                className={`cursor-pointer border-t border-white/5 tabular-nums ${active ? "bg-violet-500/15" : "hover:bg-white/5"}`}
              >
                <td className="px-3 py-2 whitespace-nowrap text-zinc-100">{row.player || "—"}</td>
                <td className="max-w-[12rem] truncate px-3 py-2 text-zinc-300 sm:max-w-[18rem]" title={row.map ?? row.beatmapMd5}>
                  {(row.map ?? row.beatmapMd5) || "—"}
                </td>
                <td className="hidden px-3 py-2 text-zinc-400 sm:table-cell">{row.source}</td>
                <td className="hidden px-3 py-2 text-zinc-400 lg:table-cell">{row.leaderboardRank ?? ""}</td>
                <td className="hidden px-3 py-2 text-zinc-400 md:table-cell">{row.stars ? row.stars.toFixed(1) : ""}</td>
                <td className="hidden px-3 py-2 text-zinc-400 lg:table-cell">{row.accuracy ? `${(row.accuracy * 100).toFixed(1)}%` : ""}</td>
                <td className="hidden px-3 py-2 text-zinc-400 md:table-cell">{row.mods}</td>
                <td className="hidden px-3 py-2 text-zinc-500 xl:table-cell">{row.isLazer ? "lazer" : "stable"}</td>
                <td className="hidden px-3 py-2 text-zinc-400 lg:table-cell">{row.frameCount ?? "—"}</td>
                <td className={`px-3 py-2 ${row.parseOk ? "text-emerald-400" : "text-rose-400"}`}>
                  {row.parseOk ? "ok" : (row.parseError ?? "fail")}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
