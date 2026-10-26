# cassowary-cs

An incremental **Cassowary** linear-constraint solver for .NET 8 — the algorithm
behind Apple's Auto Layout, GSS and Kiwi — written from scratch in C# with no
runtime dependencies.

Describe a layout as linear equalities and inequalities, give each one a
strength, and the solver finds the assignment that satisfies every *required*
constraint while violating the weaker ones as little as their priorities allow.
Constraints can be added and removed one at a time, and "edit variables" (for
example a window width that is being dragged) are re-solved with a handful of
dual-simplex pivots instead of from scratch.

```csharp
using Cassowary;

var left = new Variable("left");
var mid = new Variable("mid");
var right = new Variable("right");

var solver = new Solver();
solver.AddConstraint((left + right).Eq(2 * mid));       // mid is the midpoint
solver.AddConstraint(right >= left + 10);               // at least 10 wide
solver.AddConstraint(left >= 0);
solver.AddConstraint(right <= 100);
solver.AddConstraint(left.Eq(20) | Strength.Weak);      // preferences
solver.AddConstraint(right.Eq(60) | Strength.Weak);

solver.AddEditVariable(mid, Strength.Strong);
solver.SuggestValue(mid, 90);                           // "drag" the midpoint
solver.UpdateVariables();
// left = 80, mid = 90, right = 100
```

## Features

- **Incremental simplex tableau** (Badros, Borning & Stuckey, *The Cassowary
  Linear Arithmetic Constraint Solving Algorithm*, 2001), structured like Kiwi:
  slack symbols for inequalities, dummy markers for required equalities and
  error symbols for soft constraints, with a primal simplex for additions and
  removals and a phase-one artificial variable when a new row has no obvious
  basic variable.
- **Strengths**: `Required`, `Strong`, `Medium`, `Weak`, plus
  `Strength.Create(strong, medium, weak, weight)` for weighted tiers. Tiers are
  lexicographic: no number of weak violations outweighs one medium violation
  (up to 1000×).
- **Edit variables**: `AddEditVariable` / `SuggestValue` / `RemoveEditVariable`.
  Suggesting a value only shifts row constants, then the **dual simplex**
  restores feasibility while keeping the basis optimal.
- **Errors you can act on**: `UnsatisfiableConstraintException` (conflicting
  required constraints; the solver remains usable), `DuplicateConstraintException`,
  `UnknownConstraintException`, `DuplicateEditVariableException`,
  `UnknownEditVariableException`, `BadRequiredStrengthException`.
- **Operator API**: `x + 2 * y - 5`, `a <= b`, `x.Eq(y + 10)`,
  `constraint | Strength.Weak`.
- **Constraint DSL** with line/column error messages:

  ```text
  # comments, ';' or newlines between constraints
  sidebar.right + 8 == content.left
  content.width >= 2 * sidebar.width   @ strong
  0 <= x <= 100                        # chains expand to two constraints
  title.width == 120                   @ weak * 3
  ```

  Non-linear input (`x * y`, `1 / x`) is rejected with a precise position.
- **Layout demo CLI** that turns the DSL into boxes and re-solves for several
  window sizes, with ASCII rendering.
- **Randomized tests** that build systems around a hidden feasible assignment
  and check that every required constraint holds, that the solver's objective
  equals the weighted violation of the soft constraints, and that incremental
  add/remove and `SuggestValue` sequences reach the same optimum as solving
  from scratch.

## Layout demo

```text
$ dotnet run --project examples/LayoutDemo -- examples/layouts/holy-grail.layout --size 1000x420 --ascii
== window 1000 x 420 ==
box              left      top    width   height
header             10       10      980       60
footer             10      370      980       40
nav                10       80      200      280
aside             790       80      200      280
main              220       80      560      280
+======================================================================+
| +-header-----------------------------------------------------------+ |
| |                                                                  | |
| +------------------------------------------------------------------+ |
| +-nav--------++-main---------------------------------++-aside------+ |
| |            ||                                      ||            | |
| |            ||                                      ||            | |
| |            ||                                      ||            | |
| |            ||                                      ||            | |
| +------------++--------------------------------------++------------+ |
| +-footer-----------------------------------------------------------+ |
| +------------------------------------------------------------------+ |
+======================================================================+
```

Layout files use `box.attribute` names (`left right top bottom width height
centerX centerY`). Every box gets the intrinsic relations (`right == left +
width`, …, non-negative size) and is kept inside `window`, whose size is
driven by strong edit variables. Pass several `--size WxH` flags to watch the
same solver re-solve incrementally.

## Parsing from code

```csharp
using Cassowary.Parsing;

var scope = new VariableScope();
var solver = new Solver();
solver.AddConstraints(ConstraintParser.ParseAll("""
    a.top == 0; a.height == 40
    b.top == a.top + a.height + 8
    b.height == a.height * 2   @ medium
    """, scope));
solver.UpdateVariables();
Console.WriteLine(scope["b.top"].Value);   // 48
```

## How it works

Every constraint is rewritten as `expr OP 0` and turned into a tableau row
with all current basic variables substituted out:

| constraint            | row                               | objective        |
|-----------------------|-----------------------------------|------------------|
| `e <= 0` required     | `e + s = 0`, `s >= 0`             | –                |
| `e <= 0` strength w   | `e + s - err = 0`                 | `+ w·err`        |
| `e == 0` required     | `e + d = 0` (dummy marker)        | –                |
| `e == 0` strength w   | `e - err⁺ + err⁻ = 0`             | `+ w·err⁺ + w·err⁻` |

The new row's subject is an external variable if it has one, otherwise a
slack/error marker with a negative coefficient; if neither exists an
artificial variable is minimised first (phase one), and a positive minimum
means the constraint is unsatisfiable. The primal simplex then minimises the
objective — the weighted sum of soft violations. Entering and leaving symbols
are chosen with lowest-id tie breaking so results are deterministic.

Removing a constraint subtracts its error terms from the objective and pivots
its marker out of the basis. `SuggestValue` shifts row constants by the
change in the edit target; rows that become negative are queued and fixed by
the dual simplex.

## Building and testing

```sh
dotnet build
dotnet test
dotnet run --project examples/LayoutDemo -- examples/layouts/dialog.layout --size 900x400 --size 400x300 --ascii
```

Requires the .NET 8 SDK. The library has no dependencies; tests use xUnit.

## Layout

```
src/Cassowary/            solver, expressions, strengths, errors
src/Cassowary/Parsing/    lexer, recursive-descent constraint parser, VariableScope
tests/Cassowary.Tests/    unit, parser and randomized property tests
examples/LayoutDemo/      layout CLI with ASCII rendering
examples/layouts/         sample layout files
```

## License

MIT © Aryan Vohra
