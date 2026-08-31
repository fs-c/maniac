namespace ManiaHuman;

sealed class ReplayStats
{
    public required MatchResult Match;
    public double MeanError;
    public double StdError;
    public double UnstableRate;
    public double Skew;
    public double EarlyRate;
    public double AutocorrLag1;
    public double ChordSpreadMean;
    public double ChordSpreadStd;
    public int ChordCount;
    public double TapDurationMean;
    public double TapDurationStd;
    public double HoldReleaseMean;
    public double HoldReleaseStd;
    public double ErrorVsDensityCorr;
    public double SparseUr;
    public double DenseUr;
    public int[] Judgements = new int[6];
    public double[] ColumnMean = [];
    public double[] ColumnStd = [];
    public int MatchedHeads;
    public int MissedHeads;
    public double MissRate;
    public int? LeaderboardRank;
    public double ApiAccuracy;
    public double Stars;
    public string Source = "";
    public int? UserGlobalRank;

    public OsrReplay Replay => Match.Replay;
    public ManiaBeatmap Beatmap => Match.Beatmap;
}

sealed class AggregateStats
{
    public int ReplayCount;
    public int Skipped;
    public double MeanUr;
    public double MeanAbsBias;
    public double MeanAutocorr;
    public double MeanChordSpread;
    public double MeanTapDuration;
    public double MeanTapDurationStd;
    public double MeanHoldRelease;
    public double MeanDensityCorr;
    public double MeanMissRate;
    public List<string> Findings = [];
    public List<ReplayStats> Replays = [];
}

static class Analyze
{
    public static ReplayStats Compute(MatchResult match)
    {
        var stats = new ReplayStats { Match = match };
        var heads = match.Hits.Where(h => h.Kind != HitKind.HoldTail).ToArray();
        var hitHeads = heads.Where(h => h.Hit && !double.IsNaN(h.Error)).ToArray();
        stats.MatchedHeads = hitHeads.Length;
        stats.MissedHeads = heads.Length - hitHeads.Length;
        foreach (var h in match.Hits.Where(h => h.Kind != HitKind.HoldTail || h.Hit || h.Judgement == Judgement.Miss))
        {
            if (h.Kind == HitKind.HoldTail && !h.Hit && heads.Any(x => ReferenceEquals(x.Note, h.Note) && !x.Hit))
                continue;
            stats.Judgements[(int)h.Judgement]++;
        }

        var errors = hitHeads.Select(h => h.Error).ToArray();
        stats.MeanError = Mean(errors);
        stats.StdError = Std(errors);
        stats.UnstableRate = stats.StdError * 10;
        stats.Skew = Skew(errors, stats.MeanError, stats.StdError);
        stats.EarlyRate = errors.Length == 0 ? 0 : errors.Count(e => e < 0) / (double)errors.Length;
        stats.AutocorrLag1 = Autocorr(errors);

        var chords = ChordSpreads(hitHeads);
        stats.ChordCount = chords.Length;
        stats.ChordSpreadMean = Mean(chords);
        stats.ChordSpreadStd = Std(chords);

        var tapDurations = hitHeads
            .Where(h => h.Kind == HitKind.Tap && h.ReleaseTime is double r)
            .Select(h => h.ReleaseTime!.Value - h.PressTime)
            .Where(d => d is > 0 and < 200)
            .ToArray();
        stats.TapDurationMean = Mean(tapDurations);
        stats.TapDurationStd = Std(tapDurations);

        var holdRel = match.Hits
            .Where(h => h.Kind == HitKind.HoldTail && h.Hit && !double.IsNaN(h.Error))
            .Select(h => h.Error)
            .ToArray();
        stats.HoldReleaseMean = Mean(holdRel);
        stats.HoldReleaseStd = Std(holdRel);

        var dens = hitHeads.Select(h => h.LocalNps).ToArray();
        var absErr = hitHeads.Select(h => Math.Abs(h.Error)).ToArray();
        stats.ErrorVsDensityCorr = Corr(dens, absErr);

        var sparse = hitHeads.Where(h => h.LocalNps < 6).Select(h => h.Error).ToArray();
        var dense = hitHeads.Where(h => h.LocalNps >= 12).Select(h => h.Error).ToArray();
        stats.SparseUr = Std(sparse) * 10;
        stats.DenseUr = Std(dense) * 10;

        int cols = Math.Max(match.Beatmap.Columns, 1);
        stats.ColumnMean = new double[cols];
        stats.ColumnStd = new double[cols];
        for (int c = 0; c < cols; c++)
        {
            var ce = hitHeads.Where(h => h.Note.Column == c).Select(h => h.Error).ToArray();
            stats.ColumnMean[c] = Mean(ce);
            stats.ColumnStd[c] = Std(ce);
        }

        stats.MissRate = heads.Length == 0 ? 0 : stats.MissedHeads / (double)heads.Length;
        return stats;
    }

    public static AggregateStats Aggregate(IEnumerable<ReplayStats> items)
    {
        var list = items.Where(s => s.MatchedHeads >= 20).ToList();
        var agg = new AggregateStats
        {
            ReplayCount = list.Count,
            Replays = list,
        };
        if (list.Count == 0)
        {
            agg.Findings.Add("No usable mania replays yet. Drop .osr files into data/replays, or run fetch / scan.");
            return agg;
        }

        agg.MeanUr = Mean(list.Select(s => s.UnstableRate).ToArray());
        agg.MeanAbsBias = Mean(list.Select(s => Math.Abs(s.MeanError)).ToArray());
        agg.MeanAutocorr = Mean(list.Select(s => s.AutocorrLag1).ToArray());
        agg.MeanChordSpread = Mean(list.Where(s => s.ChordCount > 0).Select(s => s.ChordSpreadMean).ToArray());
        agg.MeanTapDuration = Mean(list.Select(s => s.TapDurationMean).Where(v => v > 0).ToArray());
        agg.MeanTapDurationStd = Mean(list.Select(s => s.TapDurationStd).Where(v => v > 0).ToArray());
        agg.MeanHoldRelease = Mean(list.Select(s => s.HoldReleaseMean).ToArray());
        agg.MeanDensityCorr = Mean(list.Select(s => s.ErrorVsDensityCorr).ToArray());
        agg.MeanMissRate = Mean(list.Select(s => s.MissRate).ToArray());
        agg.Findings = Describe(agg, list);
        return agg;
    }

    static List<string> Describe(AggregateStats agg, List<ReplayStats> list)
    {
        var f = new List<string>();
        f.Add($"n={agg.ReplayCount}. Mean UR {agg.MeanUr:0.0} (hit-error σ ≈ {agg.MeanUr / 10:0.0} ms). Mean miss rate {agg.MeanMissRate:0.0%}.");
        f.Add($"Mean |bias| {agg.MeanAbsBias:0.00} ms; {list.Count(s => s.MeanError < -1)} early / {list.Count(s => s.MeanError > 1)} late / {list.Count(s => Math.Abs(s.MeanError) <= 1)} within ±1 ms of on-time.");
        f.Add($"Chords (same timestamp, 2+ keys) are desynced by {agg.MeanChordSpread:0.0} ms on average (max−min press time). Simultaneous presses are the exception, not the rule.");
        f.Add($"Rice taps stay held for {agg.MeanTapDuration:0.0} ± {agg.MeanTapDurationStd:0.0} ms. Hold tails release {Signed(agg.MeanHoldRelease)}.");
        f.Add($"Lag-1 autocorrelation of hit error is {agg.MeanAutocorr:0.00}. Above ~0.15 means timing drifts (late stays late) rather than independent noise.");
        f.Add($"|error| vs local density r={agg.MeanDensityCorr:0.00}. Positive means streams get sloppier.");

        var nm = list.Where(s => s.Replay.Rate == 1 && !s.Replay.HasAcronym("HT") && !s.Replay.HasAcronym("DT") && !s.Replay.HasAcronym("NC")).ToList();
        var top = nm.Where(s => s.Source == "leaderboard" && s.LeaderboardRank is >= 1 and <= 10).ToList();
        var bottom = nm.Where(s => s.Source == "leaderboard" && s.LeaderboardRank is >= 20).ToList();
        if (top.Count >= 2 && bottom.Count >= 2)
        {
            f.Add($"Hard-map leaderboard, no rate mods: #1–10 UR {Mean(top.Select(s => s.UnstableRate).ToArray()):0.0} and miss {Mean(top.Select(s => s.MissRate).ToArray()):0.0%}; #20+ UR {Mean(bottom.Select(s => s.UnstableRate).ToArray()):0.0} and miss {Mean(bottom.Select(s => s.MissRate).ToArray()):0.0%}. (HT/DT scores are excluded here — they sit lower on the board because the map is easier at 0.75×/1.5×.)");
        }

        var recent = list.Where(s => s.Source == "recent").ToList();
        if (recent.Count >= 2)
        {
            f.Add($"Mid-tier players' recent plays (not BPs): UR {Mean(recent.Select(s => s.UnstableRate).ToArray()):0.0}, miss {Mean(recent.Select(s => s.MissRate).ToArray()):0.0%}, acc {Mean(recent.Select(s => s.ApiAccuracy).Where(a => a > 0).ToArray()):0.0%}.");
        }

        var denser = list.Count(s => s.DenseUr > s.SparseUr + 5);
        f.Add($"{denser}/{list.Count} replays are less stable in dense sections (dense UR vs sparse).");
        return f;
    }

    static double[] ChordSpreads(HitSample[] hitHeads)
    {
        var groups = hitHeads
            .GroupBy(h => Math.Round(h.Note.Start))
            .Where(g => g.Select(x => x.Note.Column).Distinct().Count() >= 2);
        var spreads = new List<double>();
        foreach (var g in groups)
        {
            var times = g.Select(h => h.PressTime).ToArray();
            spreads.Add(times.Max() - times.Min());
        }

        return spreads.ToArray();
    }

    static double Mean(double[] xs)
    {
        if (xs.Length == 0) return 0;
        double s = 0;
        foreach (double x in xs) s += x;
        return s / xs.Length;
    }

    static double Std(double[] xs)
    {
        if (xs.Length < 2) return 0;
        double m = Mean(xs);
        double v = 0;
        foreach (double x in xs) v += (x - m) * (x - m);
        return Math.Sqrt(v / (xs.Length - 1));
    }

    static double Skew(double[] xs, double mean, double std)
    {
        if (xs.Length < 3 || std == 0) return 0;
        double s = 0;
        foreach (double x in xs)
        {
            double z = (x - mean) / std;
            s += z * z * z;
        }

        return s / xs.Length;
    }

    static double Autocorr(double[] xs)
    {
        if (xs.Length < 4) return 0;
        var a = xs.AsSpan(0, xs.Length - 1).ToArray();
        var b = xs.AsSpan(1, xs.Length - 1).ToArray();
        return Corr(a, b);
    }

    static double Corr(double[] a, double[] b)
    {
        int n = Math.Min(a.Length, b.Length);
        if (n < 4) return 0;
        double ma = 0, mb = 0;
        for (int i = 0; i < n; i++)
        {
            ma += a[i];
            mb += b[i];
        }

        ma /= n;
        mb /= n;
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < n; i++)
        {
            double xa = a[i] - ma, xb = b[i] - mb;
            num += xa * xb;
            da += xa * xa;
            db += xb * xb;
        }

        double den = Math.Sqrt(da * db);
        return den == 0 ? 0 : num / den;
    }

    static string Signed(double v) => v == 0 ? "on time" : v < 0 ? $"{v:0.0} ms early" : $"{v:0.0} ms late";
}
