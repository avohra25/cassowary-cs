using Cassowary;
using Cassowary.Parsing;

namespace LayoutDemo;

/// <summary>A rectangle whose edges are solver variables.</summary>
public sealed class Box
{
    public static readonly string[] Attributes =
        { "left", "right", "top", "bottom", "width", "height", "centerX", "centerY" };

    public Box(string name, VariableScope scope)
    {
        Name = name;
        Left = scope.GetOrAdd(name + ".left");
        Right = scope.GetOrAdd(name + ".right");
        Top = scope.GetOrAdd(name + ".top");
        Bottom = scope.GetOrAdd(name + ".bottom");
        Width = scope.GetOrAdd(name + ".width");
        Height = scope.GetOrAdd(name + ".height");
        CenterX = scope.GetOrAdd(name + ".centerX");
        CenterY = scope.GetOrAdd(name + ".centerY");
    }

    public string Name { get; }
    public Variable Left { get; }
    public Variable Right { get; }
    public Variable Top { get; }
    public Variable Bottom { get; }
    public Variable Width { get; }
    public Variable Height { get; }
    public Variable CenterX { get; }
    public Variable CenterY { get; }

    /// <summary>The geometric relations every box obeys.</summary>
    public IEnumerable<Constraint> Intrinsic()
    {
        yield return Right.Eq(Left + Width);
        yield return Bottom.Eq(Top + Height);
        yield return CenterX.Eq(Left + Width / 2);
        yield return CenterY.Eq(Top + Height / 2);
        yield return Width >= 0;
        yield return Height >= 0;
    }
}

/// <summary>
/// A layout file: constraints over <c>box.attribute</c> names. Boxes are
/// discovered from the names used; the special box <c>window</c> is pinned at
/// the origin and its size is driven through edit variables.
/// </summary>
public sealed class Layout
{
    private readonly Solver _solver = new();
    private readonly VariableScope _scope = new();
    private readonly List<Box> _boxes = new();

    public Layout(string source)
    {
        var user = ConstraintParser.ParseAll(source, _scope);

        foreach (string name in _scope.Names.ToList())
        {
            int dot = name.LastIndexOf('.');
            if (dot <= 0 || !Box.Attributes.Contains(name[(dot + 1)..]))
                throw new ParseException(
                    $"'{name}' is not a box attribute (expected box.{string.Join("|box.", Box.Attributes)})", 1, 1);
            string box = name[..dot];
            if (_boxes.All(b => b.Name != box)) _boxes.Add(new Box(box, _scope));
        }

        Window = _boxes.FirstOrDefault(b => b.Name == "window") ?? new Box("window", _scope);
        if (!_boxes.Contains(Window)) _boxes.Insert(0, Window);

        foreach (var b in _boxes) _solver.AddConstraints(b.Intrinsic());
        _solver.AddConstraint(Window.Left.Eq(0));
        _solver.AddConstraint(Window.Top.Eq(0));
        // Every box stays inside the window.
        foreach (var b in _boxes.Where(b => b != Window))
        {
            _solver.AddConstraint(b.Left >= Window.Left);
            _solver.AddConstraint(b.Top >= Window.Top);
            _solver.AddConstraint(b.Right <= Window.Right);
            _solver.AddConstraint(b.Bottom <= Window.Bottom);
        }

        foreach (var c in user)
        {
            try
            {
                _solver.AddConstraint(c);
            }
            catch (UnsatisfiableConstraintException)
            {
                throw new InvalidOperationException($"layout is over-constrained at: {c}");
            }
        }

        _solver.AddEditVariable(Window.Width, Strength.Strong);
        _solver.AddEditVariable(Window.Height, Strength.Strong);
    }

    public Box Window { get; }
    public IReadOnlyList<Box> Boxes => _boxes;

    /// <summary>Resizes the window (incrementally, via the dual simplex) and updates all boxes.</summary>
    public void Resize(double width, double height)
    {
        _solver.SuggestValue(Window.Width, width);
        _solver.SuggestValue(Window.Height, height);
        _solver.UpdateVariables();
    }
}
