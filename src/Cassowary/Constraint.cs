namespace Cassowary;

/// <summary>How a constraint's expression compares with zero.</summary>
public enum RelationalOperator
{
    /// <summary><c>expr &lt;= 0</c></summary>
    LessOrEqual,
    /// <summary><c>expr &gt;= 0</c></summary>
    GreaterOrEqual,
    /// <summary><c>expr == 0</c></summary>
    Equal,
}

/// <summary>
/// An immutable linear constraint <c>expression OP 0</c> with a strength.
/// Constraints use reference identity: adding two separately built but
/// identical constraints to a solver is allowed, adding the same instance twice is not.
/// </summary>
public sealed class Constraint
{
    /// <summary>Creates a constraint; the expression is reduced (like terms merged).</summary>
    public Constraint(Expression expression, RelationalOperator op, double strength = double.NaN)
    {
        Expression = expression.Reduce();
        Operator = op;
        Strength = double.IsNaN(strength) ? Cassowary.Strength.Required : Cassowary.Strength.Clip(strength);
    }

    /// <summary>Copies <paramref name="other"/> with a different strength.</summary>
    public Constraint(Constraint other, double strength)
    {
        Expression = other.Expression;
        Operator = other.Operator;
        Strength = Cassowary.Strength.Clip(strength);
    }

    /// <summary>The reduced left-hand side; the right-hand side is always zero.</summary>
    public Expression Expression { get; }

    /// <summary>The relation between <see cref="Expression"/> and zero.</summary>
    public RelationalOperator Operator { get; }

    /// <summary>The constraint's strength (see <see cref="Cassowary.Strength"/>).</summary>
    public double Strength { get; }

    /// <summary>True if the strength is <see cref="Cassowary.Strength.Required"/>.</summary>
    public bool IsRequired => Strength >= Cassowary.Strength.Required;

    /// <summary>Returns a copy of this constraint with another strength.</summary>
    public Constraint WithStrength(double strength) => new(this, strength);

    /// <summary>
    /// How far the current variable values are from satisfying the constraint
    /// (0 when satisfied).
    /// </summary>
    public double Violation
    {
        get
        {
            double v = Expression.Value;
            return Operator switch
            {
                RelationalOperator.LessOrEqual => System.Math.Max(0.0, v),
                RelationalOperator.GreaterOrEqual => System.Math.Max(0.0, -v),
                _ => System.Math.Abs(v),
            };
        }
    }

    /// <summary>Applies a strength to a constraint: <c>(x &gt;= 10) | Strength.Weak</c>.</summary>
    public static Constraint operator |(Constraint c, double strength) => c.WithStrength(strength);

    /// <inheritdoc />
    public override string ToString()
    {
        string op = Operator switch
        {
            RelationalOperator.LessOrEqual => "<=",
            RelationalOperator.GreaterOrEqual => ">=",
            _ => "==",
        };
        return $"{Expression} {op} 0 | {Cassowary.Strength.Describe(Strength)}";
    }
}
