namespace Cassowary.Tests;

public class SolverTests
{
    private const double Tol = 1e-6;

    [Fact]
    public void SolvesSimpleRequiredSystem()
    {
        var x = new Variable("x");
        var y = new Variable("y");
        var s = new Solver();
        s.AddConstraint(x.Eq(20));
        s.AddConstraint((x + 2).Eq(y + 10));
        s.UpdateVariables();
        Assert.Equal(20, x.Value, Tol);
        Assert.Equal(12, y.Value, Tol);
    }

    [Fact]
    public void InequalitiesRespectBounds()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x >= 10);
        s.AddConstraint(x <= 20);
        s.AddConstraint(x.Eq(0) | Strength.Weak);
        s.UpdateVariables();
        Assert.Equal(10, x.Value, Tol);

        var pull = x.Eq(100) | Strength.Medium;
        s.AddConstraint(pull);
        s.UpdateVariables();
        Assert.Equal(20, x.Value, Tol);

        s.RemoveConstraint(pull);
        s.UpdateVariables();
        Assert.Equal(10, x.Value, Tol);
    }

    [Fact]
    public void StrongerConstraintWins()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x.Eq(10) | Strength.Weak);
        s.AddConstraint(x.Eq(30) | Strength.Strong);
        s.AddConstraint(x.Eq(20) | Strength.Medium);
        s.UpdateVariables();
        Assert.Equal(30, x.Value, Tol);
    }

    [Fact]
    public void ManyWeaksDoNotOutvoteOneMedium()
    {
        var x = new Variable("x");
        var s = new Solver();
        for (int i = 0; i < 50; i++) s.AddConstraint(x.Eq(0) | Strength.Weak);
        s.AddConstraint(x.Eq(7) | Strength.Medium);
        s.UpdateVariables();
        Assert.Equal(7, x.Value, Tol);
    }

    [Fact]
    public void WeightedStrengthsTradeOff()
    {
        // Two equally strong opinions about x: the heavier one wins.
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x.Eq(0) | Strength.Create(0, 1, 0));
        s.AddConstraint(x.Eq(10) | Strength.Create(0, 2, 0));
        s.UpdateVariables();
        Assert.Equal(10, x.Value, Tol);
    }

    [Fact]
    public void UnsatisfiableRequiredConstraintThrows()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x >= 10);
        var bad = x <= 5;
        var ex = Assert.Throws<UnsatisfiableConstraintException>(() => s.AddConstraint(bad));
        Assert.Same(bad, ex.Constraint);
        Assert.False(s.HasConstraint(bad));
    }

    [Fact]
    public void ContradictoryEqualitiesThrow()
    {
        var x = new Variable("x");
        var y = new Variable("y");
        var s = new Solver();
        s.AddConstraint(x.Eq(y + 1));
        s.AddConstraint(y.Eq(3));
        Assert.Throws<UnsatisfiableConstraintException>(() => s.AddConstraint(x.Eq(5)));
        // A redundant but consistent equality is fine.
        s.AddConstraint(x.Eq(4));
        s.UpdateVariables();
        Assert.Equal(4, x.Value, Tol);
    }

    [Fact]
    public void DuplicateAndUnknownConstraints()
    {
        var x = new Variable("x");
        var s = new Solver();
        var c = x >= 1;
        s.AddConstraint(c);
        Assert.Throws<DuplicateConstraintException>(() => s.AddConstraint(c));
        // A structurally identical but distinct instance is accepted.
        s.AddConstraint(x >= 1);
        Assert.Equal(2, s.ConstraintCount);

        s.RemoveConstraint(c);
        Assert.Throws<UnknownConstraintException>(() => s.RemoveConstraint(c));
    }

    [Fact]
    public void RemovingRequiredConstraintsRestoresFreedom()
    {
        var left = new Variable("left");
        var width = new Variable("width");
        var right = new Variable("right");
        var s = new Solver();
        s.AddConstraint(right.Eq(left + width));
        s.AddConstraint(left >= 0);
        s.AddConstraint(width >= 0);
        s.AddConstraint(width.Eq(100) | Strength.Weak);
        var pin = right.Eq(50);
        s.AddConstraint(pin);
        s.UpdateVariables();
        Assert.Equal(50, right.Value, Tol);
        Assert.Equal(50, left.Value + width.Value, Tol);
        Assert.True(width.Value <= 50 + Tol);

        s.RemoveConstraint(pin);
        s.UpdateVariables();
        Assert.Equal(100, width.Value, Tol);
        Assert.Equal(left.Value + width.Value, right.Value, Tol);
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x.Eq(3));
        s.Reset();
        Assert.Equal(0, s.ConstraintCount);
        s.AddConstraint(x.Eq(4));
        s.UpdateVariables();
        Assert.Equal(4, x.Value, Tol);
    }

    [Fact]
    public void ClassicCenteringExample()
    {
        // left + right == 2 * mid, right >= left + 10, 0 <= left, right <= 100
        var left = new Variable("left");
        var mid = new Variable("mid");
        var right = new Variable("right");
        var s = new Solver();
        s.AddConstraint((left + right).Eq(2 * mid));
        s.AddConstraint(right >= left + 10);
        s.AddConstraint(left >= 0);
        s.AddConstraint(right <= 100);
        s.AddConstraint(mid.Eq(70) | Strength.Strong);
        s.AddConstraint(left.Eq(80) | Strength.Weak);
        s.UpdateVariables();
        Assert.Equal(70, mid.Value, Tol);
        Assert.Equal(2 * mid.Value, left.Value + right.Value, Tol);
        Assert.True(right.Value >= left.Value + 10 - Tol);
        Assert.True(right.Value <= 100 + Tol);
        Assert.Equal(65, left.Value, Tol); // as close to 80 as right >= left + 10 allows
    }

    [Fact]
    public void DumpDescribesTableau()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x >= 1);
        string dump = s.Dump();
        Assert.Contains("Tableau", dump);
        Assert.Contains("x >= 0", dump.Replace("x - 1 >= 0", "x >= 0"));
    }
}
