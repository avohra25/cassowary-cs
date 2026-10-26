using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cassowary;

/// <summary>
/// An immutable linear expression <c>c1*v1 + c2*v2 + ... + constant</c>.
/// Arithmetic operators build new expressions; relational operators and
/// <see cref="Eq"/>/<see cref="Le"/>/<see cref="Ge"/> build <see cref="Constraint"/>s.
/// </summary>
public sealed class Expression
{
    private static readonly Term[] s_noTerms = Array.Empty<Term>();

    /// <summary>Creates a constant expression.</summary>
    public Expression(double constant = 0.0) : this(s_noTerms, constant) { }

    /// <summary>Creates an expression from a single term.</summary>
    public Expression(Term term, double constant = 0.0) : this(new[] { term }, constant) { }

    /// <summary>Creates an expression from terms and a constant. Terms are kept as given.</summary>
    public Expression(IEnumerable<Term> terms, double constant = 0.0)
    {
        Terms = terms.ToArray();
        Constant = constant;
    }

    /// <summary>The (not necessarily reduced) terms of the expression.</summary>
    public IReadOnlyList<Term> Terms { get; }

    /// <summary>The constant part of the expression.</summary>
    public double Constant { get; }

    /// <summary>True when the expression has no variable terms.</summary>
    public bool IsConstant => Terms.Count == 0;

    /// <summary>Evaluates the expression with the variables' current values.</summary>
    public double Value
    {
        get
        {
            double sum = Constant;
            foreach (var t in Terms) sum += t.Value;
            return sum;
        }
    }

    /// <summary>
    /// Merges terms that share a variable and drops zero coefficients. Terms are
    /// ordered by variable creation order so equal expressions print equally.
    /// </summary>
    public Expression Reduce()
    {
        var merged = new Dictionary<Variable, double>();
        foreach (var t in Terms)
        {
            merged.TryGetValue(t.Variable, out double c);
            merged[t.Variable] = c + t.Coefficient;
        }
        var terms = merged
            .Where(kv => kv.Value != 0.0)
            .OrderBy(kv => kv.Key.Id)
            .Select(kv => new Term(kv.Key, kv.Value));
        return new Expression(terms, Constant);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Terms.Count == 0) return Format.Number(Constant);
        var sb = new StringBuilder();
        for (int i = 0; i < Terms.Count; i++)
        {
            var t = Terms[i];
            if (i == 0) sb.Append(t.ToString());
            else if (t.Coefficient < 0) sb.Append(" - ").Append(new Term(t.Variable, -t.Coefficient));
            else sb.Append(" + ").Append(t);
        }
        if (Constant > 0) sb.Append(" + ").Append(Format.Number(Constant));
        else if (Constant < 0) sb.Append(" - ").Append(Format.Number(-Constant));
        return sb.ToString();
    }

    // ---- conversions ----

    /// <summary>Lifts a constant into an expression.</summary>
    public static implicit operator Expression(double constant) => new(constant);
    /// <summary>Lifts a variable into an expression.</summary>
    public static implicit operator Expression(Variable v) => new(new Term(v));
    /// <summary>Lifts a term into an expression.</summary>
    public static implicit operator Expression(Term t) => new(t);

    // ---- arithmetic ----

    /// <summary>Adds two expressions.</summary>
    public static Expression operator +(Expression a, Expression b) =>
        new(a.Terms.Concat(b.Terms), a.Constant + b.Constant);
    /// <summary>Adds a constant.</summary>
    public static Expression operator +(Expression a, double c) => new(a.Terms, a.Constant + c);
    /// <summary>Adds a constant.</summary>
    public static Expression operator +(double c, Expression a) => a + c;
    /// <summary>Negates an expression.</summary>
    public static Expression operator -(Expression a) => a * -1.0;
    /// <summary>Subtracts two expressions.</summary>
    public static Expression operator -(Expression a, Expression b) => a + -b;
    /// <summary>Subtracts a constant.</summary>
    public static Expression operator -(Expression a, double c) => a + -c;
    /// <summary>Subtracts an expression from a constant.</summary>
    public static Expression operator -(double c, Expression a) => -a + c;
    /// <summary>Scales an expression.</summary>
    public static Expression operator *(Expression a, double c) =>
        new(a.Terms.Select(t => t with { Coefficient = t.Coefficient * c }), a.Constant * c);
    /// <summary>Scales an expression.</summary>
    public static Expression operator *(double c, Expression a) => a * c;
    /// <summary>Divides an expression by a constant.</summary>
    public static Expression operator /(Expression a, double c) => a * (1.0 / c);

    // ---- relations ----

    /// <summary>Builds the required constraint <c>this == rhs</c>.</summary>
    public Constraint Eq(Expression rhs) => new(this - rhs, RelationalOperator.Equal);
    /// <summary>Builds the required constraint <c>this &lt;= rhs</c>.</summary>
    public Constraint Le(Expression rhs) => new(this - rhs, RelationalOperator.LessOrEqual);
    /// <summary>Builds the required constraint <c>this &gt;= rhs</c>.</summary>
    public Constraint Ge(Expression rhs) => new(this - rhs, RelationalOperator.GreaterOrEqual);

    /// <summary>Builds the required constraint <c>a &lt;= b</c>.</summary>
    public static Constraint operator <=(Expression a, Expression b) => a.Le(b);
    /// <summary>Builds the required constraint <c>a &gt;= b</c>.</summary>
    public static Constraint operator >=(Expression a, Expression b) => a.Ge(b);
    /// <summary>Builds the required constraint <c>a &lt;= c</c>.</summary>
    public static Constraint operator <=(Expression a, double c) => a.Le(c);
    /// <summary>Builds the required constraint <c>a &gt;= c</c>.</summary>
    public static Constraint operator >=(Expression a, double c) => a.Ge(c);
    /// <summary>Builds the required constraint <c>c &lt;= a</c>.</summary>
    public static Constraint operator <=(double c, Expression a) => a.Ge(c);
    /// <summary>Builds the required constraint <c>c &gt;= a</c>.</summary>
    public static Constraint operator >=(double c, Expression a) => a.Le(c);
}
