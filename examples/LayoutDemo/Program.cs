using System.Globalization;
using System.Text;
using Cassowary;
using Cassowary.Parsing;
using LayoutDemo;

const string Usage = """
    usage: LayoutDemo <file.layout> [--size WxH]... [--ascii] [--cols N]

    Solves a constraint layout for one or more window sizes. Each extra --size
    re-solves incrementally by suggesting new window dimensions.
    """;

string? path = null;
var sizes = new List<(double W, double H)>();
bool ascii = false;
int cols = 72;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--size" when i + 1 < args.Length:
        {
            var parts = args[++i].Split('x', 'X');
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double w)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
            {
                Console.Error.WriteLine($"bad --size '{args[i]}', expected e.g. 800x600");
                return 2;
            }
            sizes.Add((w, h));
            break;
        }
        case "--ascii":
            ascii = true;
            break;
        case "--cols" when i + 1 < args.Length && int.TryParse(args[i + 1], out int c) && c >= 20:
            cols = c;
            i++;
            break;
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        default:
            if (path is null && !args[i].StartsWith('-')) { path = args[i]; break; }
            Console.Error.WriteLine($"unexpected argument '{args[i]}'\n{Usage}");
            return 2;
    }
}
if (path is null)
{
    Console.Error.WriteLine(Usage);
    return 2;
}
if (sizes.Count == 0) sizes.Add((800, 600));

Layout layout;
try
{
    layout = new Layout(File.ReadAllText(path));
}
catch (ParseException e)
{
    Console.Error.WriteLine($"{path}:{e.Message}");
    return 1;
}
catch (Exception e) when (e is InvalidOperationException or CassowaryException or IOException)
{
    Console.Error.WriteLine($"{path}: {e.Message}");
    return 1;
}

foreach (var (w, h) in sizes)
{
    layout.Resize(w, h);
    Console.WriteLine($"== window {Fmt(layout.Window.Width.Value)} x {Fmt(layout.Window.Height.Value)} ==");
    Console.WriteLine($"{"box",-12} {"left",8} {"top",8} {"width",8} {"height",8}");
    foreach (var b in layout.Boxes.Where(b => b != layout.Window))
        Console.WriteLine($"{b.Name,-12} {Fmt(b.Left.Value),8} {Fmt(b.Top.Value),8} {Fmt(b.Width.Value),8} {Fmt(b.Height.Value),8}");
    if (ascii) Console.Write(Render(layout, cols));
    Console.WriteLine();
}
return 0;

static string Fmt(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

static string Render(Layout layout, int cols)
{
    double ww = Math.Max(layout.Window.Width.Value, 1);
    double wh = Math.Max(layout.Window.Height.Value, 1);
    // Boxes are drawn inside a one-cell window frame.
    double sx = (cols - 3) / ww;
    double sy = sx / 2; // terminal cells are roughly twice as tall as wide
    int rows = Math.Max(3, (int)Math.Round(wh * sy) + 3);
    var grid = new char[rows, cols];
    for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            grid[r, c] = ' ';

    void Rect(Box b, char h, char v)
    {
        int X(double px) => Clamp(1 + (int)Math.Round(px * sx), cols);
        int Y(double px) => Clamp(1 + (int)Math.Round(px * sy), rows);
        int x0 = X(b.Left.Value), x1 = X(b.Right.Value), y0 = Y(b.Top.Value), y1 = Y(b.Bottom.Value);
        if (b == layout.Window) { x0 = y0 = 0; x1 = cols - 1; y1 = rows - 1; }
        for (int x = x0; x <= x1; x++) { grid[y0, x] = h; grid[y1, x] = h; }
        for (int y = y0; y <= y1; y++) { grid[y, x0] = v; grid[y, x1] = v; }
        grid[y0, x0] = grid[y0, x1] = grid[y1, x0] = grid[y1, x1] = '+';
        // Label sits in the top border so nested boxes never overwrite it.
        if (b != layout.Window && x1 - x0 >= 4)
        {
            string label = b.Name.Length > x1 - x0 - 3 ? b.Name[..(x1 - x0 - 3)] : b.Name;
            for (int k = 0; k < label.Length; k++) grid[y0, x0 + 2 + k] = label[k];
        }
    }

    Rect(layout.Window, '=', '|');
    foreach (var b in layout.Boxes.Where(b => b != layout.Window)) Rect(b, '-', '|');

    var sb = new StringBuilder();
    for (int r = 0; r < rows; r++)
    {
        for (int c = 0; c < cols; c++) sb.Append(grid[r, c]);
        sb.Append('\n');
    }
    return sb.ToString().Replace(" \n", "\n");

    static int Clamp(int v, int n) => Math.Clamp(v, 0, n - 1);
}
