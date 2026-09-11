import { describe, expect, it } from "vitest";
import { hitWindows, matchNotes, summarizeMatch } from "./match.ts";
import { columnFromX, GameMode, parseOsu, type KeyEvent, type OsrReplay } from "./osr.ts";

const SAMPLE_OSU = [
  "osu file format v14",
  "[General]",
  "Mode: 3",
  "[Metadata]",
  "Title:test",
  "Artist:a",
  "Version:x",
  "[Difficulty]",
  "CircleSize:4",
  "OverallDifficulty:8",
  "[HitObjects]",
  "64,192,1000,1,0,0:0:0:0:",
  "192,192,1000,1,0,0:0:0:0:",
  "320,192,1200,128,0,1500:0:0:0:0:",
].join("\n");

describe("osu hit objects", () => {
  it("parses columns, taps, and holds", () => {
    const map = parseOsu(new TextEncoder().encode(SAMPLE_OSU));
    expect(map.columns).toBe(4);
    expect(map.isMania).toBe(true);
    expect(map.notes).toHaveLength(3);
    expect(map.notes[0].column).toBe(0);
    expect(map.notes[1].column).toBe(1);
    expect(map.notes[2].hold).toBe(true);
    expect(map.notes[2].end).toBe(1500);
    expect(columnFromX(320, 4)).toBe(2);
  });
});

describe("hit windows", () => {
  it("classic OD8 300 window is 16.5", () => {
    const classic = hitWindows({ od: 8, rate: 1, classic: true, easy: false, hardRock: false, convert: false });
    expect(classic.perfect).toBeGreaterThanOrEqual(16);
    expect(classic.perfect).toBeLessThanOrEqual(17);
    const easy = hitWindows({ od: 0, rate: 1, classic: true, easy: false, hardRock: false, convert: false });
    expect(easy.miss).toBeGreaterThan(classic.miss);
  });
});

describe("note matching", () => {
  const map = parseOsu(new TextEncoder().encode(SAMPLE_OSU));

  it("exact-time presses have UR ~0 and no misses", () => {
    const stats = summarizeMatch(matchNotes(replay([
      { time: 1000, column: 0, down: true },
      { time: 1000, column: 1, down: true },
      { time: 1020, column: 0, down: false },
      { time: 1020, column: 1, down: false },
      { time: 1200, column: 2, down: true },
      { time: 1500, column: 2, down: false },
    ]), map));
    expect(stats.unstableRate ?? 0).toBeLessThan(0.01);
    expect(stats.missedHeads).toBe(0);
    expect(stats.matchedHeads).toBe(3);
  });

  it("offset presses produce error, UR, chord desync, early hold tail", () => {
    const result = matchNotes(replay([
      { time: 1004, column: 0, down: true },
      { time: 1009, column: 1, down: true },
      { time: 1055, column: 0, down: false },
      { time: 1060, column: 1, down: false },
      { time: 1192, column: 2, down: true },
      { time: 1488, column: 2, down: false },
    ]), map);
    const stats = summarizeMatch(result);
    expect(stats.unstableRate ?? 0).toBeGreaterThan(1);
    expect(stats.meanError ?? 0).toBeGreaterThan(0);
    const heads = result.hits.filter((h) => h.kind !== "holdTail" && h.hit);
    const chord = heads.filter((h) => h.note.start === 1000);
    expect(Math.max(...chord.map((h) => h.pressTime)) - Math.min(...chord.map((h) => h.pressTime))).toBeGreaterThanOrEqual(5);
    const tail = result.hits.find((h) => h.kind === "holdTail" && h.hit);
    expect(tail?.error).toBeLessThan(0);
  });

  it("mirrors columns when MR is set", () => {
    const result = matchNotes(replay(
      [{ time: 1000, column: 3, down: true }, { time: 1020, column: 3, down: false }],
      { mods: [{ acronym: "MR" }] },
    ), map);
    const hit = result.hits.find((h) => h.hit && h.kind === "tap");
    expect(hit?.note.column).toBe(3);
    expect(hit?.note.index).toBe(0);
  });
});

function replay(events: KeyEvent[], extra: Partial<OsrReplay> = {}): OsrReplay {
  return {
    mode: GameMode.Mania,
    version: 20140721,
    beatmapMd5: "d".repeat(32),
    player: "t",
    replayMd5: "r".repeat(32),
    count300: 0,
    count100: 0,
    count50: 0,
    countGeki: 0,
    countKatu: 0,
    countMiss: 0,
    totalScore: 0,
    maxCombo: 0,
    perfect: false,
    legacyMods: 0,
    timestamp: new Date(0),
    onlineId: 0,
    frames: [],
    events,
    mods: [],
    ...extra,
  };
}
