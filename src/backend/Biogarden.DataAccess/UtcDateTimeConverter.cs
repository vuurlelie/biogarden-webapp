using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Biogarden.DataAccess;

// SQL datetime2 has no timezone marker. Application timestamps are stored as UTC.
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
