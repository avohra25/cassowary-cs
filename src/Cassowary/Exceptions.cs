using System;

namespace Cassowary;

/// <summary>Base class for all errors raised by the solver.</summary>
public abstract class CassowaryException : Exception
{
    /// <summary>Creates the exception.</summary>
    protected CassowaryException(string message) : base(message) { }
}

/// <summary>The constraint instance was already added to the solver.</summary>
public sealed class DuplicateConstraintException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public DuplicateConstraintException(Constraint constraint)
        : base($"duplicate constraint: {constraint}") => Constraint = constraint;

    /// <summary>The offending constraint.</summary>
    public Constraint Constraint { get; }
}

/// <summary>A required constraint conflicts with the required constraints already in the solver.</summary>
public sealed class UnsatisfiableConstraintException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public UnsatisfiableConstraintException(Constraint constraint)
        : base($"unsatisfiable constraint: {constraint}") => Constraint = constraint;

    /// <summary>The offending constraint (it was not added).</summary>
    public Constraint Constraint { get; }
}

/// <summary>The constraint is not in the solver.</summary>
public sealed class UnknownConstraintException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public UnknownConstraintException(Constraint constraint)
        : base($"unknown constraint: {constraint}") => Constraint = constraint;

    /// <summary>The offending constraint.</summary>
    public Constraint Constraint { get; }
}

/// <summary>The variable is already registered as an edit variable.</summary>
public sealed class DuplicateEditVariableException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public DuplicateEditVariableException(Variable variable)
        : base($"duplicate edit variable: {variable}") => Variable = variable;

    /// <summary>The offending variable.</summary>
    public Variable Variable { get; }
}

/// <summary>The variable is not registered as an edit variable.</summary>
public sealed class UnknownEditVariableException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public UnknownEditVariableException(Variable variable)
        : base($"unknown edit variable: {variable}") => Variable = variable;

    /// <summary>The offending variable.</summary>
    public Variable Variable { get; }
}

/// <summary>Edit variables cannot have required strength.</summary>
public sealed class BadRequiredStrengthException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public BadRequiredStrengthException()
        : base("an edit variable may not have required strength") { }
}

/// <summary>The tableau reached a state that should be impossible; indicates a solver bug.</summary>
public sealed class InternalSolverException : CassowaryException
{
    /// <summary>Creates the exception.</summary>
    public InternalSolverException(string message) : base("internal solver error: " + message) { }
}
