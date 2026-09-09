import type { FetchStream } from "./types.ts";

export function summarizeFetchStream(stream: FetchStream): string {
  switch (stream.type) {
    case "map-leaderboards": {
      const scores = `${stream.scores.take} scores #${stream.scores.rankFrom}–${stream.scores.rankTo} ${stream.scores.sample}`;
      if (stream.beatmapIds.length > 0)
        return `ids ×${stream.beatmapIds.length} → ${scores}`;
      const search = stream.search;
      if (!search)
        return `maps → ${scores}`;
      const keys = search.keys.map((k) => `${k}K`).join("/");
      const q = search.q ? ` q=${search.q}` : "";
      return `search ${search.s ?? "ranked"} ≥${search.minStars}* ${keys} N=${search.take} ${search.sample}${q} → ${scores}`;
    }
    case "ranking-recents":
      return `ranking ${stream.variant} p${stream.pages.join(",")} ×${stream.usersPerPage} users max ${stream.maxReplays}`;
    case "user-best":
      return `user-best ${stream.limit}`;
  }
}
