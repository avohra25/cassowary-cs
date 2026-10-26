namespace Cassowary;

/// <summary>A variable scaled by a coefficient, e.g. <c>3*x</c>.</summary>
public readonly record struct Term(Variable Variable, double Coefficient = 1.0)
{
    /// <summary>The coefficient times the variable's current value.</summary>
    public double Value => Coefficient * Variable.Value;

    /// <inheritdoc />
    public override string ToString() => Coefficient switch
    {
        1.0 => Variable.ToString(),
        -1.0 => "-" + Variable,
        _ => $"{Format.Number(Coefficient)}*{Variable}",
    };
}
