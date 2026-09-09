import { describe, expect, it } from "vitest";
import { buildQuery, osuUrl, RulesetId } from "./osu.ts";

describe("buildQuery", () => {
  it("omits empty values and joins dotted arrays", () => {
    expect(buildQuery({
      q: undefined,
      m: RulesetId.mania,
      s: "ranked",
      sort: "difficulty_desc",
      c: ["converts", "spotlights"],
      nsfw: false,
    })).toBe("?m=3&s=ranked&sort=difficulty_desc&c=converts.spotlights&nsfw=false");
  });

  it("builds a full search URL", () => {
    expect(osuUrl("/beatmapsets/search", { m: 3, s: "ranked", sort: "plays_desc" }))
      .toBe("https://osu.ppy.sh/api/v2/beatmapsets/search?m=3&s=ranked&sort=plays_desc");
  });
});
