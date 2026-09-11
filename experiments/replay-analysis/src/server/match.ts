import type { HitKind, HitWindowsInfo, Judgement, MatchSummary, NoteHit } from "../shared/types.ts";
import {
  hasAcronym,
  hasClassicWindows,
  hasMirror,
  LegacyMods,
  replayRate,
  type ManiaBeatmap,
  type ManiaNote,
  type OsrReplay,
} from "./osr.ts";

export type HitWindows = HitWindowsInfo;

export type HitSample = {
  note: ManiaNote;
  kind: HitKind;
  pressTime: number;
  releaseTime?: number;
  error: number;
  judgement: Judgement;
  hit: boolean;
};

export type MatchResult = {
  replay: OsrReplay;
  beatmap: ManiaBeatmap;
  windows: HitWindows;
  hits: HitSample[];
  ghostTaps: number;
  extraReleases: number;
};

export function hitWindows(opts: {
  od: number;
  rate: number;
  classic: boolean;
  easy: boolean;
  hardRock: boolean;
  convert: boolean;
}): HitWindows {
  let difficulty = 1;
  if (opts.easy)
    difficulty = 1 / 1.4;
  if (opts.hardRock)
    difficulty = 1.4;
  const total = opts.rate / difficulty;
  const od = opts.od;

  let perfect: number;
  let great: number;
  let good: number;
  let ok: number;
  let meh: number;
  let miss: number;

  if (opts.classic) {
    if (opts.convert) {
      perfect = floorHalf(16 * total);
      great = floorHalf((Math.round(od) > 4 ? 34 : 47) * total);
      good = floorHalf((Math.round(od) > 4 ? 67 : 77) * total);
      ok = floorHalf(97 * total);
      meh = floorHalf(121 * total);
      miss = floorHalf(158 * total);
    } else {
      const inverted = Math.min(10, Math.max(0, 10 - od));
      perfect = floorHalf(16 * total);
      great = floorHalf((34 + 3 * inverted) * total);
      good = floorHalf((67 + 3 * inverted) * total);
      ok = floorHalf((97 + 3 * inverted) * total);
      meh = floorHalf((121 + 3 * inverted) * total);
      miss = floorHalf((158 + 3 * inverted) * total);
    }
  } else {
    perfect = floorHalf(difficultyRange(od, 22.4, 19.4, 13.9) * total);
    great = floorHalf(difficultyRange(od, 64, 49, 34) * total);
    good = floorHalf(difficultyRange(od, 97, 82, 67) * total);
    ok = floorHalf(difficultyRange(od, 127, 112, 97) * total);
    meh = floorHalf(difficultyRange(od, 151, 136, 121) * total);
    miss = floorHalf(difficultyRange(od, 188, 173, 158) * total);
  }

  return {
    classic: opts.classic,
    rate: opts.rate,
    od,
    convert: opts.convert,
    perfect,
    great,
    good,
    ok,
    meh,
    miss,
  };
}

export function judge(windows: HitWindows, error: number): Judgement {
  const a = Math.abs(error);
  if (a <= windows.perfect)
    return "perfect";
  if (a <= windows.great)
    return "great";
  if (a <= windows.good)
    return "good";
  if (a <= windows.ok)
    return "ok";
  if (a <= windows.meh)
    return "meh";
  return "miss";
}

export function matchNotes(replay: OsrReplay, beatmap: ManiaBeatmap): MatchResult {
  const notes = applyMods(beatmap.notes, beatmap.columns, replay);
  const windows = hitWindows({
    od: beatmap.overallDifficulty,
    rate: replayRate(replay),
    classic: hasClassicWindows(replay),
    easy: hasMod(replay, "EZ"),
    hardRock: hasMod(replay, "HR"),
    convert: beatmap.mode !== 3,
  });

  const byColumn: ManiaNote[][] = Array.from({ length: Math.max(beatmap.columns, 1) }, () => []);
  for (const note of notes) {
    const c = Math.min(byColumn.length - 1, Math.max(0, note.column));
    byColumn[c].push(note);
  }

  const hits: HitSample[] = [];
  let ghost = 0;
  let extraUp = 0;
  const downAt: Array<number | undefined> = Array.from({ length: 20 });
  const openHit: Array<HitSample | undefined> = Array.from({ length: 20 });
  const cursor = byColumn.map(() => 0);

  for (const event of replay.events) {
    const col = event.column;
    if (col >= byColumn.length) {
      if (event.down)
        ghost++;
      continue;
    }
    if (event.down) {
      downAt[col] = event.time;
      let i = cursor[col];
      const list = byColumn[col];
      while (i < list.length && event.time - list[i].start > windows.miss)
        i++;
      cursor[col] = i;
      if (i >= list.length || Math.abs(event.time - list[i].start) > windows.miss) {
        ghost++;
        continue;
      }
      const note = list[i];
      cursor[col] = i + 1;
      const error = event.time - note.start;
      const sample: HitSample = {
        note,
        kind: note.hold ? "holdHead" : "tap",
        pressTime: event.time,
        error,
        judgement: judge(windows, error),
        hit: true,
      };
      hits.push(sample);
      openHit[col] = sample;
    } else {
      const press = downAt[col] ?? event.time;
      downAt[col] = undefined;
      const last = openHit[col];
      openHit[col] = undefined;
      if (!last) {
        extraUp++;
        continue;
      }
      last.releaseTime = event.time;
      if (last.kind === "holdHead") {
        const error = event.time - last.note.end;
        hits.push({
          note: last.note,
          kind: "holdTail",
          pressTime: press,
          releaseTime: event.time,
          error,
          judgement: judge(windows, error),
          hit: Math.abs(error) <= windows.miss,
        });
      }
    }
  }

  const matched = new Set(hits.filter((h) => h.kind !== "holdTail").map((h) => h.note.index));
  for (const note of notes) {
    if (matched.has(note.index))
      continue;
    hits.push({
      note,
      kind: note.hold ? "holdHead" : "tap",
      pressTime: Number.NaN,
      error: Number.NaN,
      judgement: "miss",
      hit: false,
    });
    if (note.hold) {
      hits.push({
        note,
        kind: "holdTail",
        pressTime: Number.NaN,
        error: Number.NaN,
        judgement: "miss",
        hit: false,
      });
    }
  }

  hits.sort((a, b) => a.note.start - b.note.start || kindOrder(a.kind) - kindOrder(b.kind) || a.note.column - b.note.column);

  return { replay, beatmap, windows, hits, ghostTaps: ghost, extraReleases: extraUp };
}

export function summarizeMatch(match: MatchResult): MatchSummary {
  const heads = match.hits.filter((h) => h.kind !== "holdTail");
  const hitHeads = heads.filter((h) => h.hit && Number.isFinite(h.error));
  const errors = hitHeads.map((h) => h.error);
  const meanError = mean(errors);
  const stdError = std(errors);
  const judgements: Record<Judgement, number> = {
    miss: 0,
    meh: 0,
    ok: 0,
    good: 0,
    great: 0,
    perfect: 0,
  };
  for (const hit of match.hits) {
    if (hit.kind === "holdTail" && !hit.hit) {
      const headMissed = heads.some((h) => h.note.index === hit.note.index && !h.hit);
      if (headMissed)
        continue;
    }
    judgements[hit.judgement]++;
  }
  return {
    noteCount: match.beatmap.notes.length,
    columns: match.beatmap.columns,
    matchedHeads: hitHeads.length,
    missedHeads: heads.length - hitHeads.length,
    ghostTaps: match.ghostTaps,
    extraReleases: match.extraReleases,
    meanError: errors.length > 0 ? meanError : undefined,
    stdError: errors.length > 1 ? stdError : undefined,
    unstableRate: errors.length > 1 ? stdError * 10 : undefined,
    judgements,
    windows: match.windows,
  };
}

export function serializeHits(hits: HitSample[], offset: number, limit: number): NoteHit[] {
  return hits.slice(offset, offset + limit).map(serializeHit);
}

function serializeHit(hit: HitSample): NoteHit {
  const row: NoteHit = {
    noteIndex: hit.note.index,
    noteStart: hit.note.start,
    noteEnd: hit.note.end,
    column: hit.note.column,
    kind: hit.kind,
    judgement: hit.judgement,
    hit: hit.hit,
  };
  if (Number.isFinite(hit.pressTime))
    row.pressTime = hit.pressTime;
  if (hit.releaseTime !== undefined && Number.isFinite(hit.releaseTime))
    row.releaseTime = hit.releaseTime;
  if (Number.isFinite(hit.error))
    row.error = hit.error;
  return row;
}

function applyMods(notes: ManiaNote[], columns: number, replay: OsrReplay): ManiaNote[] {
  if (!hasMirror(replay))
    return notes;
  return notes.map((n) => ({ ...n, column: columns - 1 - n.column }));
}

function hasMod(replay: OsrReplay, acronym: string): boolean {
  if (hasAcronym(replay, acronym))
    return true;
  if (acronym === "EZ")
    return (replay.legacyMods & LegacyMods.Easy) !== 0;
  if (acronym === "HR")
    return (replay.legacyMods & LegacyMods.HardRock) !== 0;
  return false;
}

function kindOrder(kind: HitKind): number {
  if (kind === "tap" || kind === "holdHead")
    return 0;
  return 1;
}

function floorHalf(v: number): number {
  return Math.floor(v) + 0.5;
}

function difficultyRange(difficulty: number, min: number, mid: number, max: number): number {
  const t = (difficulty - 5) / 5;
  if (difficulty > 5)
    return mid + (max - mid) * t;
  if (difficulty < 5)
    return mid + (mid - min) * t;
  return mid;
}

function mean(xs: number[]): number {
  if (xs.length === 0)
    return 0;
  return xs.reduce((s, x) => s + x, 0) / xs.length;
}

function std(xs: number[]): number {
  if (xs.length < 2)
    return 0;
  const m = mean(xs);
  let v = 0;
  for (const x of xs)
    v += (x - m) * (x - m);
  return Math.sqrt(v / (xs.length - 1));
}
