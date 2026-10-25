namespace LibraryAdmin.Web.Formatting;

public static class LocalTimeDisplay
{
    private static readonly TimeZoneInfo DisplayZone = ResolveDisplayZone();

    public static string Format(DateTime value, string format = "yyyy-MM-dd HH:mm") =>
        Format(value, DisplayZone, format);

    public static string Format(DateTime value, TimeZoneInfo zone, string format = "yyyy-MM-dd HH:mm")
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, zone).ToString(format);
    }

    private static TimeZoneInfo ResolveDisplayZone()
    {
        foreach (var id in new[] { "China Standard Time", "Asia/Shanghai" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}

public static class LocalTimeDisplayExtensions
{
    public static string ToLocalDisplay(this DateTime value, string format = "yyyy-MM-dd HH:mm") =>
        LocalTimeDisplay.Format(value, format);

    public static string ToLocalDisplay(this DateTime? value, string format = "yyyy-MM-dd HH:mm") =>
        value is null ? "—" : LocalTimeDisplay.Format(value.Value, format);
}
