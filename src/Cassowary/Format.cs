using System.Globalization;

namespace Cassowary;

internal static class Format
{
    public static string Number(double d) => d.ToString("0.######", CultureInfo.InvariantCulture);
}
