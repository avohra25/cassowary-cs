namespace Cassowary.Parsing;

/// <summary>Malformed or non-linear constraint text.</summary>
public sealed class ParseException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public ParseException(string message, int line, int column)
        : base($"{line}:{column}: {message}")
    {
        Line = line;
        Column = column;
        Reason = message;
    }

    /// <summary>1-based line of the error.</summary>
    public int Line { get; }
    /// <summary>1-based column of the error.</summary>
    public int Column { get; }
    /// <summary>The message without the position prefix.</summary>
    public string Reason { get; }
}
