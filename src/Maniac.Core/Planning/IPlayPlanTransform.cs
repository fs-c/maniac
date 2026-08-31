namespace Maniac.Core.Planning;

/// <summary>
/// Optional pre-gameplay rewrite of an immutable play plan.
/// Future customization/humanization should implement this rather than changing drivers.
/// </summary>
public interface IPlayPlanTransform<TPlan>
{
    TPlan Apply(TPlan plan);
}

public static class PlayPlanTransforms
{
    public static TPlan ApplyAll<TPlan>(TPlan plan, IReadOnlyList<IPlayPlanTransform<TPlan>>? transforms)
    {
        if (transforms == null || transforms.Count == 0)
            return plan;

        for (int i = 0; i < transforms.Count; i++)
            plan = transforms[i].Apply(plan);
        return plan;
    }
}
