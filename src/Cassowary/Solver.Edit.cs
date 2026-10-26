using System.Collections.Generic;

namespace Cassowary;

public sealed partial class Solver
{
    private sealed class EditInfo
    {
        public EditInfo(Tag tag, Constraint constraint)
        {
            Tag = tag;
            Constraint = constraint;
        }

        public Tag Tag { get; }
        public Constraint Constraint { get; }
        public double Constant { get; set; }
    }

    private readonly Dictionary<Variable, EditInfo> _edits = new();

    /// <summary>
    /// Registers <paramref name="variable"/> as an edit variable: a non-required
    /// <c>variable == value</c> constraint whose value can later be changed cheaply
    /// with <see cref="SuggestValue"/>.
    /// </summary>
    /// <exception cref="DuplicateEditVariableException">Already an edit variable.</exception>
    /// <exception cref="BadRequiredStrengthException">Edit strength may not be required.</exception>
    public void AddEditVariable(Variable variable, double strength)
    {
        if (_edits.ContainsKey(variable))
            throw new DuplicateEditVariableException(variable);
        strength = Strength.Clip(strength);
        if (strength >= Strength.Required)
            throw new BadRequiredStrengthException();

        var constraint = new Constraint(new Expression(new Term(variable)), RelationalOperator.Equal, strength);
        AddConstraint(constraint);
        _edits[variable] = new EditInfo(_constraints[constraint], constraint);
    }

    /// <summary>Unregisters an edit variable and removes its edit constraint.</summary>
    /// <exception cref="UnknownEditVariableException">Not an edit variable.</exception>
    public void RemoveEditVariable(Variable variable)
    {
        if (!_edits.Remove(variable, out var info))
            throw new UnknownEditVariableException(variable);
        RemoveConstraint(info.Constraint);
    }

    /// <summary>True if <paramref name="variable"/> is an edit variable.</summary>
    public bool HasEditVariable(Variable variable) => _edits.ContainsKey(variable);

    /// <summary>
    /// Suggests a new value for an edit variable. Only the constants of the
    /// tableau change, so the current basis stays optimal but may become
    /// infeasible; the dual simplex then restores feasibility in a few pivots.
    /// </summary>
    /// <exception cref="UnknownEditVariableException">Not an edit variable.</exception>
    public void SuggestValue(Variable variable, double value)
    {
        if (!_edits.TryGetValue(variable, out var info))
            throw new UnknownEditVariableException(variable);

        double delta = value - info.Constant;
        info.Constant = value;

        // If the positive error variable is basic, only its row changes.
        if (_rows.TryGetValue(info.Tag.Marker, out var row))
        {
            if (row.Add(-delta) < 0.0) _infeasibleRows.Add(info.Tag.Marker);
            DualOptimize();
            return;
        }

        // Likewise for the negative error variable.
        if (_rows.TryGetValue(info.Tag.Other, out row))
        {
            if (row.Add(delta) < 0.0) _infeasibleRows.Add(info.Tag.Other);
            DualOptimize();
            return;
        }

        // Otherwise both are parametric: shift every row that mentions the marker.
        foreach (var (symbol, r) in _rows)
        {
            double coeff = r.CoefficientFor(info.Tag.Marker);
            if (coeff != 0.0 && r.Add(delta * coeff) < 0.0 && !symbol.IsExternal)
                _infeasibleRows.Add(symbol);
        }
        DualOptimize();
    }

    /// <summary>
    /// Dual simplex: while some restricted basic variable is negative, pivot it
    /// out using the entering symbol that keeps the objective optimal.
    /// </summary>
    private void DualOptimize()
    {
        while (_infeasibleRows.Count > 0)
        {
            var leaving = _infeasibleRows[^1];
            _infeasibleRows.RemoveAt(_infeasibleRows.Count - 1);

            if (!_rows.TryGetValue(leaving, out var row) || Row.NearZero(row.Constant) || row.Constant >= 0.0)
                continue;

            var entering = GetDualEnteringSymbol(row);
            if (!entering.IsValid)
                throw new InternalSolverException("dual optimize failed");

            _rows.Remove(leaving);
            row.SolveFor(leaving, entering);
            Substitute(entering, row);
            _rows[entering] = row;
        }
    }

    /// <summary>Dual ratio test: minimise objective coefficient / row coefficient over positive cells.</summary>
    private Symbol GetDualEnteringSymbol(Row row)
    {
        Symbol entering = Symbol.Invalid;
        double ratio = double.MaxValue;
        foreach (var (symbol, coeff) in row.Cells)
        {
            if (coeff <= 0.0 || symbol.IsDummy) continue;
            double r = _objective.CoefficientFor(symbol) / coeff;
            if (r < ratio || (r == ratio && symbol.Id < entering.Id))
            {
                ratio = r;
                entering = symbol;
            }
        }
        return entering;
    }

    private void ResetEdits() => _edits.Clear();
}
