using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cassowary;

/// <summary>
/// One tableau row: <c>basic = constant + sum(coeff * symbol)</c>. The basic
/// symbol itself is the key in <see cref="Solver"/>'s row map, not stored here.
/// </summary>
internal sealed class Row
{
    public const double Epsilon = 1.0e-8;

    public Row(double constant = 0.0)
    {
        Constant = constant;
        Cells = new Dictionary<Symbol, double>();
    }

    public Row(Row other)
    {
        Constant = other.Constant;
        Cells = new Dictionary<Symbol, double>(other.Cells);
    }

    public double Constant { get; private set; }
    public Dictionary<Symbol, double> Cells { get; }

    public static bool NearZero(double v) => v < 0.0 ? -v < Epsilon : v < Epsilon;

    /// <summary>Adds to the constant and returns the new constant.</summary>
    public double Add(double value) => Constant += value;

    /// <summary>Adds <paramref name="coefficient"/>*symbol, dropping the cell if it cancels out.</summary>
    public void Insert(Symbol symbol, double coefficient = 1.0)
    {
        Cells.TryGetValue(symbol, out double existing);
        double updated = existing + coefficient;
        if (NearZero(updated)) Cells.Remove(symbol);
        else Cells[symbol] = updated;
    }

    /// <summary>Adds <paramref name="coefficient"/> times another row (constant included).</summary>
    public void Insert(Row other, double coefficient = 1.0)
    {
        Constant += other.Constant * coefficient;
        foreach (var (symbol, c) in other.Cells) Insert(symbol, c * coefficient);
    }

    public void Remove(Symbol symbol) => Cells.Remove(symbol);

    public void ReverseSign()
    {
        Constant = -Constant;
        foreach (var symbol in Cells.Keys.ToArray()) Cells[symbol] = -Cells[symbol];
    }

    /// <summary>
    /// Treats the row as <c>0 = constant + ... + a*symbol + ...</c> and rewrites it
    /// as <c>symbol = (constant + ...) / -a</c>, removing the symbol's cell.
    /// </summary>
    public void SolveFor(Symbol symbol)
    {
        double coeff = -1.0 / Cells[symbol];
        Cells.Remove(symbol);
        Constant *= coeff;
        foreach (var s in Cells.Keys.ToArray()) Cells[s] *= coeff;
    }

    /// <summary>
    /// For a row <c>lhs = ... + a*rhs + ...</c>, pivots so that the row reads
    /// <c>rhs = ... + (1/a)*lhs ...</c>.
    /// </summary>
    public void SolveFor(Symbol lhs, Symbol rhs)
    {
        Insert(lhs, -1.0);
        SolveFor(rhs);
    }

    public double CoefficientFor(Symbol symbol) => Cells.TryGetValue(symbol, out double c) ? c : 0.0;

    /// <summary>Replaces <paramref name="symbol"/> with the expression held by <paramref name="row"/>.</summary>
    public void Substitute(Symbol symbol, Row row)
    {
        if (Cells.Remove(symbol, out double coeff)) Insert(row, coeff);
    }

    public override string ToString()
    {
        var sb = new StringBuilder(Format.Number(Constant));
        foreach (var (s, c) in Cells.OrderBy(kv => kv.Key.Id))
            sb.Append(c < 0 ? " - " : " + ").Append(Format.Number(System.Math.Abs(c))).Append('*').Append(s);
        return sb.ToString();
    }
}
