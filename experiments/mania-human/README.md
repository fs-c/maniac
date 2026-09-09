# mania-human

Experiment: what public mania replays actually look like in timing space.

Parses `.osr` keypresses, aligns them to `.osu` notes, writes `data/report.html` + `data/report.json`.

- hit error (press − note time), unstable rate = 10 × σ
- chord desync (max−min press time on simultaneous notes)
- rice tap hold duration
- hold-tail release error
- lag-1 error autocorrelation
- miss rate, and how UR/miss change with leaderboard place

Converts and `RD`/`AU` scores are skipped. Mirror is applied.

The osu! API leaderboard is only the **top 50**. Fetch therefore (1) spreads across that top 50 on **hard** ranked 4K/7K maps (stars ≥ 6) and (2) pulls **recent** plays (including fails) from mid-tier 4K pp ranking pages, which are not career BPs.

## Run

```powershell
dotnet run --project experiments/mania-human -- selftest
dotnet run --project experiments/mania-human -- fetch --replace --also-analyze --open
```

`--replace` deletes previously fetched `.osr` files first. Data lives in `experiments/mania-human/data/` (gitignored).

## Credentials

OAuth app at [osu! account settings](https://osu.ppy.sh/home/account/edit#oauth), then `%APPDATA%\maniac-lazer\osu-oauth.json` (or `OSU_CLIENT_ID` / `OSU_CLIENT_SECRET`). Replay listing/download worked with client credentials last time; `login` is only needed if download starts returning 403.

Optional: drop exported `.osr` into `data/replays` and `.osu` into `data/beatmaps`, or `scan` a local stable/lazer install.
