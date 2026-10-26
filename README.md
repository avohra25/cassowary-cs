# cassowary-cs

An incremental **Cassowary** linear-constraint solver for .NET 8 — the
algorithm behind Apple's Auto Layout — written from scratch in C# with no
runtime dependencies.

> Work in progress: this first cut contains the expression model, strengths
> and the core incremental simplex (add/remove constraints).

```csharp
using Cassowary;

var x = new Variable("x");
var solver = new Solver();
solver.AddConstraint(x >= 10);
solver.AddConstraint(x <= 20);
solver.AddConstraint(x.Eq(100) | Strength.Medium);
solver.UpdateVariables();   // x.Value == 20
```

## Building and testing

```sh
dotnet build
dotnet test
```

## License

MIT © Aryan Vohra
