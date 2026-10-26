using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cassowary;

/// <summary>
/// Incremental Cassowary solver (Badros, Borning &amp; Stuckey 2001), following the
/// tableau design popularised by Kiwi. Constraints can be added and removed one
/// at a time; each change re-optimises from the previous solution instead of
/// solving from scratch.
/// <para>
/// The tableau keeps every row in "basic = constant + Σ coeff·parametric" form.
/// Required constraints become rows with a slack (inequalities) or dummy
/// (equalities) marker; non-required constraints additionally get error
/// symbols that appear in the objective weighted by their strength. The
/// primal simplex then minimises the objective, i.e. the weighted sum of
/// constraint violations.
/// </para>
/// </summary>
public sealed partial class Solver
{
    private sealed class Tag
    {
        public Symbol Marker = Symbol.Invalid;
        public Symbol Other = Symbol.Invalid;
    }

    private readonly Dictionary<Constraint, Tag> _constraints = new();
    private readonly Dictionary<Symbol, Row> _rows = new();
    private readonly Dictionary<Variable, Symbol> _vars = new();
    private readonly List<Symbol> _infeasibleRows = new();
    private Row _objective = new();
    private Row? _artificial;
    private long _nextSymbolId = 1;

    /// <summary>Number of constraints currently in the solver (including edit constraints).</summary>
    public int ConstraintCount => _constraints.Count;

    /// <summary>True if this exact constraint instance has been added.</summary>
    public bool HasConstraint(Constraint constraint) => _constraints.ContainsKey(constraint);

    /// <summary>
    /// Adds a constraint and re-optimises.
    /// </summary>
    /// <exception cref="DuplicateConstraintException">The constraint was already added.</exception>
    /// <exception cref="UnsatisfiableConstraintException">A required constraint conflicts with existing required constraints.</exception>
    public void AddConstraint(Constraint constraint)
    {
        if (_constraints.ContainsKey(constraint))
            throw new DuplicateConstraintException(constraint);

        // Build the row with the current basic variables substituted out.
        var row = CreateRow(constraint, out var tag);
        var subject = ChooseSubject(row, tag);

        // A row made purely of dummies is either redundant (constant 0) or contradictory.
        if (!subject.IsValid && AllDummies(row))
        {
            if (!Row.NearZero(row.Constant))
                throw new UnsatisfiableConstraintException(constraint);
            subject = tag.Marker;
        }

        if (!subject.IsValid)
        {
            // No obvious entering symbol: solve a phase-one problem to find a feasible basis.
            if (!AddWithArtificialVariable(row))
                throw new UnsatisfiableConstraintException(constraint);
        }
        else
        {
            row.SolveFor(subject);
            Substitute(subject, row);
            _rows[subject] = row;
        }

        _constraints[constraint] = tag;
        Optimize(_objective);
    }

    /// <summary>Adds several constraints in order.</summary>
    public void AddConstraints(IEnumerable<Constraint> constraints)
    {
        foreach (var c in constraints) AddConstraint(c);
    }

    /// <summary>
    /// Removes a constraint and re-optimises.
    /// </summary>
    /// <exception cref="UnknownConstraintException">The constraint is not in the solver.</exception>
    public void RemoveConstraint(Constraint constraint)
    {
        if (!_constraints.Remove(constraint, out var tag))
            throw new UnknownConstraintException(constraint);

        // Take the error symbols out of the objective before pivoting the marker out.
        RemoveConstraintEffects(constraint, tag);

        // If the marker is basic, its row simply disappears. Otherwise pivot it
        // into the basis using the most restrictive row, then drop that row.
        if (!_rows.Remove(tag.Marker))
        {
            var (leaving, row) = GetMarkerLeavingRow(tag.Marker);
            if (row is null)
                throw new InternalSolverException("failed to find leaving row");
            _rows.Remove(leaving);
            row.SolveFor(leaving, tag.Marker);
            Substitute(tag.Marker, row);
        }

        Optimize(_objective);
    }

    /// <summary>
    /// Copies the solution into <see cref="Variable.Value"/> for every variable
    /// the solver knows about.
    /// </summary>
    public void UpdateVariables()
    {
        foreach (var (variable, symbol) in _vars)
        {
            double value = ValueOf(symbol);
            // Avoid printing "-0" for parametric variables pivoted through negation.
            variable.Value = value == 0.0 ? 0.0 : value;
        }
    }

    /// <summary>Removes every constraint and edit variable.</summary>
    public void Reset()
    {
        _constraints.Clear();
        _rows.Clear();
        _vars.Clear();
        _infeasibleRows.Clear();
        _objective = new Row();
        _artificial = null;
        _nextSymbolId = 1;
    }

    /// <summary>
    /// The weighted violation of all non-required constraints at the current
    /// solution: Σ strength × error. This is what the solver minimises.
    /// </summary>
    public double ObjectiveValue
    {
        get
        {
            double total = 0.0;
            foreach (var (constraint, tag) in _constraints)
            {
                if (tag.Marker.Kind == SymbolKind.Error) total += constraint.Strength * ValueOf(tag.Marker);
                if (tag.Other.Kind == SymbolKind.Error) total += constraint.Strength * ValueOf(tag.Other);
            }
            return total;
        }
    }

    private double ValueOf(Symbol symbol) => _rows.TryGetValue(symbol, out var row) ? row.Constant : 0.0;

    /// <summary>Human-readable dump of the internal tableau, for debugging.</summary>
    public string Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Objective");
        sb.AppendLine("---------");
        sb.AppendLine(_objective.ToString());
        sb.AppendLine();
        sb.AppendLine("Tableau");
        sb.AppendLine("-------");
        foreach (var (symbol, row) in _rows.OrderBy(kv => kv.Key.Id))
            sb.Append(symbol).Append(" | ").AppendLine(row.ToString());
        sb.AppendLine();
        sb.AppendLine("Variables");
        sb.AppendLine("---------");
        foreach (var (variable, symbol) in _vars.OrderBy(kv => kv.Key.Id))
            sb.Append(variable).Append(" = ").AppendLine(symbol.ToString());
        sb.AppendLine();
        sb.AppendLine("Constraints");
        sb.AppendLine("-----------");
        foreach (var c in _constraints.Keys) sb.AppendLine(c.ToString());
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Tableau construction
    // ------------------------------------------------------------------

    private Symbol NewSymbol(SymbolKind kind) => new(_nextSymbolId++, kind);

    private Symbol GetVarSymbol(Variable variable)
    {
        if (!_vars.TryGetValue(variable, out var symbol))
        {
            symbol = NewSymbol(SymbolKind.External);
            _vars[variable] = symbol;
        }
        return symbol;
    }

    /// <summary>
    /// Converts a constraint into a tableau row (with basic variables substituted)
    /// plus the marker/error symbols that identify it later.
    /// </summary>
    private Row CreateRow(Constraint constraint, out Tag tag)
    {
        var expr = constraint.Expression;
        var row = new Row(expr.Constant);

        foreach (var term in expr.Terms)
        {
            if (Row.NearZero(term.Coefficient)) continue;
            var symbol = GetVarSymbol(term.Variable);
            if (_rows.TryGetValue(symbol, out var basicRow)) row.Insert(basicRow, term.Coefficient);
            else row.Insert(symbol, term.Coefficient);
        }

        tag = new Tag();
        double strength = constraint.Strength;
        switch (constraint.Operator)
        {
            case RelationalOperator.LessOrEqual:
            case RelationalOperator.GreaterOrEqual:
            {
                // expr <= 0  ==>  expr + slack = 0, slack >= 0  (and mirrored for >=)
                double coeff = constraint.Operator == RelationalOperator.LessOrEqual ? 1.0 : -1.0;
                var slack = NewSymbol(SymbolKind.Slack);
                tag.Marker = slack;
                row.Insert(slack, coeff);
                if (!constraint.IsRequired)
                {
                    var error = NewSymbol(SymbolKind.Error);
                    tag.Other = error;
                    row.Insert(error, -coeff);
                    _objective.Insert(error, strength);
                }
                break;
            }
            case RelationalOperator.Equal:
            {
                if (!constraint.IsRequired)
                {
                    // expr = e+ - e-, both >= 0, both penalised.
                    var errPlus = NewSymbol(SymbolKind.Error);
                    var errMinus = NewSymbol(SymbolKind.Error);
                    tag.Marker = errPlus;
                    tag.Other = errMinus;
                    row.Insert(errPlus, -1.0);
                    row.Insert(errMinus, 1.0);
                    _objective.Insert(errPlus, strength);
                    _objective.Insert(errMinus, strength);
                }
                else
                {
                    // A dummy can never enter the basis; it only marks the row.
                    var dummy = NewSymbol(SymbolKind.Dummy);
                    tag.Marker = dummy;
                    row.Insert(dummy);
                }
                break;
            }
        }

        // The tableau invariant requires non-negative constants for restricted rows.
        if (row.Constant < 0.0) row.ReverseSign();
        return row;
    }

    /// <summary>
    /// Picks the symbol to make basic for a new row: any external symbol, else a
    /// slack/error marker with negative coefficient (so the row stays feasible).
    /// </summary>
    private static Symbol ChooseSubject(Row row, Tag tag)
    {
        Symbol best = Symbol.Invalid;
        foreach (var symbol in row.Cells.Keys)
            if (symbol.IsExternal && (!best.IsValid || symbol.Id < best.Id))
                best = symbol;
        if (best.IsValid) return best;

        if (tag.Marker.IsPivotable && row.CoefficientFor(tag.Marker) < 0.0) return tag.Marker;
        if (tag.Other.IsPivotable && row.CoefficientFor(tag.Other) < 0.0) return tag.Other;
        return Symbol.Invalid;
    }

    private static bool AllDummies(Row row) => row.Cells.Keys.All(s => s.IsDummy);

    /// <summary>
    /// Phase one: adds the row with an artificial basic variable and minimises it.
    /// The constraint is satisfiable iff the artificial variable can be driven to zero.
    /// </summary>
    private bool AddWithArtificialVariable(Row row)
    {
        var art = NewSymbol(SymbolKind.Slack);
        _rows[art] = new Row(row);
        _artificial = new Row(row);

        Optimize(_artificial);
        bool success = Row.NearZero(_artificial.Constant);
        _artificial = null;

        // If the artificial variable is still basic, pivot it out (or drop an empty row).
        if (_rows.Remove(art, out var artRow))
        {
            if (artRow.Cells.Count == 0) return success;
            var entering = AnyPivotableSymbol(artRow);
            if (!entering.IsValid) return false;
            artRow.SolveFor(art, entering);
            Substitute(entering, artRow);
            _rows[entering] = artRow;
        }

        // The artificial variable is now parametric; remove its column everywhere.
        foreach (var r in _rows.Values) r.Remove(art);
        _objective.Remove(art);
        return success;
    }

    private static Symbol AnyPivotableSymbol(Row row)
    {
        Symbol best = Symbol.Invalid;
        foreach (var symbol in row.Cells.Keys)
            if (symbol.IsPivotable && (!best.IsValid || symbol.Id < best.Id))
                best = symbol;
        return best;
    }

    /// <summary>Substitutes a symbol's new row into every row and the objective(s).</summary>
    private void Substitute(Symbol symbol, Row row)
    {
        foreach (var (basic, r) in _rows)
        {
            r.Substitute(symbol, row);
            if (!basic.IsExternal && r.Constant < 0.0)
                _infeasibleRows.Add(basic);
        }
        _objective.Substitute(symbol, row);
        _artificial?.Substitute(symbol, row);
    }

    // ------------------------------------------------------------------
    // Primal simplex
    // ------------------------------------------------------------------

    /// <summary>Primal simplex: pivot until no objective coefficient is negative.</summary>
    private void Optimize(Row objective)
    {
        while (true)
        {
            var entering = GetEnteringSymbol(objective);
            if (!entering.IsValid) return;

            var (leaving, row) = GetLeavingRow(entering);
            if (row is null)
                throw new InternalSolverException("the objective is unbounded");

            _rows.Remove(leaving);
            row.SolveFor(leaving, entering);
            Substitute(entering, row);
            _rows[entering] = row;
        }
    }

    /// <summary>Lowest-id non-dummy symbol with a negative objective coefficient (Bland-style tie break).</summary>
    private static Symbol GetEnteringSymbol(Row objective)
    {
        Symbol best = Symbol.Invalid;
        foreach (var (symbol, coeff) in objective.Cells)
            if (!symbol.IsDummy && coeff < 0.0 && (!best.IsValid || symbol.Id < best.Id))
                best = symbol;
        return best;
    }

    /// <summary>Minimum-ratio test over restricted rows in which <paramref name="entering"/> appears with a negative coefficient.</summary>
    private (Symbol, Row?) GetLeavingRow(Symbol entering)
    {
        double ratio = double.MaxValue;
        Symbol found = Symbol.Invalid;
        Row? foundRow = null;
        foreach (var (symbol, row) in _rows)
        {
            if (symbol.IsExternal) continue;
            double coeff = row.CoefficientFor(entering);
            if (coeff >= 0.0) continue;
            double r = -row.Constant / coeff;
            if (r < ratio || (r == ratio && symbol.Id < found.Id))
            {
                ratio = r;
                found = symbol;
                foundRow = row;
            }
        }
        return (found, foundRow);
    }

    /// <summary>
    /// Chooses which row to pivot on when removing a non-basic marker:
    /// prefer restricted rows with a negative coefficient (smallest ratio), then
    /// restricted rows with a positive coefficient, then any unrestricted row.
    /// </summary>
    private (Symbol, Row?) GetMarkerLeavingRow(Symbol marker)
    {
        double r1 = double.MaxValue, r2 = double.MaxValue;
        Symbol first = Symbol.Invalid, second = Symbol.Invalid, third = Symbol.Invalid;
        foreach (var (symbol, row) in _rows)
        {
            double c = row.CoefficientFor(marker);
            if (c == 0.0) continue;
            if (symbol.IsExternal)
            {
                if (!third.IsValid || symbol.Id < third.Id) third = symbol;
            }
            else if (c < 0.0)
            {
                double r = -row.Constant / c;
                if (r < r1 || (r == r1 && symbol.Id < first.Id)) { r1 = r; first = symbol; }
            }
            else
            {
                double r = row.Constant / c;
                if (r < r2 || (r == r2 && symbol.Id < second.Id)) { r2 = r; second = symbol; }
            }
        }
        var pick = first.IsValid ? first : second.IsValid ? second : third;
        return pick.IsValid ? (pick, _rows[pick]) : (Symbol.Invalid, null);
    }

    private void RemoveConstraintEffects(Constraint constraint, Tag tag)
    {
        if (tag.Marker.Kind == SymbolKind.Error) RemoveMarkerEffects(tag.Marker, constraint.Strength);
        if (tag.Other.Kind == SymbolKind.Error) RemoveMarkerEffects(tag.Other, constraint.Strength);
    }

    private void RemoveMarkerEffects(Symbol marker, double strength)
    {
        if (_rows.TryGetValue(marker, out var row)) _objective.Insert(row, -strength);
        else _objective.Insert(marker, -strength);
    }
}
