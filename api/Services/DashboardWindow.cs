using System.Diagnostics.CodeAnalysis;

namespace api.Services;

/// <summary>
/// Validation and trend bucketing shared by the Overview and Feedback dashboards.
/// Windows are a whole number of days (expressed in hours) to keep trend bucketing
/// and query ranges bounded. Windows of 24h use hourly buckets, longer windows use
/// daily buckets aligned to local midnight in the requested time zone.
/// </summary>
public static class DashboardWindow
{
    public const int MinDays = 1;
    public const int MaxDays = 365;

    public static bool TryParse(
        int windowHours,
        string windowParameterName,
        string timeZoneId,
        [NotNullWhen(true)] out TimeZoneInfo? timeZone,
        [NotNullWhen(false)] out string? error
    )
    {
        timeZone = null;
        if (windowHours % 24 != 0 || windowHours / 24 < MinDays || windowHours / 24 > MaxDays)
        {
            error =
                $"{windowParameterName} must be a whole number of days between {MinDays} and {MaxDays}, "
                + $"expressed in hours (a multiple of 24 from {MinDays * 24} to {MaxDays * 24})";
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            error = null;
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            error = $"Unknown time zone '{timeZoneId}'";
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            error = $"Invalid time zone '{timeZoneId}'";
            return false;
        }
    }

    public static List<DateTime> BuildBucketBoundaries(
        int windowHours,
        TimeZoneInfo timeZone,
        DateTime now
    )
    {
        if (windowHours <= 24)
        {
            var currentHour = new DateTime(
                now.Year,
                now.Month,
                now.Day,
                now.Hour,
                0,
                0,
                DateTimeKind.Utc
            );
            return Enumerable
                .Range(0, windowHours + 1)
                .Select(index => currentHour.AddHours(index - windowHours + 1))
                .ToList();
        }

        var dayCount = windowHours / 24;
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).Date;
        return Enumerable
            .Range(0, dayCount + 1)
            .Select(index =>
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(
                        localToday.AddDays(index - dayCount + 1),
                        DateTimeKind.Unspecified
                    ),
                    timeZone
                )
            )
            .ToList();
    }

    public static DateTime GetBucketEnd(
        DateTime bucketStart,
        int windowHours,
        TimeZoneInfo timeZone
    )
    {
        if (windowHours <= 24)
            return bucketStart.AddHours(1);

        var localDate = TimeZoneInfo.ConvertTimeFromUtc(bucketStart, timeZone).Date;
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified),
            timeZone
        );
    }
}
