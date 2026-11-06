using System.Globalization;

namespace Tolerance.Core;

public enum DecimalParseError { None, InvalidFormat, PrecisionExceeded }

/// <summary>Parses decimal input without accepting a silently rounded value.</summary>
public static class ExactDecimal
{
    public static bool TryParse(string? text, bool allowSign, out decimal value, out DecimalParseError error)
    {
        value = 0;
        error = DecimalParseError.InvalidFormat;
        var input = text?.Trim();
        if (string.IsNullOrEmpty(input)) return false;
        var styles = NumberStyles.AllowDecimalPoint | (allowSign ? NumberStyles.AllowLeadingSign : NumberStyles.None);
        if (!decimal.TryParse(input, styles, CultureInfo.InvariantCulture, out value)) return false;
        var parts = input.TrimStart('+', '-').Split('.');
        var whole = parts[0].TrimStart('0');
        var fraction = parts.Length > 1 ? parts[1].TrimEnd('0') : "";
        var canonical = (whole.Length == 0 ? "0" : whole) + (fraction.Length == 0 ? "" : "." + fraction);
        if (input.StartsWith('-') && canonical != "0") canonical = "-" + canonical;
        if (canonical != value.ToString("0.############################", CultureInfo.InvariantCulture))
        {
            error = DecimalParseError.PrecisionExceeded;
            return false;
        }
        error = DecimalParseError.None;
        return true;
    }
}
