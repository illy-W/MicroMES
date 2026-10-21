namespace MicroMES.ApiFeatures.CalendarFeature.Domain;

public sealed record Holiday(DateOnly Date, string Name);

/// <summary>Italian national holidays: fixed dates plus Easter Sunday and Easter Monday (Gregorian computus).</summary>
public static class ItalianHolidays
{
    public static DateOnly EasterSunday(int year)
    {
        // Anonymous Gregorian algorithm (Meeus/Jones/Butcher).
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }

    public static List<Holiday> ForYear(int year)
    {
        var easter = EasterSunday(year);
        return
        [
            new(new DateOnly(year, 1, 1), "Capodanno"),
            new(new DateOnly(year, 1, 6), "Epifania"),
            new(easter, "Pasqua"),
            new(easter.AddDays(1), "Lunedì dell'Angelo (Pasquetta)"),
            new(new DateOnly(year, 4, 25), "Festa della Liberazione"),
            new(new DateOnly(year, 5, 1), "Festa dei lavoratori"),
            new(new DateOnly(year, 6, 2), "Festa della Repubblica"),
            new(new DateOnly(year, 8, 15), "Assunzione di Maria (Ferragosto)"),
            new(new DateOnly(year, 11, 1), "Ognissanti"),
            new(new DateOnly(year, 12, 8), "Immacolata Concezione"),
            new(new DateOnly(year, 12, 25), "Natale"),
            new(new DateOnly(year, 12, 26), "Santo Stefano")
        ];
    }
}
