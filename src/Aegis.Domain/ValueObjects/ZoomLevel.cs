using Aegis.Domain.Exceptions;

namespace Aegis.Domain.ValueObjects;

public sealed class ZoomLevel : IEquatable<ZoomLevel>
{
    public const int MinValue = 2;
    public const int MaxValue = 20;

    public int Value { get; }

    private ZoomLevel(int value) => Value = value;

    public static ZoomLevel Create(int value)
    {
        if (value is < MinValue or > MaxValue)
        {
            throw new DomainException($"Zoom level must be between {MinValue} and {MaxValue}. Received {value}.");
        }

        return new ZoomLevel(value);
    }

    public bool ShowsCountries => Value is >= 2 and <= 5;

    public bool ShowsAirports => Value is >= 6 and <= 9;

    public bool ShowsBuildings => Value >= 4;

    public bool ShowsBuildingFootprints => Value >= 17;

    public bool Equals(ZoomLevel? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is ZoomLevel other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(ZoomLevel? left, ZoomLevel? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ZoomLevel? left, ZoomLevel? right) => !(left == right);

    public override string ToString() => Value.ToString();
}
