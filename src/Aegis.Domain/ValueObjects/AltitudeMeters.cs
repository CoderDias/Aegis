namespace Aegis.Domain.ValueObjects;

public sealed class AltitudeMeters : IEquatable<AltitudeMeters>
{
    public double? Value { get; }

    public AltitudeMeters(double? value) => Value = value;

    public bool IsInRange(double min, double max)
    {
        if (!Value.HasValue)
        {
            return false;
        }

        return Value.Value >= min && Value.Value <= max;
    }

    public bool Equals(AltitudeMeters? other) =>
        other is not null &&
        Nullable.Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is AltitudeMeters other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(AltitudeMeters? left, AltitudeMeters? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(AltitudeMeters? left, AltitudeMeters? right) => !(left == right);

    public override string ToString() => Value?.ToString() ?? "null";
}
