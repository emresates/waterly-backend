using Waterly.Api.Exceptions;

namespace Waterly.Api.Helpers;

// Rules used everywhere in the app:
// - DateTimeOffset (UTC) = a real moment in time, e.g. when water was consumed.
// - DateOnly             = a calendar day in the user's local time zone, e.g. "2026-11-23".
// - TimeOnly             = a wall-clock time without a date, e.g. reminder start "09:00".
public static class DateTimeHelper
{
    public const string DefaultTimeZone = "Europe/Istanbul";

    // Only IANA ids such as "Europe/Istanbul" are accepted, so data is portable between Linux, macOS and Windows.
    public static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone)
            && timeZone.HasIanaId)
        {
            return timeZone;
        }

        throw AppException.BadRequest(ErrorCodes.InvalidTimeZone,
            $"Geçersiz saat dilimi: '{timeZoneId}'. IANA formatında olmalı, örneğin 'Europe/Istanbul'.");
    }

    public static void EnsureValidTimeZone(string timeZoneId) => GetTimeZone(timeZoneId);

    // Converts a UTC moment to the user's wall-clock time. Daylight saving time is handled by TimeZoneInfo.
    public static DateTimeOffset ToLocalTime(DateTimeOffset moment, string timeZoneId) =>
        TimeZoneInfo.ConvertTime(moment, GetTimeZone(timeZoneId));

    public static DateOnly ToLocalDate(DateTimeOffset moment, string timeZoneId) =>
        DateOnly.FromDateTime(ToLocalTime(moment, timeZoneId).DateTime);

    public static DateOnly GetToday(TimeProvider timeProvider, string timeZoneId) =>
        ToLocalDate(timeProvider.GetUtcNow(), timeZoneId);

    public static IEnumerable<DateOnly> EachDay(DateOnly from, DateOnly to)
    {
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm");
}
