# replay-analysis

Web-first experiment for collecting public osu! mania replays. v1 only **gathers** data and shows a catalog. Timing analysis is intentionally absent.

Leave `experiments/mania-human` alone; this is the next surface.

## Run

```powershell
cd experiments/replay-analysis
npm install
npm test
npm run dev
```

Then open [http://127.0.0.1:5173](http://127.0.0.1:5173). The Vite page proxies `/api` to the local server on port 8787.

Data lives in `experiments/replay-analysis/data/` (gitignored). You can also drop exported `.osr` files into `data/replays` and click Refresh.

Each fetch is recorded in `data/runs.json` (params, timestamp, which `.osr` files and beatmap IDs it produced). The **Runs** panel filters the catalog to one search — useful later when analysis should run on a subset (e.g. only the latest batch) instead of everything. Dropped-in files with no run show under **No run**. Replace existing `.osr` also clears run history.

osu! API v2 wrappers (full query surfaces, not just what the UI sends) live in [`src/server/osu.ts`](src/server/osu.ts). Catalog streams in `catalog.ts` are thin recipes on top of those.

## Credentials

Same file as mania-human: an OAuth app at [osu! account settings](https://osu.ppy.sh/home/account/edit#oauth), callback `http://127.0.0.1:7272/callback`, then `%APPDATA%\maniac-lazer\osu-oauth.json` (or `OSU_CLIENT_ID` / `OSU_CLIENT_SECRET`).

Client credentials can list maps and scores. Downloading `.osr` needs **Log in with osu!** in the top bar (user token). Login sends this tab to osu!, then back to the app — the OAuth app callback stays `http://127.0.0.1:7272/callback`.

## Sources

Each card is a separate stream. Run them independently.

- **Maps → scores** — pick N beatmaps (search with filters, random by default; or a list of IDs), then for each map draw M scores from global leaderboard places `rankFrom–rankTo` (API top 50). Score sample is random by default (or even spacing).
- **Ranking recents** — `GET /rankings/mania/performance?page=P&variant=4k|7k` (page P = ranks `(P-1)*50+1 … P*50`), then one recent per user from `GET /users/{id}/scores/recent`.

Unusable mods (`RD`, `AU`, `CN`, `RX`, `AP`, key-count mods) are skipped. Mirror is kept.

The inspector shows replay headers and key events (time, column, down/up). It does not match notes or compute UR.
