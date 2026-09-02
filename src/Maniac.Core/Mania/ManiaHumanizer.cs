using Maniac.Core.Planning;
using Maniac.Core.Settings;

namespace Maniac.Core.Mania;

public sealed class ManiaHumanizer : IPlayPlanTransform<ManiaPlayPlan>
{
    readonly ManiaHumanParams p;
    readonly Random rng;

    public ManiaHumanizer(ManiaSettings settings, int seed)
        : this(ManiaHumanParams.From(settings), seed)
    {
    }

    public ManiaHumanizer(ManiaHumanParams parameters, int seed)
    {
        p = parameters;
        rng = new Random(seed);
    }

    public ManiaPlayPlan Apply(ManiaPlayPlan plan)
    {
        var hits = plan.Hits;
        if (hits.Length == 0)
            return plan;

        int n = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            if ((uint)hits[i].Column <= ManiaPlanner.MaxColumn)
                n++;
        }

        if (n == 0)
            return new ManiaPlayPlan([], 0, 0, hits);

        var start = new double[n];
        var end = new double[n];
        var column = new int[n];
        var isHold = new bool[n];
        var nps = new double[n];
        var press = new double[n];
        var release = new double[n];
        var miss = new bool[n];
        var order = new int[n];

        int w = 0;
        int columns = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            int col = hits[i].Column;
            if ((uint)col > ManiaPlanner.MaxColumn)
                continue;
            start[w] = hits[i].Start;
            end[w] = hits[i].End;
            column[w] = col;
            isHold[w] = hits[i].IsHold;
            order[w] = w;
            if (col + 1 > columns)
                columns = col + 1;
            w++;
        }

        var bias = new double[ManiaPlanner.MaxColumn + 1];
        for (int c = 0; c < bias.Length; c++)
            bias[c] = Gaussian(0, p.FingerBiasSigma);

        Array.Sort(order, (a, b) =>
        {
            int cmp = start[a].CompareTo(start[b]);
            return cmp != 0 ? cmp : column[a].CompareTo(column[b]);
        });

        FillNps(start, order, nps);

        double residual = 0;
        double phi = Math.Clamp(p.Phi, 0, 0.95);
        double innovScale = Math.Sqrt(Math.Max(1e-8, 1 - phi * phi));
        var extras = new double[n];
        var group = new int[n];

        for (int i = 0; i < n;)
        {
            int j = i + 1;
            double t0 = start[order[i]];
            while (j < n && start[order[j]] - t0 <= 1)
                j++;

            int count = j - i;
            double groupNps = nps[order[i]];
            for (int k = i + 1; k < j; k++)
                groupNps = Math.Max(groupNps, nps[order[k]]);

            double sigma = p.SigmaAt(groupNps);
            residual = phi * residual + Gaussian(0, sigma * innovScale);

            bool chord = count >= 2;
            if (chord)
            {
                for (int k = 0; k < count; k++)
                    extras[k] = Gaussian(0, p.ChordSigma);
                ClampChord(extras, count, p.ChordWindow);
            }

            for (int k = 0; k < count; k++)
            {
                int idx = order[i + k];
                double extra = chord ? extras[k] : 0;
                press[idx] = start[idx] + bias[column[idx]] + residual + extra + p.EarlyLate;
                group[k] = idx;
            }

            if (chord)
                SpreadChordTimes(press, group, count);

            i = j;
        }

        for (int i = 0; i < n; i++)
        {
            double sigma = p.SigmaAt(nps[i]);
            if (isHold[i])
            {
                double tail = end[i] + Gaussian(0, sigma) + p.HoldTailBias;
                release[i] = Math.Max(press[i] + 1, tail);
            }
            else
            {
                double duration = Truncated(p.TapLength, ManiaHumanParams.TapDurationStd, ManiaHumanParams.MinTapMs, ManiaHumanParams.MaxTapMs);
                release[i] = press[i] + duration;
            }
        }

        ApplyMisses(start, order, nps, miss);
        RepairColumns(start, column, press, release, miss, n, columns);

        int kept = 0;
        for (int i = 0; i < n; i++)
        {
            if (!miss[i])
                kept++;
        }

        var notes = new ManiaPressRelease[kept];
        int o = 0;
        for (int i = 0; i < n; i++)
        {
            if (miss[i])
                continue;
            notes[o++] = new ManiaPressRelease(press[i], release[i], column[i]);
        }

        return ManiaPlanner.FromPressReleases(hits, notes, columns);
    }

    void ApplyMisses(double[] start, int[] order, double[] nps, bool[] miss)
    {
        double burstUntil = double.NegativeInfinity;
        for (int k = 0; k < order.Length; k++)
        {
            int i = order[k];
            double rate = p.MissRate * Math.Pow(Math.Max(nps[i], 1) / ManiaHumanParams.MissNpsRef, 1.2);
            rate = Math.Clamp(rate, 0, 0.4);
            if (start[i] <= burstUntil)
                rate = Math.Min(0.4, rate * 2.2);
            if (rng.NextDouble() < rate)
            {
                miss[i] = true;
                burstUntil = start[i] + 160;
            }
        }
    }

    static void FillNps(double[] start, int[] order, double[] nps)
    {
        int n = order.Length;
        int lo = 0;
        int hi = 0;
        double window = ManiaHumanParams.DensityWindowMs;
        for (int k = 0; k < n; k++)
        {
            double t = start[order[k]];
            while (t - start[order[lo]] > window)
                lo++;
            while (hi + 1 < n && start[order[hi + 1]] - t <= window)
                hi++;
            nps[order[k]] = (hi - lo + 1) / (window * 2 / 1000);
        }
    }

    static void ClampChord(double[] extras, int count, double window)
    {
        if (count < 2 || window <= 0)
            return;

        double min = extras[0];
        double max = extras[0];
        double sum = extras[0];
        for (int i = 1; i < count; i++)
        {
            double v = extras[i];
            sum += v;
            if (v < min)
                min = v;
            if (v > max)
                max = v;
        }

        double span = max - min;
        if (span <= window)
            return;

        double mean = sum / count;
        double scale = window / span;
        for (int i = 0; i < count; i++)
            extras[i] = mean + (extras[i] - mean) * scale;
    }

    static void SpreadChordTimes(double[] press, int[] group, int count)
    {
        var ranked = new int[count];
        for (int i = 0; i < count; i++)
            ranked[i] = i;
        Array.Sort(ranked, (a, b) => press[group[a]].CompareTo(press[group[b]]));

        for (int i = 1; i < count; i++)
        {
            int cur = group[ranked[i]];
            int prev = group[ranked[i - 1]];
            double floorPrev = Math.Floor(press[prev]);
            if (Math.Floor(press[cur]) <= floorPrev)
                press[cur] = floorPrev + 1;
        }
    }

    static void RepairColumns(double[] start, int[] column, double[] press, double[] release, bool[] miss, int n, int columns)
    {
        if (columns <= 0)
            return;

        var lists = new List<int>[columns];
        for (int i = 0; i < n; i++)
        {
            if (miss[i])
                continue;
            int col = column[i];
            lists[col] ??= new List<int>();
            lists[col].Add(i);
        }

        for (int c = 0; c < columns; c++)
        {
            var list = lists[c];
            if (list == null || list.Count == 0)
                continue;

            list.Sort((a, b) =>
            {
                int cmp = start[a].CompareTo(start[b]);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            EnforceColumnOrder(list, press, release);

            for (int i = 0; i < list.Count; i++)
            {
                int cur = list[i];
                if (i + 1 >= list.Count)
                    continue;
                int next = list[i + 1];
                double gap = press[next] - press[cur];
                if (gap <= 1)
                {
                    press[next] = press[cur] + 2;
                    release[cur] = press[cur] + 1;
                    continue;
                }

                if (release[cur] >= press[next])
                    release[cur] = press[cur] + gap * 0.9;
                if (release[cur] >= press[next])
                    release[cur] = press[next] - 1;
                if (release[cur] <= press[cur])
                    release[cur] = press[cur] + 1;
            }

            EnforceColumnOrder(list, press, release);
        }
    }

    static void EnforceColumnOrder(List<int> list, double[] press, double[] release)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int cur = list[i];
            double minPress = i == 0 ? double.NegativeInfinity : Math.Max(press[list[i - 1]] + 1, release[list[i - 1]] + 1);
            if (press[cur] < minPress)
                press[cur] = minPress;
            if (release[cur] <= press[cur])
                release[cur] = press[cur] + 1;
        }
    }

    double Truncated(double mean, double std, double min, double max) =>
        Math.Clamp(Gaussian(mean, std), min, max);

    double Gaussian(double mean, double std)
    {
        if (std <= 0)
            return mean;
        double u1 = 1 - rng.NextDouble();
        if (u1 < 1e-12)
            u1 = 1e-12;
        double u2 = rng.NextDouble();
        double z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        return mean + std * z;
    }
}
