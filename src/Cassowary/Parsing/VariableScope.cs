using System;
using System.Collections.Generic;

namespace Cassowary.Parsing;

/// <summary>
/// Maps names used in constraint text to <see cref="Variable"/> instances.
/// By default unknown names create new variables on first use.
/// </summary>
public sealed class VariableScope
{
    private readonly Dictionary<string, Variable> _vars = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();

    /// <summary>Creates a scope.</summary>
    /// <param name="allowNew">If false, referencing an undeclared name is a parse error.</param>
    public VariableScope(bool allowNew = true) => AllowNew = allowNew;

    /// <summary>Whether unknown names create variables automatically.</summary>
    public bool AllowNew { get; set; }

    /// <summary>Names in order of first appearance.</summary>
    public IReadOnlyList<string> Names => _order;

    /// <summary>Number of variables in the scope.</summary>
    public int Count => _order.Count;

    /// <summary>Gets a variable by name, creating it if necessary.</summary>
    public Variable this[string name] => GetOrAdd(name);

    /// <summary>Declares (or fetches) a variable regardless of <see cref="AllowNew"/>.</summary>
    public Variable GetOrAdd(string name)
    {
        if (!_vars.TryGetValue(name, out var v))
        {
            v = new Variable(name);
            _vars[name] = v;
            _order.Add(name);
        }
        return v;
    }

    /// <summary>Adds an existing variable under a name.</summary>
    public void Add(string name, Variable variable)
    {
        if (!_vars.TryAdd(name, variable))
            throw new ArgumentException($"variable '{name}' already declared", nameof(name));
        _order.Add(name);
    }

    /// <summary>Looks a variable up without creating it.</summary>
    public bool TryGet(string name, out Variable variable) => _vars.TryGetValue(name, out variable!);

    internal Variable? Resolve(string name)
    {
        if (_vars.TryGetValue(name, out var v)) return v;
        return AllowNew ? GetOrAdd(name) : null;
    }
}
