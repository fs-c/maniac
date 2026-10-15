using Maniac.Core.Mania;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Play;

namespace Maniac.Plugin.Adapters;

static class ManiaAdapter
{
    public static ManiaPlayPlan Build(GameplayState state)
    {
        var objects = state.Beatmap.HitObjects;
        int count = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] is IHasColumn)
                count++;
        }

        var hits = new ManiaHit[count];
        int w = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            var hit = objects[i];
            if (hit is not IHasColumn column)
                continue;
            bool hasDuration = hit is IHasDuration;
            double duration = hasDuration ? ((IHasDuration)hit).Duration : 0;
            hits[w++] = new ManiaHit(
                hit.StartTime,
                hit.GetEndTime(),
                column.Column,
                duration > 0,
                hasDuration && duration == 0);
        }

        return ManiaPlanner.Build(hits);
    }
}
