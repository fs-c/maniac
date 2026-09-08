namespace Maniac.Core.Planning;

/// <summary>
/// Source plan plus optional rewrites. Drivers consume <see cref="Output"/>.
/// </summary>
public sealed class PlayPlanPipeline<TPlan>
{
    public TPlan? Source { get; private set; }
    public TPlan? Output { get; private set; }
    public bool HasSource => Source != null;

    public void Clear()
    {
        Source = default;
        Output = default;
    }

    public TPlan SetSource(TPlan plan, IReadOnlyList<IPlayPlanTransform<TPlan>>? transforms = null)
    {
        Source = plan;
        Output = PlayPlanTransforms.ApplyAll(plan, transforms);
        return Output;
    }

    public TPlan Rebuild(IReadOnlyList<IPlayPlanTransform<TPlan>>? transforms)
    {
        if (Source == null)
            throw new InvalidOperationException("No source plan.");
        Output = PlayPlanTransforms.ApplyAll(Source, transforms);
        return Output;
    }
}
