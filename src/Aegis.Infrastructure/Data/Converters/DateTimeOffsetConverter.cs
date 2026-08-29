using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aegis.Infrastructure.Data.Converters;

public sealed class DateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
{
    public DateTimeOffsetConverter()
        : base(
            v => v.ToString("O"),
            v => DateTimeOffset.Parse(v))
    {
    }
}

public sealed class NullableDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, string?>
{
    public NullableDateTimeOffsetConverter()
        : base(
            v => v.HasValue ? v.Value.ToString("O") : null,
            v => v == null ? null : DateTimeOffset.Parse(v))
    {
    }
}
