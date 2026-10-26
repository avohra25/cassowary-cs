namespace Cassowary;

internal enum SymbolKind
{
    Invalid,
    External,
    Slack,
    Error,
    Dummy,
}

/// <summary>
/// A column of the simplex tableau. External symbols stand for user variables;
/// slack, error and dummy symbols are introduced per constraint.
/// </summary>
internal sealed class Symbol
{
    public static readonly Symbol Invalid = new(0, SymbolKind.Invalid);

    public Symbol(long id, SymbolKind kind)
    {
        Id = id;
        Kind = kind;
    }

    public long Id { get; }
    public SymbolKind Kind { get; }

    public bool IsValid => Kind != SymbolKind.Invalid;
    public bool IsExternal => Kind == SymbolKind.External;
    public bool IsDummy => Kind == SymbolKind.Dummy;
    /// <summary>Slack and error symbols are restricted to be non-negative and may enter the basis freely.</summary>
    public bool IsPivotable => Kind is SymbolKind.Slack or SymbolKind.Error;

    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    public override string ToString() => Kind switch
    {
        SymbolKind.External => $"v{Id}",
        SymbolKind.Slack => $"s{Id}",
        SymbolKind.Error => $"e{Id}",
        SymbolKind.Dummy => $"d{Id}",
        _ => "i",
    };
}
