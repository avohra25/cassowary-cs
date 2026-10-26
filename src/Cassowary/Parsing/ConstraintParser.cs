using System.Collections.Generic;

namespace Cassowary.Parsing;

/// <summary>
/// Parses a small constraint language into <see cref="Constraint"/>s.
/// <code>
/// # comments run to the end of the line; ';' or newlines separate constraints
/// sidebar.right + 8 == content.left
/// content.width >= 2 * sidebar.width          @ strong
/// 0 &lt;= x &lt;= 100                                # chains expand to two constraints
/// title.width == 120                          @ weak * 3
/// </code>
/// Grammar:
/// <code>
/// program    := (constraint? SEP)* constraint?
/// constraint := expr (relop expr)+ ('@' strength)?
/// relop      := '==' | '=' | '&lt;=' | '&gt;='
/// strength   := (NAME | NUMBER) ('*' NUMBER)?
/// expr       := term (('+' | '-') term)*
/// term       := unary (('*' | '/') unary)*
/// unary      := '-' unary | '+' unary | primary
/// primary    := NUMBER | NAME | '(' expr ')'
/// </code>
/// Products of two non-constant expressions and division by a variable are
/// rejected as non-linear.
/// </summary>
public sealed class ConstraintParser
{
    private readonly List<Token> _tokens;
    private readonly VariableScope _scope;
    private int _pos;

    private ConstraintParser(string text, VariableScope scope)
    {
        _tokens = Lexer.Tokenize(text);
        _scope = scope;
    }

    /// <summary>Parses a program of zero or more constraints (chains expand to several).</summary>
    public static IReadOnlyList<Constraint> ParseAll(string text, VariableScope scope)
    {
        var p = new ConstraintParser(text, scope);
        var result = new List<Constraint>();
        while (true)
        {
            while (p.Peek.Kind == TokenKind.Separator) p._pos++;
            if (p.Peek.Kind == TokenKind.End) break;
            result.AddRange(p.ParseConstraintChain());
            if (p.Peek.Kind != TokenKind.Separator && p.Peek.Kind != TokenKind.End)
                throw p.Error($"expected end of constraint, found {p.Peek.Describe()}");
        }
        return result;
    }

    /// <summary>Parses exactly one (non-chained) constraint.</summary>
    public static Constraint Parse(string text, VariableScope scope)
    {
        var all = ParseAll(text, scope);
        if (all.Count != 1)
            throw new ParseException($"expected exactly one constraint, found {all.Count}", 1, 1);
        return all[0];
    }

    /// <summary>Parses a single linear expression.</summary>
    public static Expression ParseExpression(string text, VariableScope scope)
    {
        var p = new ConstraintParser(text, scope);
        var e = p.ParseExpr();
        if (p.Peek.Kind != TokenKind.End)
            throw p.Error($"expected end of expression, found {p.Peek.Describe()}");
        return e.Reduce();
    }

    private Token Peek => _tokens[_pos];

    private Token Next() => _tokens[_pos++];

    private ParseException Error(string message, Token? at = null)
    {
        var t = at ?? Peek;
        return new ParseException(message, t.Line, t.Column);
    }

    private static bool IsRelOp(TokenKind k) =>
        k is TokenKind.Equal or TokenKind.LessEqual or TokenKind.GreaterEqual;

    private List<Constraint> ParseConstraintChain()
    {
        var operands = new List<Expression> { ParseExpr() };
        var ops = new List<RelationalOperator>();
        if (!IsRelOp(Peek.Kind))
            throw Error($"expected '==', '<=' or '>=', found {Peek.Describe()}");
        while (IsRelOp(Peek.Kind))
        {
            var op = Next().Kind switch
            {
                TokenKind.LessEqual => RelationalOperator.LessOrEqual,
                TokenKind.GreaterEqual => RelationalOperator.GreaterOrEqual,
                _ => RelationalOperator.Equal,
            };
            ops.Add(op);
            operands.Add(ParseExpr());
        }

        double strength = Strength.Required;
        if (Peek.Kind == TokenKind.At)
        {
            Next();
            strength = ParseStrength();
        }

        var result = new List<Constraint>(ops.Count);
        for (int i = 0; i < ops.Count; i++)
        {
            var c = new Constraint(operands[i] - operands[i + 1], ops[i], strength);
            if (c.Expression.IsConstant)
                throw Error("constraint does not mention any variable");
            result.Add(c);
        }
        return result;
    }

    private double ParseStrength()
    {
        var t = Next();
        double s;
        if (t.Kind == TokenKind.Number)
        {
            s = t.Number;
        }
        else if (t.Kind == TokenKind.Identifier)
        {
            s = t.Text.ToLowerInvariant() switch
            {
                "required" => Strength.Required,
                "strong" => Strength.Strong,
                "medium" => Strength.Medium,
                "weak" => Strength.Weak,
                _ => throw Error($"unknown strength '{t.Text}' (use required, strong, medium, weak or a number)", t),
            };
        }
        else
        {
            throw Error($"expected a strength, found {t.Describe()}", t);
        }

        if (Peek.Kind == TokenKind.Star)
        {
            Next();
            var w = Next();
            if (w.Kind != TokenKind.Number)
                throw Error($"expected a strength weight, found {w.Describe()}", w);
            s *= w.Number;
        }
        return Strength.Clip(s);
    }

    private Expression ParseExpr()
    {
        var left = ParseTerm();
        while (Peek.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            bool plus = Next().Kind == TokenKind.Plus;
            var right = ParseTerm();
            left = plus ? left + right : left - right;
        }
        return left;
    }

    private Expression ParseTerm()
    {
        var left = ParseUnary();
        while (Peek.Kind is TokenKind.Star or TokenKind.Slash)
        {
            var op = Next();
            var right = ParseUnary();
            if (op.Kind == TokenKind.Star)
            {
                if (right.IsConstant) left = left * right.Constant;
                else if (left.IsConstant) left = right * left.Constant;
                else throw Error("non-linear expression: cannot multiply two variable expressions", op);
            }
            else
            {
                if (!right.IsConstant)
                    throw Error("non-linear expression: cannot divide by a variable expression", op);
                if (right.Constant == 0.0)
                    throw Error("division by zero", op);
                left = left / right.Constant;
            }
        }
        return left;
    }

    private Expression ParseUnary()
    {
        if (Peek.Kind == TokenKind.Minus) { Next(); return -ParseUnary(); }
        if (Peek.Kind == TokenKind.Plus) { Next(); return ParseUnary(); }
        return ParsePrimary();
    }

    private Expression ParsePrimary()
    {
        var t = Next();
        switch (t.Kind)
        {
            case TokenKind.Number:
                return new Expression(t.Number);
            case TokenKind.Identifier:
            {
                var v = _scope.Resolve(t.Text)
                    ?? throw Error($"unknown variable '{t.Text}'", t);
                return v;
            }
            case TokenKind.LParen:
            {
                var e = ParseExpr();
                var close = Next();
                if (close.Kind != TokenKind.RParen)
                    throw Error($"expected ')', found {close.Describe()}", close);
                // Reduce eagerly so (x - x) * y counts as constant * y.
                return e.Reduce();
            }
            default:
                throw Error($"expected a number, variable or '(', found {t.Describe()}", t);
        }
    }
}
