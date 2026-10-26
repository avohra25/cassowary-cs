using Cassowary.Parsing;

namespace Cassowary.Tests;

public class ParserTests
{
    private const double Tol = 1e-6;

    [Fact]
    public void ParsesSimpleConstraint()
    {
        var scope = new VariableScope();
        var c = ConstraintParser.Parse("box.left + 10 == box.right - 2*pad", scope);
        Assert.Equal(RelationalOperator.Equal, c.Operator);
        Assert.True(c.IsRequired);
        Assert.Equal(new[] { "box.left", "box.right", "pad" }, scope.Names);
        Assert.Equal("box.left - box.right + 2*pad + 10 == 0 | required", c.ToString());
    }

    [Theory]
    [InlineData("x >= 1 @ strong", 1_000_000.0)]
    [InlineData("x >= 1 @ medium", 1_000.0)]
    [InlineData("x >= 1 @weak*3", 3.0)]
    [InlineData("x >= 1 @ 42", 42.0)]
    [InlineData("x >= 1 @ REQUIRED", 1_001_001_000.0)]
    public void ParsesStrengths(string text, double expected)
    {
        var c = ConstraintParser.Parse(text, new VariableScope());
        Assert.Equal(expected, c.Strength);
    }

    [Fact]
    public void PrecedenceParenthesesAndUnaryMinus()
    {
        var scope = new VariableScope();
        var e = ConstraintParser.ParseExpression("-(a - 2*(b + 3)) / 2 + 1.5e1", scope);
        var a = scope["a"];
        var b = scope["b"];
        a.Value = 4;
        b.Value = 1;
        // -(4 - 2*4)/2 + 15 = 2 + 15
        Assert.Equal(17.0, e.Value, Tol);
    }

    [Fact]
    public void ChainsExpandToSeveralConstraints()
    {
        var scope = new VariableScope();
        var cs = ConstraintParser.ParseAll("0 <= x <= 100 @ strong", scope);
        Assert.Equal(2, cs.Count);
        Assert.All(cs, c => Assert.Equal(Strength.Strong, c.Strength));
        Assert.Equal(RelationalOperator.LessOrEqual, cs[0].Operator);
        Assert.Equal(RelationalOperator.LessOrEqual, cs[1].Operator);
    }

    [Fact]
    public void ProgramsWithCommentsAndSeparators()
    {
        const string program = """
            # a column of two boxes
            a.top == 0; a.height == 40
            b.top == a.top + a.height + 8   # gap
            b.height == a.height * 2 @ medium

            """;
        var scope = new VariableScope();
        var cs = ConstraintParser.ParseAll(program, scope);
        Assert.Equal(4, cs.Count);

        var s = new Solver();
        s.AddConstraints(cs);
        s.UpdateVariables();
        Assert.Equal(48, scope["b.top"].Value, Tol);
        Assert.Equal(80, scope["b.height"].Value, Tol);
    }

    [Theory]
    [InlineData("x * y == 1", "non-linear", 1, 3)]
    [InlineData("1 / x == 1", "non-linear", 1, 3)]
    [InlineData("x / 0 == 1", "division by zero", 1, 3)]
    [InlineData("x + == 1", "expected a number, variable or '('", 1, 5)]
    [InlineData("x < 1", "strict comparison", 1, 3)]
    [InlineData("x == 1 @ mighty", "unknown strength", 1, 10)]
    [InlineData("(x + 1 == 2", "expected ')'", 1, 8)]
    [InlineData("x + 1", "expected '==', '<=' or '>='", 1, 6)]
    [InlineData("1 + 2 == 3", "does not mention any variable", 1, 11)]
    [InlineData("x == 1\ny == 2 2", "expected end of constraint", 2, 8)]
    [InlineData("x == $", "unexpected character '$'", 1, 6)]
    public void ReportsErrorsWithPositions(string text, string fragment, int line, int column)
    {
        var ex = Assert.Throws<ParseException>(() => ConstraintParser.ParseAll(text, new VariableScope()));
        Assert.Contains(fragment, ex.Reason);
        Assert.Equal(line, ex.Line);
        Assert.Equal(column, ex.Column);
    }

    [Fact]
    public void ConstantFactorsMayAppearOnEitherSide()
    {
        var scope = new VariableScope();
        var c = ConstraintParser.Parse("(2 + 1) * x == x * 4 - (y - y) * z", scope);
        Assert.Single(c.Expression.Terms);
        Assert.Equal(-1.0, c.Expression.Terms[0].Coefficient);
    }

    [Fact]
    public void ClosedScopesRejectUnknownNames()
    {
        var scope = new VariableScope(allowNew: false);
        var x = new Variable("x");
        scope.Add("x", x);
        Assert.Same(x, ConstraintParser.Parse("x >= 0", scope).Expression.Terms[0].Variable);
        var ex = Assert.Throws<ParseException>(() => ConstraintParser.Parse("x + y >= 0", scope));
        Assert.Contains("unknown variable 'y'", ex.Message);
    }

    [Fact]
    public void ParseRequiresExactlyOneConstraint()
    {
        Assert.Throws<ParseException>(() => ConstraintParser.Parse("0 <= x <= 1", new VariableScope()));
        Assert.Empty(ConstraintParser.ParseAll("  # nothing here\n;;", new VariableScope()));
    }
}
