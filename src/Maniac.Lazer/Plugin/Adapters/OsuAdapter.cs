using System.Reflection;
using Maniac.Core.Geometry;
using Maniac.Core.Osu;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Play;
using osuTK;

namespace Maniac.Plugin.Adapters;

static class OsuAdapter
{
    static readonly Dictionary<Type, Func<HitObject, Vec2>> Positions = [];

    public static OsuPlayPlan Build(GameplayState state)
    {
        var builder = new OsuPlanBuilder();
        var objects = state.Beatmap.HitObjects;
        Span<Vec2> buffer = stackalloc Vec2[512];

        for (int i = 0; i < objects.Count; i++)
        {
            var hit = objects[i];
            Vec2 pos = PositionOf(hit);
            double start = hit.StartTime;
            double end = hit.GetEndTime();
            double preempt = hit is IHasTimePreempt tp ? tp.TimePreempt : 600;

            if (hit is IHasPathWithRepeats path)
            {
                int samples = SamplePath(path, pos, start, end, buffer);
                builder.AddSlider(start, end, pos, preempt, buffer[..samples]);
            }
            else if (hit is IHasDuration duration && duration.Duration > 0)
            {
                builder.AddSpinner(start, end, pos, preempt);
            }
            else
            {
                builder.AddCircle(start, pos, preempt);
            }
        }

        return builder.Build();
    }

    static int SamplePath(IHasPathWithRepeats path, Vec2 start, double startTime, double endTime, Span<Vec2> buffer)
    {
        double duration = Math.Max(0, endTime - startTime);
        int samples = (int)Math.Clamp(Math.Ceiling(duration / 16.0) + 1, 2, buffer.Length);
        for (int i = 0; i < samples; i++)
        {
            double progress = samples == 1 ? 0 : i / (double)(samples - 1);
            var offset = path.CurvePositionAt(progress);
            buffer[i] = start + new Vec2(offset.X, offset.Y);
        }

        return samples;
    }

    static Vec2 PositionOf(HitObject hit)
    {
        var type = hit.GetType();
        if (!Positions.TryGetValue(type, out var getter))
        {
            var prop = type.GetProperty("StackedPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                getter = h => prop.GetValue(h) is Vector2 v
                    ? new Vec2(v.X, v.Y)
                    : Fallback(h);
            }
            else
            {
                getter = Fallback;
            }

            Positions[type] = getter;
        }

        return getter(hit);
    }

    static Vec2 Fallback(HitObject hit) =>
        hit is IHasPosition p ? new Vec2(p.Position.X, p.Position.Y) : Vec2.PlayfieldCentre;
}
