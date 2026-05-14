namespace SIMS.Application.Common;

public sealed class DocumentMergeData
{
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> RepeatingValues { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
