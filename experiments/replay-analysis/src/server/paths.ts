import { mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

export const experimentRoot = join(here, "..", "..");

export const dataDir = process.env.REPLAY_ANALYSIS_DATA ?? join(experimentRoot, "data");
export const replayDir = join(dataDir, "replays");
export const beatmapDir = join(dataDir, "beatmaps");

export function secretsPath(): string {
  const appdata = process.env.APPDATA
    ?? join(process.env.HOME ?? ".", ".config");
  return join(appdata, "maniac-lazer", "osu-oauth.json");
}

export function ensureDataDirs(): void {
  mkdirSync(replayDir, { recursive: true });
  mkdirSync(beatmapDir, { recursive: true });
}
