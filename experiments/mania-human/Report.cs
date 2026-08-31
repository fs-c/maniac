using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ManiaHuman;

static class Report
{
    public static string Write(string dataDir, AggregateStats agg)
    {
        Directory.CreateDirectory(dataDir);
        string htmlPath = Path.Combine(dataDir, "report.html");
        string jsonPath = Path.Combine(dataDir, "report.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(agg.Replays.Select(Summary), new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(htmlPath, Html(agg), Encoding.UTF8);
        return htmlPath;
    }

    public static void Print(AggregateStats agg)
    {
        Console.WriteLine();
        Console.WriteLine($"analyzed {agg.ReplayCount} mania replays  (skipped {agg.Skipped})");
        if (agg.ReplayCount == 0)
        {
            foreach (string f in agg.Findings)
                Console.WriteLine("  " + f);
            return;
        }

        Console.WriteLine($"  UR {agg.MeanUr:0.0}   |bias| {agg.MeanAbsBias:0.00}ms   autocorr {agg.MeanAutocorr:0.00}   miss {agg.MeanMissRate:0.0%}");
        Console.WriteLine($"  chord spread {agg.MeanChordSpread:0.0}ms   tap hold {agg.MeanTapDuration:0.0}±{agg.MeanTapDurationStd:0.0}ms");
        Console.WriteLine();
        foreach (string f in agg.Findings)
            Console.WriteLine("  • " + f);
        Console.WriteLine();
        Console.WriteLine($"{"player",-16} {"src",8} {"#",4} {"*",5} {"UR",6} {"bias",7} {"miss",6} {"chord",6} {"tap",6} {"r1",5}");
        foreach (var s in agg.Replays.OrderBy(x => x.Source).ThenBy(x => x.LeaderboardRank ?? 999))
        {
            Console.WriteLine($"{Trunc(s.Replay.Player, 16),-16} {Trunc(s.Source, 8),8} {s.LeaderboardRank,4} {s.Stars,5:0.0} {s.UnstableRate,6:0.0} {s.MeanError,7:0.0} {s.MissRate,6:0.0%} {s.ChordSpreadMean,6:0.0} {s.TapDurationMean,6:0.0} {s.AutocorrLag1,5:0.00}");
        }
    }

    static object Summary(ReplayStats s) => new
    {
        player = s.Replay.Player,
        map = s.Beatmap.DisplayName,
        md5 = s.Beatmap.Md5,
        mods = s.Replay.ModsLabel,
        keys = s.Beatmap.Columns,
        source = s.Source,
        leaderboardRank = s.LeaderboardRank,
        stars = Round(s.Stars),
        apiAccuracy = Round(s.ApiAccuracy),
        ur = Round(s.UnstableRate),
        meanError = Round(s.MeanError),
        stdError = Round(s.StdError),
        skew = Round(s.Skew),
        earlyRate = Round(s.EarlyRate),
        autocorr = Round(s.AutocorrLag1),
        chordSpread = Round(s.ChordSpreadMean),
        tapDuration = Round(s.TapDurationMean),
        tapDurationStd = Round(s.TapDurationStd),
        holdRelease = Round(s.HoldReleaseMean),
        densityCorr = Round(s.ErrorVsDensityCorr),
        sparseUr = Round(s.SparseUr),
        denseUr = Round(s.DenseUr),
        matched = s.MatchedHeads,
        missed = s.MissedHeads,
        missRate = Round(s.MissRate),
        ghostTaps = s.Match.GhostTaps,
        columnMean = s.ColumnMean.Select(Round).ToArray(),
        columnStd = s.ColumnStd.Select(Round).ToArray(),
    };

    static string Html(AggregateStats agg)
    {
        var errors = agg.Replays.SelectMany(s => s.Match.Hits.Where(h => h.Hit && h.Kind != HitKind.HoldTail).Select(h => h.Error)).ToArray();
        var taps = agg.Replays.SelectMany(s => s.Match.Hits.Where(h => h.Hit && h.Kind == HitKind.Tap && h.ReleaseTime is double)
            .Select(h => h.ReleaseTime!.Value - h.PressTime).Where(d => d is > 0 and < 200)).ToArray();
        var chords = agg.Replays.SelectMany(ChordSpreads).ToArray();
        var board = agg.Replays.Where(s => s.Source == "leaderboard" && s.LeaderboardRank is > 0).ToArray();
        var accPts = agg.Replays.Where(s => s.ApiAccuracy > 0).Select(s => (s.ApiAccuracy * 100, s.UnstableRate)).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>mania human timing</title>");
        sb.AppendLine("""
            <style>
              :root { --bg:#121218; --card:#1c1c28; --ink:#e8e8f0; --muted:#9aa0b4; --accent:#bf5fff; }
              body { font: 15px/1.5 ui-sans-serif, system-ui, sans-serif; background:var(--bg); color:var(--ink); margin:0; padding:32px 36px 64px; max-width:1100px; }
              h1 { font-size:22px; font-weight:650; margin:0 0 8px; }
              h2 { font-size:16px; margin:36px 0 8px; color:var(--accent); }
              p, li { color:var(--muted); }
              .sub { color:var(--muted); margin-bottom:24px; }
              .grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:12px; }
              .card { background:var(--card); border-radius:12px; padding:16px 18px; }
              .card b { display:block; font-size:22px; color:var(--ink); }
              .card span { color:var(--muted); font-size:12px; letter-spacing:.04em; text-transform:uppercase; }
              table { width:100%; border-collapse:collapse; font-variant-numeric:tabular-nums; }
              th, td { text-align:left; padding:6px 8px; border-bottom:1px solid #2a2a3a; }
              th { color:var(--muted); font-size:12px; font-weight:600; }
              .findings li { margin:8px 0; }
              figure { margin:0 0 8px; }
              figcaption { color:var(--muted); font-size:13px; margin-top:6px; }
              svg { width:100%; height:auto; background:#16161f; border-radius:10px; }
            </style></head><body>
            """);
        sb.AppendLine($"<h1>mania timing</h1><p class=\"sub\">{agg.ReplayCount} public mania replays. Hit error is press time minus note time (negative = early). Unstable rate is 10 × std of those errors.</p>");
        sb.AppendLine("<div class=\"grid\">");
        Metric(sb, "unstable rate", $"{agg.MeanUr:0.0}", "10 × σ of hit error");
        Metric(sb, "hit-error σ", $"{agg.MeanUr / 10:0.0} ms", "not the plot range");
        Metric(sb, "miss rate", $"{agg.MeanMissRate:0.0%}", "unmatched heads");
        Metric(sb, "chord spread", $"{agg.MeanChordSpread:0.0} ms", "max−min in a chord");
        Metric(sb, "tap duration", $"{agg.MeanTapDuration:0.0} ± {agg.MeanTapDurationStd:0.0} ms", "rice key hold time");
        Metric(sb, "error autocorr", $"{agg.MeanAutocorr:0.00}", "lag-1");
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>Hit error</h2>");
        sb.AppendLine("<figure>");
        sb.AppendLine(Chart.Histogram(errors, -80, 80, 40, "Hit error (ms)", "Hits", markZero: true));
        sb.AppendLine($"<figcaption>Each bar is a 4 ms bin. σ ≈ {Std(errors):0.0} ms, so most hits sit in a much narrower band than the ±80 ms axis. Negative = early.</figcaption></figure>");

        sb.AppendLine("<h2>Rice tap duration</h2>");
        sb.AppendLine("<figure>");
        sb.AppendLine(Chart.Histogram(taps, 0, 160, 32, "Key hold duration (ms)", "Taps", markZero: false));
        sb.AppendLine("<figcaption>How long a rice note is held before release. Short holds (&lt;15 ms) are often jack transitions.</figcaption></figure>");

        sb.AppendLine("<h2>Chord desync</h2>");
        sb.AppendLine("<figure>");
        sb.AppendLine(Chart.Histogram(chords, 0, 30, 30, "Chord spread (ms)", "Chords", markZero: false));
        sb.AppendLine("<figcaption>For notes that share a timestamp, spread is the delay between the first and last key in that chord.</figcaption></figure>");

        if (board.Length >= 3)
        {
            var pts = board.Select(s => ((double)s.LeaderboardRank!.Value, s.UnstableRate)).ToArray();
            sb.AppendLine("<h2>Leaderboard place vs unstable rate</h2>");
            sb.AppendLine("<figure>");
            sb.AppendLine(Chart.Scatter(pts, "Leaderboard place (1 = #1)", "Unstable rate", xMin: 1, xMax: 50));
            sb.AppendLine("<figcaption>osu! API only returns the top 50. Lower on the board should be sloppier if the map is hard enough that #50 is not still an SS.</figcaption></figure>");
        }

        if (accPts.Length >= 3)
        {
            sb.AppendLine("<h2>Accuracy vs unstable rate</h2>");
            sb.AppendLine("<figure>");
            sb.AppendLine(Chart.Scatter(accPts, "Score accuracy (%)", "Unstable rate", xMin: 80, xMax: 100));
            sb.AppendLine("<figcaption>API accuracy for the score. Lower acc should come with higher UR and more misses if the sample is mixed.</figcaption></figure>");
        }

        sb.AppendLine("<h2>Notes</h2><ul class=\"findings\">");
        foreach (string f in agg.Findings)
            sb.AppendLine($"<li>{Esc(f)}</li>");
        sb.AppendLine("</ul>");

        sb.AppendLine("<h2>Replays</h2><table><tr><th>player</th><th>map</th><th>src</th><th>#</th><th>*</th><th>acc</th><th>UR</th><th>bias</th><th>miss</th><th>chord</th><th>tap</th><th>r₁</th></tr>");
        foreach (var s in agg.Replays.OrderBy(x => x.Source).ThenBy(x => x.LeaderboardRank ?? 999))
        {
            sb.AppendLine("<tr>"
                + $"<td>{Esc(s.Replay.Player)}</td>"
                + $"<td>{Esc(s.Beatmap.DisplayName)}</td>"
                + $"<td>{Esc(s.Source)}</td>"
                + $"<td>{s.LeaderboardRank?.ToString() ?? ""}</td>"
                + $"<td>{s.Stars:0.0}</td>"
                + $"<td>{(s.ApiAccuracy > 0 ? s.ApiAccuracy.ToString("0.0%") : "")}</td>"
                + $"<td>{s.UnstableRate:0.0}</td>"
                + $"<td>{s.MeanError:0.0}</td>"
                + $"<td>{s.MissRate:0.0%}</td>"
                + $"<td>{s.ChordSpreadMean:0.0}</td>"
                + $"<td>{s.TapDurationMean:0.0}</td>"
                + $"<td>{s.AutocorrLag1:0.00}</td></tr>");
        }

        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    static void Metric(StringBuilder sb, string label, string value, string note)
        => sb.AppendLine($"<div class=\"card\"><span>{Esc(label)}</span><b>{Esc(value)}</b><span>{Esc(note)}</span></div>");

    static IEnumerable<double> ChordSpreads(ReplayStats s)
        => s.Match.Hits.Where(h => h.Hit && h.Kind != HitKind.HoldTail)
            .GroupBy(h => Math.Round(h.Note.Start))
            .Where(g => g.Select(x => x.Note.Column).Distinct().Count() >= 2)
            .Select(g => g.Max(x => x.PressTime) - g.Min(x => x.PressTime));

    static double Std(double[] xs)
    {
        if (xs.Length < 2) return 0;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(x => (x - m) * (x - m)) / (xs.Length - 1));
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    static double Round(double v) => Math.Round(v, 3);
}

static class Chart
{
    const double W = 800, H = 340, L = 58, R = 18, T = 18, B = 52;

    public static string Histogram(double[] values, double min, double max, int bins, string xLabel, string yLabel, bool markZero)
    {
        var counts = new int[bins];
        double bw = (max - min) / bins;
        int extra = 0;
        foreach (double v in values)
        {
            int i = (int)Math.Floor((v - min) / bw);
            if ((uint)i >= bins) extra++;
            else counts[i]++;
        }

        int peak = Math.Max(1, counts.Max());
        double yMax = NiceMax(peak);
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg viewBox=\"0 0 {W} {H}\" role=\"img\" aria-label=\"{Esc(xLabel)} histogram\">");
        Axes(sb, min, max, yMax, xLabel, yLabel);

        double plotW = W - L - R, plotH = H - T - B;
        double barW = plotW / bins;
        for (int i = 0; i < bins; i++)
        {
            if (counts[i] == 0) continue;
            double h = counts[i] / yMax * plotH;
            double x = L + i * barW;
            sb.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{x + 0.6:0.##}\" y=\"{T + plotH - h:0.##}\" width=\"{barW - 1.2:0.##}\" height=\"{h:0.##}\" fill=\"#bf5fff\" opacity=\"0.88\"/>");
        }

        if (markZero && min < 0 && max > 0)
        {
            double zx = L + (0 - min) / (max - min) * plotW;
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{zx:0.##}\" y1=\"{T}\" x2=\"{zx:0.##}\" y2=\"{T + plotH}\" stroke=\"#e8e8f0\" stroke-dasharray=\"4 4\" opacity=\"0.45\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{zx + 4:0.##}\" y=\"{T + 12}\" fill=\"#e8e8f0\" font-size=\"11\">on time</text>");
        }

        if (values.Length > 0)
        {
            double mean = values.Average();
            if (mean >= min && mean <= max)
            {
                double mx = L + (mean - min) / (max - min) * plotW;
                sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{mx:0.##}\" y1=\"{T}\" x2=\"{mx:0.##}\" y2=\"{T + plotH}\" stroke=\"#ffd166\" stroke-width=\"1.5\"/>");
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{mx + 4:0.##}\" y=\"{T + 26}\" fill=\"#ffd166\" font-size=\"11\">mean {mean:0.0}</text>");
            }
        }

        sb.Append($"<text x=\"{L}\" y=\"{H - 6}\" fill=\"#9aa0b4\" font-size=\"11\">{values.Length} samples" + (extra > 0 ? $", {extra} outside axis" : "") + "</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string Scatter((double x, double y)[] pts, string xLabel, string yLabel, double xMin, double xMax)
    {
        if (pts.Length == 0) return "";
        double yMin = 0;
        double yMax = NiceMax(pts.Max(p => p.y) * 1.1);
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg viewBox=\"0 0 {W} {H}\" role=\"img\" aria-label=\"{Esc(xLabel)} vs {Esc(yLabel)}\">");
        Axes(sb, xMin, xMax, yMax, xLabel, yLabel, yMin);
        double plotW = W - L - R, plotH = H - T - B;
        foreach (var (x, y) in pts)
        {
            double px = L + (x - xMin) / (xMax - xMin) * plotW;
            double py = T + plotH - (y - yMin) / (yMax - yMin) * plotH;
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{px:0.##}\" cy=\"{py:0.##}\" r=\"4.5\" fill=\"#bf5fff\" opacity=\"0.85\"/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    static void Axes(StringBuilder sb, double xMin, double xMax, double yMax, string xLabel, string yLabel, double yMin = 0)
    {
        double plotW = W - L - R, plotH = H - T - B;
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{L}\" y1=\"{T}\" x2=\"{L}\" y2=\"{T + plotH}\" stroke=\"#9aa0b4\"/>");
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{L}\" y1=\"{T + plotH}\" x2=\"{L + plotW}\" y2=\"{T + plotH}\" stroke=\"#9aa0b4\"/>");

        int xticks = 8;
        for (int i = 0; i <= xticks; i++)
        {
            double v = xMin + (xMax - xMin) * i / xticks;
            double x = L + plotW * i / xticks;
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{x:0.##}\" y1=\"{T + plotH}\" x2=\"{x:0.##}\" y2=\"{T + plotH + 5}\" stroke=\"#9aa0b4\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{x:0.##}\" y=\"{T + plotH + 18}\" fill=\"#9aa0b4\" font-size=\"11\" text-anchor=\"middle\">{Fmt(v)}</text>");
        }

        int yticks = 5;
        for (int i = 0; i <= yticks; i++)
        {
            double v = yMin + (yMax - yMin) * i / yticks;
            double y = T + plotH - plotH * i / yticks;
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{L - 5}\" y1=\"{y:0.##}\" x2=\"{L}\" y2=\"{y:0.##}\" stroke=\"#9aa0b4\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{L}\" y1=\"{y:0.##}\" x2=\"{L + plotW}\" y2=\"{y:0.##}\" stroke=\"#2a2a3a\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{L - 8}\" y=\"{y + 4:0.##}\" fill=\"#9aa0b4\" font-size=\"11\" text-anchor=\"end\">{Fmt(v)}</text>");
        }

        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{L + plotW / 2}\" y=\"{H - 8}\" fill=\"#e8e8f0\" font-size=\"13\" text-anchor=\"middle\">{Esc(xLabel)}</text>");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"16\" y=\"{T + plotH / 2}\" fill=\"#e8e8f0\" font-size=\"13\" text-anchor=\"middle\" transform=\"rotate(-90 16 {T + plotH / 2})\">{Esc(yLabel)}</text>");
    }

    static double NiceMax(double v)
    {
        if (v <= 1) return 1;
        double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        double n = v / exp;
        double nice = n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10;
        return nice * exp;
    }

    static string Fmt(double v)
    {
        double a = Math.Abs(v);
        if (a >= 100 || Math.Abs(v - Math.Round(v)) < 1e-6) return v.ToString("0", CultureInfo.InvariantCulture);
        if (a >= 10) return v.ToString("0.0", CultureInfo.InvariantCulture);
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
