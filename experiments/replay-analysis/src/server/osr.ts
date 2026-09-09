import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";

const lzma = createRequire(import.meta.url)("lzma") as typeof import("lzma");

export const enum GameMode {
  Osu = 0,
  Taiko = 1,
  Catch = 2,
  Mania = 3,
}

export const GameModeName: Record<number, string> = {
  [GameMode.Osu]: "osu",
  [GameMode.Taiko]: "taiko",
  [GameMode.Catch]: "catch",
  [GameMode.Mania]: "mania",
};

export const enum LegacyMods {
  None = 0,
  NoFail = 1 << 0,
  Easy = 1 << 1,
  Hidden = 1 << 3,
  HardRock = 1 << 4,
  SuddenDeath = 1 << 5,
  DoubleTime = 1 << 6,
  Relax = 1 << 7,
  HalfTime = 1 << 8,
  Nightcore = 1 << 9,
  Flashlight = 1 << 10,
  Autoplay = 1 << 11,
  SpunOut = 1 << 12,
  Autopilot = 1 << 13,
  Perfect = 1 << 14,
  Key4 = 1 << 15,
  Key5 = 1 << 16,
  Key6 = 1 << 17,
  Key7 = 1 << 18,
  Key8 = 1 << 19,
  FadeIn = 1 << 20,
  Random = 1 << 21,
  Cinema = 1 << 22,
  Key9 = 1 << 24,
  Coop = 1 << 25,
  Key1 = 1 << 26,
  Key3 = 1 << 27,
  Key2 = 1 << 28,
  ScoreV2 = 1 << 29,
  Mirror = 1 << 30,
}

export type ModInfo = {
  acronym: string;
  speedChange?: number;
};

export type ReplayFrame = {
  time: number;
  keys: number;
};

export type KeyEvent = {
  time: number;
  column: number;
  down: boolean;
};

export type OsrReplay = {
  mode: GameMode;
  version: number;
  beatmapMd5: string;
  player: string;
  replayMd5: string;
  count300: number;
  count100: number;
  count50: number;
  countGeki: number;
  countKatu: number;
  countMiss: number;
  totalScore: number;
  maxCombo: number;
  perfect: boolean;
  legacyMods: number;
  timestamp: Date;
  onlineId: number;
  frames: ReplayFrame[];
  events: KeyEvent[];
  mods: ModInfo[];
  clientVersion?: string;
  sourcePath?: string;
  frameError?: string;
};

export type OsuMeta = {
  title: string;
  artist: string;
  version: string;
  md5: string;
  mode: number;
  circleSize: number;
  overallDifficulty: number;
  beatmapId: number;
  beatmapSetId: number;
  displayName: string;
  columns: number;
  isMania: boolean;
};

const UNUSABLE = new Set(["RD", "AU", "CN", "RX", "AP"]);

export function isLazer(replay: Pick<OsrReplay, "version">): boolean {
  return replay.version >= 30_000_000;
}

export function modsLabel(replay: Pick<OsrReplay, "mods" | "legacyMods">): string {
  if (replay.mods.length > 0)
    return replay.mods.map((m) => m.acronym).join("");
  if (replay.legacyMods === LegacyMods.None)
    return "NM";
  return legacyModsLabel(replay.legacyMods);
}

export function hasAcronym(replay: Pick<OsrReplay, "mods">, acronym: string): boolean {
  return replay.mods.some((m) => m.acronym.toUpperCase() === acronym.toUpperCase());
}

export function isUnusable(replay: Pick<OsrReplay, "mods" | "legacyMods">): boolean {
  if (replay.mods.some((m) => UNUSABLE.has(m.acronym.toUpperCase())))
    return true;
  const mods = replay.legacyMods;
  return (
    (mods & LegacyMods.Random) !== 0
    || (mods & LegacyMods.Autoplay) !== 0
    || (mods & LegacyMods.Cinema) !== 0
    || (mods & LegacyMods.Relax) !== 0
    || (mods & LegacyMods.Autopilot) !== 0
  );
}

export function parseOsr(path: string): OsrReplay {
  return parseOsrBuffer(readFileSync(path), path);
}

export function parseOsrBuffer(buffer: Uint8Array, path?: string): OsrReplay {
  const br = new BinaryCursor(buffer);
  const replay: OsrReplay = {
    mode: br.u8() as GameMode,
    version: br.i32(),
    beatmapMd5: br.osuString(),
    player: br.osuString(),
    replayMd5: br.osuString(),
    count300: br.u16(),
    count100: br.u16(),
    count50: br.u16(),
    countGeki: br.u16(),
    countKatu: br.u16(),
    countMiss: br.u16(),
    totalScore: br.i32(),
    maxCombo: br.u16(),
    perfect: br.u8() !== 0,
    legacyMods: br.i32(),
    frames: [],
    events: [],
    mods: [],
    onlineId: 0,
    timestamp: new Date(0),
    sourcePath: path,
  };
  br.osuString(); // life graph
  const ticks = br.i64();
  replay.timestamp = ticks > 0n ? new Date(Number(ticks / 10000n - 62135596800000n)) : new Date(0);

  const replayLen = br.i32();
  const compressed = replayLen > 0 ? br.bytes(replayLen) : new Uint8Array();
  if (replay.version >= 20140721 && br.remaining >= 8)
    replay.onlineId = Number(br.i64());
  else if (replay.version >= 20121008 && br.remaining >= 4)
    replay.onlineId = br.i32();

  if (replay.version >= 30_000_001 && br.remaining >= 4) {
    const extraLen = br.i32();
    if (extraLen > 0 && br.remaining >= extraLen)
      applyScoreInfo(replay, br.bytes(extraLen));
  }

  replay.mods = replay.mods.length > 0 ? replay.mods : fromLegacy(replay.legacyMods);

  if (compressed.length > 0) {
    try {
      const raw = decompressLzmaSync(compressed);
      replay.frames = parseFrames(raw, replay.mode);
      replay.events = toKeyEvents(replay.frames);
    } catch (err) {
      replay.frameError = err instanceof Error ? err.message : String(err);
    }
  }

  return replay;
}

export function parseOsuMeta(bytes: Uint8Array): OsuMeta {
  const md5 = createHash("md5").update(bytes).digest("hex");
  const text = new TextDecoder("utf-8").decode(bytes);
  let section = "";
  let title = "";
  let artist = "";
  let version = "";
  let mode = 0;
  let circleSize = 4;
  let overallDifficulty = 8;
  let beatmapId = 0;
  let beatmapSetId = 0;

  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith("//"))
      continue;
    if (line.startsWith("[") && line.endsWith("]")) {
      section = line.slice(1, -1);
      continue;
    }
    if (section !== "General" && section !== "Metadata" && section !== "Difficulty")
      continue;
    const colon = line.indexOf(":");
    if (colon <= 0)
      continue;
    const key = line.slice(0, colon).trim();
    const value = line.slice(colon + 1).trim();
    switch (key) {
      case "Mode": mode = Number.parseInt(value, 10) || 0; break;
      case "Title": title = value; break;
      case "Artist": artist = value; break;
      case "Version": version = value; break;
      case "BeatmapID": beatmapId = Number.parseInt(value, 10) || 0; break;
      case "BeatmapSetID": beatmapSetId = Number.parseInt(value, 10) || 0; break;
      case "CircleSize": circleSize = Number.parseFloat(value) || 0; break;
      case "OverallDifficulty": overallDifficulty = Number.parseFloat(value) || 0; break;
    }
  }

  const columns = Math.min(18, Math.max(1, Math.round(circleSize)));
  return {
    title,
    artist,
    version,
    md5,
    mode,
    circleSize,
    overallDifficulty,
    beatmapId,
    beatmapSetId,
    columns,
    isMania: mode === 3,
    displayName: `${artist} - ${title} [${version}]`,
  };
}

export function looksLikeOsu(bytes: Uint8Array): boolean {
  const head = new TextDecoder("utf-8").decode(bytes.slice(0, 24));
  return head.includes("osu file format");
}

export function parseFrames(data: string, mode: GameMode, offset = 0): ReplayFrame[] {
  const frames: ReplayFrame[] = [];
  let time = offset;
  for (const chunk of data.split(",")) {
    if (!chunk)
      continue;
    const p = chunk.split("|");
    if (p.length < 4)
      continue;
    if (p[0] === "-12345")
      continue;
    const delta = parseDelta(p[0]);
    if (delta === undefined)
      continue;
    const x = Number.parseFloat(p[1]);
    const y = Number.parseFloat(p[2]);
    time += delta;
    frames.push({
      time,
      keys: mode === GameMode.Mania ? Math.trunc(x) || 0 : Number.parseInt(p[3], 10) || 0,
    });
    if (frames.length <= 2 && x === 256 && y === -500)
      frames.pop();
  }

  const cleaned: ReplayFrame[] = [];
  let last = Number.NEGATIVE_INFINITY;
  for (const f of frames) {
    if (f.time < last)
      continue;
    cleaned.push(f);
    last = f.time;
  }
  return cleaned;
}

export function toKeyEvents(frames: readonly ReplayFrame[]): KeyEvent[] {
  const events: KeyEvent[] = [];
  let prev = 0;
  for (const frame of frames) {
    const changed = frame.keys ^ prev;
    if (changed !== 0) {
      for (let c = 0; c < 20; c++) {
        if ((changed & (1 << c)) === 0)
          continue;
        events.push({
          time: frame.time,
          column: c,
          down: (frame.keys & (1 << c)) !== 0,
        });
      }
    }
    prev = frame.keys;
  }
  return events;
}

function parseDelta(s: string): number | undefined {
  const i = Number.parseInt(s, 10);
  if (Number.isFinite(i) && String(i) === s.trim())
    return i;
  const d = Number.parseFloat(s);
  if (!Number.isFinite(d))
    return undefined;
  return Math.round(d);
}

function applyScoreInfo(replay: OsrReplay, compressed: Uint8Array): void {
  let json: string;
  try {
    json = decompressLzmaSync(compressed);
  } catch {
    return;
  }
  try {
    const root = JSON.parse(json) as {
      online_id?: number;
      client_version?: string;
      mods?: Array<{ acronym?: string; settings?: { speed_change?: number } }>;
    };
    if (typeof root.online_id === "number" && root.online_id > 0)
      replay.onlineId = root.online_id;
    if (typeof root.client_version === "string")
      replay.clientVersion = root.client_version;
    if (Array.isArray(root.mods)) {
      const list: ModInfo[] = [];
      for (const m of root.mods) {
        const acronym = m.acronym ?? "";
        if (!acronym)
          continue;
        list.push({
          acronym,
          speedChange: m.settings?.speed_change,
        });
      }
      if (list.length > 0)
        replay.mods = list;
    }
  } catch {
    // extra blob is best-effort
  }
}

function fromLegacy(mods: number): ModInfo[] {
  const list: ModInfo[] = [];
  const add = (flag: number, acronym: string) => {
    if ((mods & flag) !== 0)
      list.push({ acronym });
  };
  add(LegacyMods.Easy, "EZ");
  add(LegacyMods.NoFail, "NF");
  add(LegacyMods.Hidden, "HD");
  add(LegacyMods.HardRock, "HR");
  add(LegacyMods.SuddenDeath, "SD");
  add(LegacyMods.DoubleTime, "DT");
  add(LegacyMods.Nightcore, "NC");
  add(LegacyMods.HalfTime, "HT");
  add(LegacyMods.Flashlight, "FL");
  add(LegacyMods.FadeIn, "FI");
  add(LegacyMods.Perfect, "PF");
  add(LegacyMods.Mirror, "MR");
  add(LegacyMods.Random, "RD");
  add(LegacyMods.Autoplay, "AU");
  add(LegacyMods.ScoreV2, "SV2");
  add(LegacyMods.Key4, "4K");
  add(LegacyMods.Key5, "5K");
  add(LegacyMods.Key6, "6K");
  add(LegacyMods.Key7, "7K");
  add(LegacyMods.Key8, "8K");
  add(LegacyMods.Key9, "9K");
  return list;
}

function legacyModsLabel(mods: number): string {
  return fromLegacy(mods).map((m) => m.acronym).join("") || "NM";
}

export function decompressLzmaSync(compressed: Uint8Array): string {
  if (compressed.length < 13)
    throw new Error("lzma payload too short");
  const result = lzma.decompress(compressed);
  if (typeof result === "string")
    return result;
  return new TextDecoder("ascii").decode(result);
}

export function compressLzma(data: string): Uint8Array {
  const result = lzma.compress(data, 1);
  return Uint8Array.from(result, (b) => b & 0xff);
}

class BinaryCursor {
  pos = 0;
  constructor(readonly buf: Uint8Array) {}

  get remaining(): number {
    return this.buf.length - this.pos;
  }

  u8(): number {
    return this.buf[this.pos++];
  }

  u16(): number {
    const v = this.buf[this.pos] | (this.buf[this.pos + 1] << 8);
    this.pos += 2;
    return v;
  }

  i32(): number {
    const v = new DataView(this.buf.buffer, this.buf.byteOffset + this.pos, 4).getInt32(0, true);
    this.pos += 4;
    return v;
  }

  i64(): bigint {
    const v = new DataView(this.buf.buffer, this.buf.byteOffset + this.pos, 8).getBigInt64(0, true);
    this.pos += 8;
    return v;
  }

  bytes(n: number): Uint8Array {
    const slice = this.buf.subarray(this.pos, this.pos + n);
    this.pos += n;
    return slice;
  }

  osuString(): string {
    if (this.pos >= this.buf.length)
      return "";
    const kind = this.u8();
    if (kind === 0)
      return "";
    if (kind !== 0x0b)
      throw new Error(`unexpected osu string kind ${kind}`);
    const len = this.uleb128();
    const bytes = this.bytes(len);
    return new TextDecoder("utf-8").decode(bytes);
  }

  uleb128(): number {
    let result = 0;
    let shift = 0;
    while (true) {
      const b = this.u8();
      result |= (b & 0x7f) << shift;
      if ((b & 0x80) === 0)
        return result;
      shift += 7;
    }
  }
}
