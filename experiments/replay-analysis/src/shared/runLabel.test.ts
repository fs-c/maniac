import { describe, expect, it } from "vitest";
import { summarizeFetchStream } from "./runLabel.ts";

describe("summarizeFetchStream", () => {
  it("labels a ranked search", () => {
    expect(summarizeFetchStream({
      type: "map-leaderboards",
      beatmapIds: [],
      search: {
        s: "ranked",
        keys: [4, 7],
        minStars: 6,
        minPlaycount: 3000,
        take: 4,
        sort: "difficulty_desc",
        sample: "random",
        q: "stars>=6",
      },
      scores: { take: 8, rankFrom: 1, rankTo: 50, sample: "random" },
    })).toBe("search ranked ≥6* 4K/7K N=4 random q=stars>=6 → 8 scores #1–50 random");
  });

  it("labels explicit ids", () => {
    expect(summarizeFetchStream({
      type: "map-leaderboards",
      beatmapIds: [1, 2, 3],
      scores: { take: 8, rankFrom: 1, rankTo: 50, sample: "even" },
    })).toBe("ids ×3 → 8 scores #1–50 even");
  });

  it("labels ranking recents", () => {
    expect(summarizeFetchStream({
      type: "ranking-recents",
      variant: "4k",
      pages: [50, 150],
      usersPerPage: 6,
      maxReplays: 12,
      includeFails: true,
    })).toBe("ranking 4k p50,150 ×6 users max 12");
  });
});
