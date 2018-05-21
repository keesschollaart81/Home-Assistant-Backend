using System;
using Innovative.SolarCalculator;
using System.Runtime.InteropServices;

namespace Functions
{
    public static class DateTimeExtensions
    {
        public static bool IsDarkOutside(this DateTime value)
        {
            var isOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            var timezone = isOSX ? "Europe/Amsterdam" : "W. Europe Standard Time";

            var west = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var solarTimes = new SolarTimes(value.Date, 51.8310915, 4.6580213);

            var now = TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), west);
            var sunset = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunset.ToUniversalTime(), west);
            var sunrise = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunrise.ToUniversalTime(), west);

            if (now > sunset) return true;
            if (now < sunrise) return true;
            
            return false;
        }
    }
}
