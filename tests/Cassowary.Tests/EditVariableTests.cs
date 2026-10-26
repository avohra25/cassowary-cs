namespace Cassowary.Tests;

public class EditVariableTests
{
    private const double Tol = 1e-6;

    [Fact]
    public void SuggestValueMovesEditVariable()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddEditVariable(x, Strength.Strong);
        Assert.True(s.HasEditVariable(x));
        foreach (var v in new[] { 10.0, -5.0, 1e6, 0.0 })
        {
            s.SuggestValue(x, v);
            s.UpdateVariables();
            Assert.Equal(v, x.Value, Tol);
        }
    }

    [Fact]
    public void RequiredConstraintsClampSuggestions()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x >= 0);
        s.AddConstraint(x <= 100);
        s.AddEditVariable(x, Strength.Strong);

        s.SuggestValue(x, 50);
        s.UpdateVariables();
        Assert.Equal(50, x.Value, Tol);

        s.SuggestValue(x, 150);
        s.UpdateVariables();
        Assert.Equal(100, x.Value, Tol);

        s.SuggestValue(x, -20);
        s.UpdateVariables();
        Assert.Equal(0, x.Value, Tol);
    }

    [Fact]
    public void DraggingOneEndpointKeepsMidpointConsistent()
    {
        var left = new Variable("left");
        var mid = new Variable("mid");
        var right = new Variable("right");
        var s = new Solver();
        s.AddConstraint((left + right).Eq(2 * mid));
        s.AddConstraint(right >= left + 10);
        s.AddConstraint(left >= 0);
        s.AddConstraint(right <= 100);
        s.AddConstraint(left.Eq(20) | Strength.Weak);
        s.AddConstraint(right.Eq(60) | Strength.Weak);

        s.AddEditVariable(mid, Strength.Strong);
        foreach (var target in new[] { 40.0, 70.0, 98.0, 2.0, 50.0 })
        {
            s.SuggestValue(mid, target);
            s.UpdateVariables();
            Assert.Equal(left.Value + right.Value, 2 * mid.Value, Tol);
            Assert.True(right.Value >= left.Value + 10 - Tol);
            Assert.True(left.Value >= -Tol && right.Value <= 100 + Tol);
        }
        // mid can reach anything in [5, 95]; 50 is reachable exactly.
        Assert.Equal(50, mid.Value, Tol);
    }

    [Fact]
    public void EditStrengthCompetesWithOtherConstraints()
    {
        var x = new Variable("x");
        var s = new Solver();
        s.AddConstraint(x.Eq(10) | Strength.Strong);
        s.AddEditVariable(x, Strength.Medium);
        s.SuggestValue(x, 90);
        s.UpdateVariables();
        Assert.Equal(10, x.Value, Tol); // strong beats the medium edit

        s.RemoveEditVariable(x);
        s.AddEditVariable(x, Strength.Create(2, 0, 0));
        s.SuggestValue(x, 90);
        s.UpdateVariables();
        Assert.Equal(90, x.Value, Tol); // a 2x-strong edit wins
    }

    [Fact]
    public void EditVariableErrors()
    {
        var x = new Variable("x");
        var s = new Solver();
        Assert.Throws<BadRequiredStrengthException>(() => s.AddEditVariable(x, Strength.Required));
        Assert.Throws<UnknownEditVariableException>(() => s.SuggestValue(x, 1));
        Assert.Throws<UnknownEditVariableException>(() => s.RemoveEditVariable(x));
        s.AddEditVariable(x, Strength.Weak);
        Assert.Throws<DuplicateEditVariableException>(() => s.AddEditVariable(x, Strength.Strong));
        s.RemoveEditVariable(x);
        Assert.False(s.HasEditVariable(x));
        Assert.Equal(0, s.ConstraintCount);
    }

    [Fact]
    public void MultipleEditVariablesInteract()
    {
        // A box whose width must stay within [10, 200], dragged at both edges.
        var left = new Variable("left");
        var right = new Variable("right");
        var s = new Solver();
        s.AddConstraint(right - left >= 10);
        s.AddConstraint(right - left <= 200);
        s.AddEditVariable(left, Strength.Strong);
        s.AddEditVariable(right, Strength.Medium);

        s.SuggestValue(left, 0);
        s.SuggestValue(right, 100);
        s.UpdateVariables();
        Assert.Equal(0, left.Value, Tol);
        Assert.Equal(100, right.Value, Tol);

        // Pull right past the max width: left (stronger) stays put.
        s.SuggestValue(right, 500);
        s.UpdateVariables();
        Assert.Equal(0, left.Value, Tol);
        Assert.Equal(200, right.Value, Tol);

        // Push left past right: right is shoved along.
        s.SuggestValue(left, 300);
        s.UpdateVariables();
        Assert.Equal(300, left.Value, Tol);
        Assert.Equal(500, right.Value, Tol);

        s.SuggestValue(right, 305);
        s.UpdateVariables();
        Assert.Equal(310, right.Value, Tol);
    }
}
