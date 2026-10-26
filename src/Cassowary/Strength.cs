using System;

namespace Cassowary;

/// <summary>
/// Constraint strengths. A strength is a single double built from three
/// lexicographic tiers (strong, medium, weak), each clamped to [0, 1000], so
/// any number of weak violations is (practically) cheaper than one medium one.
/// <see cref="Required"/> constraints must hold exactly.
/// </summary>
public static class Strength
{
    /// <summary>Must be satisfied; never traded off against anything.</summary>
    public static readonly double Required = Create(1000.0, 1000.0, 1000.0);
    /// <summary>The strongest non-required tier.</summary>
    public static readonly double Strong = Create(1.0, 0.0, 0.0);
    /// <summary>The middle tier.</summary>
    public static readonly double Medium = Create(0.0, 1.0, 0.0);
    /// <summary>The weakest tier.</summary>
    public static readonly double Weak = Create(0.0, 0.0, 1.0);

    /// <summary>Builds a strength from tier weights, each scaled by <paramref name="weight"/>.</summary>
    public static double Create(double strong, double medium, double weak, double weight = 1.0)
    {
        double result = 0.0;
        result += Math.Clamp(strong * weight, 0.0, 1000.0) * 1_000_000.0;
        result += Math.Clamp(medium * weight, 0.0, 1000.0) * 1_000.0;
        result += Math.Clamp(weak * weight, 0.0, 1000.0);
        return result;
    }

    /// <summary>Clamps an arbitrary strength into [0, Required].</summary>
    public static double Clip(double value) => Math.Clamp(value, 0.0, Required);

    /// <summary>Short display name for a strength, e.g. "strong" or "medium*2.5".</summary>
    public static string Describe(double s)
    {
        if (s >= Required) return "required";
        if (s == Strong) return "strong";
        if (s == Medium) return "medium";
        if (s == Weak) return "weak";
        return Format.Number(s);
    }
}
