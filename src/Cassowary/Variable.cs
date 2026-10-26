using System.Threading;

namespace Cassowary;

/// <summary>
/// An unknown in the constraint system. Variables use reference identity: two
/// variables with the same name are still distinct unknowns.
/// </summary>
public sealed class Variable
{
    private static long s_nextId;

    /// <summary>Creates a new variable with an optional debugging name.</summary>
    public Variable(string name = "")
    {
        Name = name;
        Id = Interlocked.Increment(ref s_nextId);
    }

    /// <summary>Human-readable name used in diagnostics and <see cref="ToString"/>.</summary>
    public string Name { get; }

    /// <summary>
    /// The value computed by the most recent <see cref="Solver.UpdateVariables"/> call.
    /// </summary>
    public double Value { get; internal set; }

    /// <summary>Process-unique creation counter; gives a stable ordering for output.</summary>
    internal long Id { get; }

    /// <inheritdoc />
    public override string ToString() => Name.Length > 0 ? Name : $"v{Id}";

    // ---- arithmetic: everything produces an Expression ----

    /// <summary>Multiplies a variable by a constant.</summary>
    public static Expression operator *(Variable v, double c) => new(new Term(v, c));
    /// <summary>Multiplies a variable by a constant.</summary>
    public static Expression operator *(double c, Variable v) => new(new Term(v, c));
    /// <summary>Divides a variable by a constant.</summary>
    public static Expression operator /(Variable v, double c) => new(new Term(v, 1.0 / c));
    /// <summary>Negates a variable.</summary>
    public static Expression operator -(Variable v) => new(new Term(v, -1.0));
    /// <summary>Adds two variables.</summary>
    public static Expression operator +(Variable a, Variable b) => (Expression)a + b;
    /// <summary>Subtracts two variables.</summary>
    public static Expression operator -(Variable a, Variable b) => (Expression)a - b;
    /// <summary>Adds a constant to a variable.</summary>
    public static Expression operator +(Variable a, double c) => (Expression)a + c;
    /// <summary>Adds a constant to a variable.</summary>
    public static Expression operator +(double c, Variable a) => c + (Expression)a;
    /// <summary>Subtracts a constant from a variable.</summary>
    public static Expression operator -(Variable a, double c) => (Expression)a - c;
    /// <summary>Subtracts a variable from a constant.</summary>
    public static Expression operator -(double c, Variable a) => c - (Expression)a;

    // ---- relations ----

    /// <summary>Builds the required constraint <c>v &lt;= rhs</c>.</summary>
    public static Constraint operator <=(Variable v, Variable rhs) => (Expression)v <= rhs;
    /// <summary>Builds the required constraint <c>v &gt;= rhs</c>.</summary>
    public static Constraint operator >=(Variable v, Variable rhs) => (Expression)v >= rhs;
    /// <summary>Builds the required constraint <c>v &lt;= c</c>.</summary>
    public static Constraint operator <=(Variable v, double c) => (Expression)v <= c;
    /// <summary>Builds the required constraint <c>v &gt;= c</c>.</summary>
    public static Constraint operator >=(Variable v, double c) => (Expression)v >= c;
    /// <summary>Builds the required constraint <c>c &lt;= v</c>.</summary>
    public static Constraint operator <=(double c, Variable v) => c <= (Expression)v;
    /// <summary>Builds the required constraint <c>c &gt;= v</c>.</summary>
    public static Constraint operator >=(double c, Variable v) => c >= (Expression)v;

    /// <summary>Builds the required constraint <c>this == rhs</c>.</summary>
    public Constraint Eq(Expression rhs) => ((Expression)this).Eq(rhs);
    /// <summary>Builds the required constraint <c>this &lt;= rhs</c>.</summary>
    public Constraint Le(Expression rhs) => ((Expression)this).Le(rhs);
    /// <summary>Builds the required constraint <c>this &gt;= rhs</c>.</summary>
    public Constraint Ge(Expression rhs) => ((Expression)this).Ge(rhs);
}
