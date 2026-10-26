namespace Cassowary.Tests;

/// <summary>
/// Property tests over seeded random systems. Each system is built around a
/// hidden assignment so that the required part is always satisfiable; the
/// solver's answer is then checked against the constraints themselves, and
/// incremental results are compared with solving from scratch.
/// </summary>
public class RandomizedTests
{
    private const double Tol = 1e-5;

    private static readonly double[] s_strengths =
        { Strength.Weak, Strength.Medium, Strength.Strong, Strength.Create(0, 3, 0), Strength.Create(0, 0, 7) };

    private sealed class Model
    {
        public readonly Random Rng;
        public readonly Variable[] Vars;
        public readonly double[] Hidden;

        public Model(int seed, int n)
        {
            Rng = new Random(seed);
            Vars = Enumerable.Range(0, n).Select(i => new Variable($"x{i}")).ToArray();
            Hidden = Vars.Select(_ => (double)Rng.Next(-50, 51)).ToArray();
        }

        private Expression RandomExpr(out double hiddenValue)
        {
            int k = Rng.Next(1, Math.Min(4, Vars.Length) + 1);
            var e = new Expression();
            hiddenValue = 0;
            foreach (int idx in Enumerable.Range(0, Vars.Length).OrderBy(_ => Rng.Next()).Take(k))
            {
                double coeff = Rng.Next(1, 6) * (Rng.Next(2) == 0 ? -1 : 1);
                e += Vars[idx] * coeff;
                hiddenValue += coeff * Hidden[idx];
            }
            return e;
        }

        /// <summary>A required constraint that the hidden assignment satisfies.</summary>
        public Constraint FeasibleRequired()
        {
            var e = RandomExpr(out double h);
            return Rng.Next(4) switch
            {
                0 => e.Eq(h),
                1 => e <= h + Rng.Next(0, 20),
                _ => e >= h - Rng.Next(0, 20),
            };
        }

        /// <summary>An arbitrary (possibly conflicting) soft constraint.</summary>
        public Constraint RandomSoft()
        {
            var e = RandomExpr(out _);
            double target = Rng.Next(-200, 201);
            double s = s_strengths[Rng.Next(s_strengths.Length)];
            return (Rng.Next(3) switch
            {
                0 => e.Eq(target),
                1 => e <= target,
                _ => e >= target,
            }) | s;
        }
    }

    private static void AssertRequiredHold(IEnumerable<Constraint> constraints, string context)
    {
        foreach (var c in constraints.Where(c => c.IsRequired))
            Assert.True(c.Violation < Tol, $"{context}: violated {c} by {c.Violation}");
    }

    /// <summary>Weighted violation of soft constraints: exactly what Cassowary minimises.</summary>
    private static double Penalty(IEnumerable<Constraint> constraints) =>
        constraints.Where(c => !c.IsRequired).Sum(c => c.Strength * c.Violation);

    private static double FreshPenalty(IReadOnlyList<Constraint> constraints)
    {
        var s = new Solver();
        foreach (var c in constraints) s.AddConstraint(new Constraint(c, c.Strength));
        s.UpdateVariables();
        AssertRequiredHold(constraints, "fresh");
        return Penalty(constraints);
    }

    private static void AssertClose(double expected, double actual, string context)
    {
        // Absolute floor: strengths reach 1e6 and the tableau rounds at ~1e-8.
        double tol = 1e-3 + 1e-7 * Math.Abs(expected);
        Assert.True(Math.Abs(expected - actual) <= tol, $"{context}: expected {expected}, got {actual}");
    }

    [Theory]
    [MemberData(nameof(Seeds), 60)]
    public void FeasibleRequiredSystemsAreSatisfied(int seed)
    {
        var m = new Model(seed, 3 + seed % 6);
        var s = new Solver();
        var cs = new List<Constraint>();
        for (int i = 0; i < 4 + seed % 10; i++)
        {
            var c = m.FeasibleRequired();
            s.AddConstraint(c);
            cs.Add(c);
        }
        for (int i = 0; i < 6; i++)
        {
            var c = m.RandomSoft();
            s.AddConstraint(c);
            cs.Add(c);
        }
        s.UpdateVariables();
        AssertRequiredHold(cs, $"seed {seed}");
        AssertClose(Penalty(cs), s.ObjectiveValue, $"seed {seed} objective");
    }

    [Theory]
    [MemberData(nameof(Seeds), 40)]
    public void IncrementalAddRemoveMatchesSolvingFromScratch(int seed)
    {
        var m = new Model(1000 + seed, 4 + seed % 4);
        var s = new Solver();
        var live = new List<Constraint>();
        for (int step = 0; step < 40; step++)
        {
            int action = m.Rng.Next(10);
            if (action < 3 && live.Count > 0)
            {
                var victim = live[m.Rng.Next(live.Count)];
                s.RemoveConstraint(victim);
                live.Remove(victim);
            }
            else
            {
                var c = action < 6 ? m.FeasibleRequired() : m.RandomSoft();
                s.AddConstraint(c);
                live.Add(c);
            }

            s.UpdateVariables();
            string ctx = $"seed {seed} step {step}";
            AssertRequiredHold(live, ctx);
            double incremental = Penalty(live);
            AssertClose(incremental, s.ObjectiveValue, ctx + " objective");
            if (step % 5 == 4)
                AssertClose(FreshPenalty(live), incremental, ctx + " vs fresh");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds), 30)]
    public void InfeasibleRequiredConstraintsAreRejectedCleanly(int seed)
    {
        var m = new Model(5000 + seed, 4);
        var s = new Solver();
        var live = new List<Constraint>();
        for (int i = 0; i < 6; i++)
        {
            var c = m.FeasibleRequired();
            s.AddConstraint(c);
            live.Add(c);
        }

        // Contradict an existing equality/inequality by shifting its bound past what's allowed.
        var victim = live[m.Rng.Next(live.Count)];
        var contradiction = victim.Operator switch
        {
            RelationalOperator.LessOrEqual => victim.Expression.Ge(1 + m.Rng.Next(1, 50)),
            RelationalOperator.GreaterOrEqual => victim.Expression.Le(-1 - m.Rng.Next(1, 50)),
            _ => victim.Expression.Eq(m.Rng.Next(1, 50)),
        };
        Assert.Throws<UnsatisfiableConstraintException>(() => s.AddConstraint(contradiction));
        Assert.False(s.HasConstraint(contradiction));

        // The solver keeps working and still satisfies everything it accepted.
        s.UpdateVariables();
        AssertRequiredHold(live, $"seed {seed} after rejection");
        var soft = m.RandomSoft();
        s.AddConstraint(soft);
        live.Add(soft);
        s.UpdateVariables();
        AssertRequiredHold(live, $"seed {seed} after soft add");
    }

    [Theory]
    [MemberData(nameof(Seeds), 30)]
    public void SuggestValueMatchesEquivalentFreshSystem(int seed)
    {
        var m = new Model(9000 + seed, 5);
        var s = new Solver();
        var live = new List<Constraint>();
        for (int i = 0; i < 5; i++)
        {
            var c = m.FeasibleRequired();
            s.AddConstraint(c);
            live.Add(c);
        }
        for (int i = 0; i < 3; i++)
        {
            var c = m.RandomSoft();
            s.AddConstraint(c);
            live.Add(c);
        }

        var edited = m.Vars.Take(2).ToArray();
        double[] editStrength = { Strength.Strong, Strength.Medium };
        for (int i = 0; i < edited.Length; i++) s.AddEditVariable(edited[i], editStrength[i]);

        var suggested = new double[edited.Length];
        for (int round = 0; round < 15; round++)
        {
            int which = m.Rng.Next(edited.Length);
            suggested[which] = m.Rng.Next(-300, 301);
            s.SuggestValue(edited[which], suggested[which]);
            s.UpdateVariables();

            // Same problem with the edits written as plain soft equalities.
            var withEdits = live
                .Concat(edited.Select((v, i) => v.Eq(suggested[i]) | editStrength[i]))
                .ToList();
            string ctx = $"seed {seed} round {round}";
            AssertRequiredHold(live, ctx);
            double incremental = Penalty(withEdits);
            AssertClose(incremental, s.ObjectiveValue, ctx + " objective");
            AssertClose(FreshPenalty(withEdits), incremental, ctx + " vs fresh");
        }
    }

    public static IEnumerable<object[]> Seeds(int count) =>
        Enumerable.Range(1, count).Select(i => new object[] { i });
}
