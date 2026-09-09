import { describe, expect, it } from "vitest";
import { compressLzma, GameMode, parseFrames, parseOsrBuffer, parseOsuMeta, toKeyEvents } from "./osr.ts";

describe("frame parsing", () => {
  it("turns mania x into key bits and drops intro frames", () => {
    const frames = parseFrames("0|256|-500|0,16|1|0|0,16|3|0|0", GameMode.Mania);
    expect(frames).toEqual([
      { time: 16, keys: 1 },
      { time: 32, keys: 3 },
    ]);
  });

  it("skips seed frames and backward time", () => {
    // deltas still accumulate; a negative frame is dropped, later frames keep that clock
    const frames = parseFrames("-12345|0|0|0,10|1|0|0,-5|1|0|0,10|0|0|0", GameMode.Mania);
    expect(frames.map((f) => f.time)).toEqual([10, 15]);
  });

  it("emits column press/release events", () => {
    const events = toKeyEvents([
      { time: 1000, keys: 0b0101 },
      { time: 1020, keys: 0b0100 },
      { time: 1040, keys: 0 },
    ]);
    expect(events).toEqual([
      { time: 1000, column: 0, down: true },
      { time: 1000, column: 2, down: true },
      { time: 1020, column: 0, down: false },
      { time: 1040, column: 2, down: false },
    ]);
  });
});

describe("osu metadata", () => {
  it("reads mania header fields", () => {
    const osu = [
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
    ].join("\n");
    const map = parseOsuMeta(new TextEncoder().encode(osu));
    expect(map.isMania).toBe(true);
    expect(map.columns).toBe(4);
    expect(map.displayName).toBe("a - test [x]");
    expect(map.md5).toHaveLength(32);
  });
});

describe("osr roundtrip", () => {
  it("parses a minimal mania replay including key events", () => {
    const frames = "16|1|0|0,16|0|0|0";
    const compressed = compressLzma(frames);
    const buf = writeOsr({
      player: "exact",
      beatmapMd5: "d".repeat(32),
      compressed,
    });
    const replay = parseOsrBuffer(buf);
    expect(replay.mode).toBe(GameMode.Mania);
    expect(replay.player).toBe("exact");
    expect(replay.frames.length).toBeGreaterThan(0);
    expect(replay.events.some((e) => e.column === 0 && e.down)).toBe(true);
  });
});

function writeOsr(opts: { player: string; beatmapMd5: string; compressed: Uint8Array }): Uint8Array {
  const parts: number[] = [];
  const pushU8 = (n: number) => parts.push(n & 0xff);
  const pushU16 = (n: number) => {
    parts.push(n & 0xff, (n >> 8) & 0xff);
  };
  const pushI32 = (n: number) => {
    parts.push(n & 0xff, (n >> 8) & 0xff, (n >> 16) & 0xff, (n >> 24) & 0xff);
  };
  const pushI64 = (n: bigint) => {
    const view = new DataView(new ArrayBuffer(8));
    view.setBigInt64(0, n, true);
    parts.push(...new Uint8Array(view.buffer));
  };
  const pushString = (s: string) => {
    if (!s) {
      pushU8(0);
      return;
    }
    const bytes = new TextEncoder().encode(s);
    pushU8(0x0b);
    let len = bytes.length;
    while (len >= 0x80) {
      pushU8((len & 0x7f) | 0x80);
      len >>= 7;
    }
    pushU8(len);
    parts.push(...bytes);
  };

  pushU8(GameMode.Mania);
  pushI32(20140721);
  pushString(opts.beatmapMd5);
  pushString(opts.player);
  pushString("r".repeat(32));
  for (let i = 0; i < 6; i++) pushU16(0);
  pushI32(0);
  pushU16(0);
  pushU8(0);
  pushI32(0);
  pushString("");
  pushI64(0n);
  pushI32(opts.compressed.length);
  parts.push(...opts.compressed);
  pushI64(1n);
  return Uint8Array.from(parts);
}
