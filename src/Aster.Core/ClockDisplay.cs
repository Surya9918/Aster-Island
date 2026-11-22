using System.Globalization;
namespace Aster.Core;
public static class ClockDisplay
{
 public static string Format(DateTime time,bool twelveHour)=>time.ToString(twelveHour?"h:mm tt":"HH:mm",CultureInfo.InvariantCulture);
}
