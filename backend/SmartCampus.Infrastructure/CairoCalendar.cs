namespace SmartCampus.Infrastructure;

public static class CairoCalendar
{
    public static DateTimeOffset StartOfDay(DateOnly date)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // Cairo can skip midnight at the spring transition. Use the first actual instant of that date.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
