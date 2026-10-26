namespace Cassowary.Tests;

public class ExpressionTests
{
    [Fact]
    public void OperatorsBuildLinearExpressions()
    {
        var x = new Variable("x");
        var y = new Variable("y");
        var e = (2 * x + y - 3 * x + 5).Reduce();
        Assert.Equal(2, e.Terms.Count);
        Assert.Equal(-1.0, e.Terms[0].Coefficient);
        Assert.Same(x, e.Terms[0].Variable);
        Assert.Equal(1.0, e.Terms[1].Coefficient);
        Assert.Equal(5.0, e.Constant);
        Assert.Equal("-x + y + 5", e.ToString());
    }

    [Fact]
    public void ReduceDropsCancelledTerms()
    {
        var x = new Variable("x");
        var e = (x - x + 1).Reduce();
        Assert.True(e.IsConstant);
        Assert.Equal("1", e.ToString());
    }

    [Fact]
    public void ScalingAndDivision()
    {
        var x = new Variable("x");
        var e = ((x + 4) * 3 / 2).Reduce();
        Assert.Equal(1.5, e.Terms[0].Coefficient);
        Assert.Equal(6.0, e.Constant);
        Assert.Equal("1.5*x + 6", e.ToString());
    }

    [Fact]
    public void ConstraintsMoveEverythingToTheLeft()
    {
        var x = new Variable("x");
        var y = new Variable("y");
        var c = (x + 10 <= y) | Strength.Strong;
        Assert.Equal(RelationalOperator.LessOrEqual, c.Operator);
        Assert.Equal(Strength.Strong, c.Strength);
        Assert.Equal("x - y + 10 <= 0 | strong", c.ToString());

        var eq = x.Eq(2 * y);
        Assert.True(eq.IsRequired);
        Assert.Equal("x - 2*y == 0 | required", eq.ToString());

        var ge = 5 <= x;
        Assert.Equal(RelationalOperator.GreaterOrEqual, ge.Operator);
    }

    [Fact]
    public void StrengthTiersAreLexicographic()
    {
        Assert.True(Strength.Strong > 999 * Strength.Medium);
        Assert.True(Strength.Medium > 999 * Strength.Weak);
        Assert.True(Strength.Required > Strength.Strong);
        Assert.Equal(Strength.Required, Strength.Clip(1e30));
        Assert.Equal(0.0, Strength.Clip(-5));
        Assert.Equal(Strength.Create(0, 2, 0), Strength.Create(0, 1, 0, 2));
        Assert.Equal("medium", Strength.Describe(Strength.Medium));
    }

    [Fact]
    public void ViolationMeasuresDistanceToSatisfaction()
    {
        var x = new Variable("x");
        x.Value = 3; // internal setter visible to tests
        Assert.Equal(0.0, (x <= 5).Violation);
        Assert.Equal(2.0, (x >= 5).Violation);
        Assert.Equal(1.0, x.Eq(4).Violation);
    }
}
