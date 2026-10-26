# cassowary-cs

An incremental **Cassowary** linear-constraint solver for .NET 8 — the
algorithm behind Apple's Auto Layout — written from scratch in C# with no
runtime dependencies.

> Work in progress: core simplex, edit variables, a constraint DSL and
> randomized property tests.

```csharp
using Cassowary;

var x = new Variable("x");
var solver = new Solver();
solver.AddConstraint(x >= 10);
solver.AddConstraint(x <= 20);
solver.AddConstraint(x.Eq(100) | Strength.Medium);
solver.UpdateVariables();   // x.Value == 20
```

## Edit variables

Interactive values (window size, a dragged handle) are edit variables:

```csharp
solver.AddEditVariable(width, Strength.Strong);
solver.SuggestValue(width, 640);   // re-optimised with the dual simplex
solver.UpdateVariables();
```

## Constraint DSL

```csharp
var scope = new VariableScope();
foreach (var c in ConstraintParser.ParseAll("""
    left >= 0
    left + width <= 800
    width == 2 * gutter + 300 @ strong
    0 <= gutter <= 40 @ medium * 2
    """, scope))
    solver.AddConstraint(c);
```

## Testing

Besides unit tests, `RandomizedTests` builds random constraint systems and
checks every required constraint holds, that solutions match a from-scratch
solve, and that rejected constraints leave the solver untouched.

## Building and testing

```sh
dotnet build
dotnet test
```

## License

MIT © Aryan Vohra
