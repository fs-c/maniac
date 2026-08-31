namespace ManiaHuman;

enum Judgement
{
    Miss,
    Meh,
    Ok,
    Good,
    Great,
    Perfect,
}

readonly struct HitWindows
{
    public readonly double Perfect, Great, Good, Ok, Meh, Miss;

    public HitWindows(double od, double rate, bool classic, bool easy, bool hardRock, bool convert)
    {
        double difficulty = 1;
        if (easy) difficulty = 1 / 1.4;
        if (hardRock) difficulty = 1.4;
        double total = rate / difficulty;

        if (classic)
        {
            if (convert)
            {
                Perfect = FloorHalf(16 * total);
                Great = FloorHalf((Math.Round(od) > 4 ? 34 : 47) * total);
                Good = FloorHalf((Math.Round(od) > 4 ? 67 : 77) * total);
                Ok = FloorHalf(97 * total);
                Meh = FloorHalf(121 * total);
                Miss = FloorHalf(158 * total);
            }
            else
            {
                double inverted = Math.Clamp(10 - od, 0, 10);
                Perfect = FloorHalf(16 * total);
                Great = FloorHalf((34 + 3 * inverted) * total);
                Good = FloorHalf((67 + 3 * inverted) * total);
                Ok = FloorHalf((97 + 3 * inverted) * total);
                Meh = FloorHalf((121 + 3 * inverted) * total);
                Miss = FloorHalf((158 + 3 * inverted) * total);
            }
        }
        else
        {
            Perfect = FloorHalf(Range(od, 22.4, 19.4, 13.9) * total);
            Great = FloorHalf(Range(od, 64, 49, 34) * total);
            Good = FloorHalf(Range(od, 97, 82, 67) * total);
            Ok = FloorHalf(Range(od, 127, 112, 97) * total);
            Meh = FloorHalf(Range(od, 151, 136, 121) * total);
            Miss = FloorHalf(Range(od, 188, 173, 158) * total);
        }
    }

    public Judgement Judge(double error)
    {
        double a = Math.Abs(error);
        if (a <= Perfect) return Judgement.Perfect;
        if (a <= Great) return Judgement.Great;
        if (a <= Good) return Judgement.Good;
        if (a <= Ok) return Judgement.Ok;
        if (a <= Meh) return Judgement.Meh;
        return Judgement.Miss;
    }

    static double FloorHalf(double v) => Math.Floor(v) + 0.5;

    static double Range(double difficulty, double min, double mid, double max)
    {
        double t = (difficulty - 5) / 5;
        if (difficulty > 5) return mid + (max - mid) * t;
        if (difficulty < 5) return mid + (mid - min) * t;
        return mid;
    }
}

enum HitKind
{
    Tap,
    HoldHead,
    HoldTail,
}

sealed class HitSample
{
    public required ManiaNote Note;
    public required HitKind Kind;
    public double PressTime;
    public double? ReleaseTime;
    public double Error;          // action time - note time (negative = early)
    public Judgement Judgement;
    public bool Hit;
    public double LocalNps;
}

sealed class MatchResult
{
    public required OsrReplay Replay;
    public required ManiaBeatmap Beatmap;
    public required HitWindows Windows;
    public required HitSample[] Hits;
    public int GhostTaps;
    public int ExtraReleases;

    public IEnumerable<HitSample> Matched => Hits.Where(h => h.Hit);
    public IEnumerable<HitSample> Heads => Hits.Where(h => h.Kind != HitKind.HoldTail);
}

static class NoteMatcher
{
    public static MatchResult Match(OsrReplay replay, ManiaBeatmap beatmap)
    {
        var notes = ApplyMods(beatmap.Notes, beatmap.Columns, replay);
        var windows = new HitWindows(
            beatmap.OverallDifficulty,
            replay.Rate,
            replay.HasClassicWindows,
            replay.HasAcronym("EZ") || replay.LegacyMods.HasFlag(LegacyMods.Easy),
            replay.HasAcronym("HR") || replay.LegacyMods.HasFlag(LegacyMods.HardRock),
            convert: false);

        var hits = new List<HitSample>(notes.Length * 2);
        var byColumn = new List<ManiaNote>[Math.Max(beatmap.Columns, 1)];
        for (int i = 0; i < byColumn.Length; i++)
            byColumn[i] = [];
        foreach (var n in notes)
        {
            int c = Math.Clamp(n.Column, 0, byColumn.Length - 1);
            byColumn[c].Add(n);
        }

        var events = replay.Events;
        int ghost = 0, extraUp = 0;
        var downAt = new double?[20];
        var openHit = new HitSample?[20];
        var cursor = new int[byColumn.Length];

        foreach (var e in events)
        {
            int col = e.Column;
            if (col >= byColumn.Length)
            {
                if (e.Down) ghost++;
                continue;
            }

            if (e.Down)
            {
                downAt[col] = e.Time;
                int i = cursor[col];
                var list = byColumn[col];
                while (i < list.Count && e.Time - list[i].Start > windows.Miss)
                    i++;
                cursor[col] = i;
                if (i >= list.Count || Math.Abs(e.Time - list[i].Start) > windows.Miss)
                {
                    ghost++;
                    continue;
                }

                var note = list[i];
                cursor[col] = i + 1;
                var sample = new HitSample
                {
                    Note = note,
                    Kind = note.IsHold ? HitKind.HoldHead : HitKind.Tap,
                    PressTime = e.Time,
                    Error = e.Time - note.Start,
                    Judgement = windows.Judge(e.Time - note.Start),
                    Hit = true,
                };
                hits.Add(sample);
                openHit[col] = sample;
            }
            else
            {
                double press = downAt[col] ?? e.Time;
                downAt[col] = null;
                var last = openHit[col];
                openHit[col] = null;
                if (last is null)
                {
                    extraUp++;
                    continue;
                }

                last.ReleaseTime = e.Time;
                if (last.Kind == HitKind.HoldHead)
                {
                    double err = e.Time - last.Note.End;
                    hits.Add(new HitSample
                    {
                        Note = last.Note,
                        Kind = HitKind.HoldTail,
                        PressTime = press,
                        ReleaseTime = e.Time,
                        Error = err,
                        Judgement = windows.Judge(err),
                        Hit = Math.Abs(err) <= windows.Miss,
                    });
                }
            }
        }

        var matchedNotes = new HashSet<ManiaNote>(hits.Where(h => h.Kind != HitKind.HoldTail).Select(h => h.Note));
        foreach (var note in notes)
        {
            if (matchedNotes.Contains(note))
                continue;
            hits.Add(new HitSample
            {
                Note = note,
                Kind = note.IsHold ? HitKind.HoldHead : HitKind.Tap,
                PressTime = double.NaN,
                Error = double.NaN,
                Judgement = Judgement.Miss,
                Hit = false,
            });
            if (note.IsHold)
            {
                hits.Add(new HitSample
                {
                    Note = note,
                    Kind = HitKind.HoldTail,
                    PressTime = double.NaN,
                    Error = double.NaN,
                    Judgement = Judgement.Miss,
                    Hit = false,
                });
            }
        }

        hits.Sort((a, b) => a.Note.Start.CompareTo(b.Note.Start));
        FillDensity(hits, notes);

        return new MatchResult
        {
            Replay = replay,
            Beatmap = beatmap,
            Windows = windows,
            Hits = hits.ToArray(),
            GhostTaps = ghost,
            ExtraReleases = extraUp,
        };
    }

    static ManiaNote[] ApplyMods(ManiaNote[] notes, int columns, OsrReplay replay)
    {
        if (!replay.Mirror)
            return notes;
        var copy = new ManiaNote[notes.Length];
        for (int i = 0; i < notes.Length; i++)
        {
            var n = notes[i];
            copy[i] = new ManiaNote
            {
                Start = n.Start,
                End = n.End,
                Column = columns - 1 - n.Column,
                IsHold = n.IsHold,
            };
        }

        return copy;
    }

    static void FillDensity(List<HitSample> hits, ManiaNote[] notes)
    {
        const double window = 300;
        int j0 = 0, j1 = 0;
        foreach (var hit in hits)
        {
            double t = hit.Note.Start;
            while (j0 < notes.Length && notes[j0].Start < t - window)
                j0++;
            while (j1 < notes.Length && notes[j1].Start <= t + window)
                j1++;
            hit.LocalNps = (j1 - j0) / (window * 2 / 1000.0);
        }
    }
}
