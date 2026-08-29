using System.Text.RegularExpressions;
using Aegis.Application.Dtos.Flights;

namespace Aegis.Application.Flights;

public enum FlightCategory
{
    Commercial,
    Private,
    Military
}

public static partial class FlightCategoryClassifier
{
    private static readonly HashSet<string> MilitaryPrefixes =
    [
        "RCH", "REACH", "EVAC", "SPAR", "SAM", "CNV", "CNVY", "PAT", "NAVY", "ARMY", "USAF", "USN",
        "USCG", "RAF", "RRR", "ASCOT", "IAM", "FAF", "GAF", "BAF", "IAF", "KEAF", "PLF", "RCAF",
        "RFF", "AFI", "DUKE", "HUN", "VIPER", "IRON", "MOXY", "NCHO", "TEAL", "TORCH", "NATO",
        "CONAN", "TITAN", "LAGR", "LAGER", "MULE", "COBRA", "HAWK", "SHELL", "TIGR", "TOPCAT",
        "LION", "EAGLE", "FURY", "JAKE", "KING", "MAVER", "NITE", "PACK", "POKE", "REDE",
        "SORD", "STRI", "TALON", "ZETA", "FAB", "FORCA", "EXFIL"
    ];

    public static FlightCategory Classify(AircraftMarkerDto aircraft) =>
        Classify(aircraft.Icao24, aircraft.Callsign);

    public static FlightCategory Classify(string icao24, string? callsign)
    {
        var hex = icao24.Trim().ToLowerInvariant();
        if (hex.StartsWith("ae", StringComparison.Ordinal) ||
            hex.StartsWith("43c", StringComparison.Ordinal) ||
            hex.StartsWith("3ea", StringComparison.Ordinal))
        {
            return FlightCategory.Military;
        }

        var cs = callsign?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(cs))
        {
            return FlightCategory.Private;
        }

        if (IsMilitaryCallsign(cs))
        {
            return FlightCategory.Military;
        }

        if (IsPrivateCallsign(cs))
        {
            return FlightCategory.Private;
        }

        if (IsCommercialCallsign(cs))
        {
            return FlightCategory.Commercial;
        }

        return FlightCategory.Private;
    }

    public static string ToKey(FlightCategory category) => category switch
    {
        FlightCategory.Commercial => "commercial",
        FlightCategory.Military => "military",
        _ => "private"
    };

    public static string ToDisplayName(FlightCategory category) => category switch
    {
        FlightCategory.Commercial => "Comercial",
        FlightCategory.Military => "Militar",
        _ => "Particular"
    };

    private static bool IsMilitaryCallsign(string callsign)
    {
        if (AllDigitsCallsign().IsMatch(callsign))
        {
            return true;
        }

        var prefix3 = callsign.Length >= 3 ? callsign[..3] : callsign;
        if (MilitaryPrefixes.Contains(prefix3))
        {
            return true;
        }

        if (callsign.Length >= 4 && MilitaryPrefixes.Contains(callsign[..4]))
        {
            return true;
        }

        return MilitaryCallsign().IsMatch(callsign);
    }

    private static bool IsCommercialCallsign(string callsign)
    {
        var match = AirlineCallsign().Match(callsign);
        if (!match.Success)
        {
            return false;
        }

        var code = match.Groups["code"].Value;
        if (MilitaryPrefixes.Contains(code) ||
            (code.Length >= 3 && MilitaryPrefixes.Contains(code[..3])))
        {
            return false;
        }

        if (code.StartsWith('N') && code.Length <= 3)
        {
            return false;
        }

        return code.Length is 2 or 3;
    }

    private static bool IsPrivateCallsign(string callsign)
    {
        if (PrivateRegistration().IsMatch(callsign))
        {
            return true;
        }

        if (callsign.Length <= 5 && !AirlineCallsign().IsMatch(callsign))
        {
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^\d{4,8}$", RegexOptions.CultureInvariant)]
    private static partial Regex AllDigitsCallsign();

    [GeneratedRegex(@"^[A-Z]{2,3}\d{1,4}[A-Z]?$", RegexOptions.CultureInvariant)]
    private static partial Regex AirlineCallsign();

    [GeneratedRegex(@"^(N\d+[A-Z0-9]*|PP[- ]?[A-Z0-9]{3}|PT[- ]?[A-Z0-9]{3}|PR[- ]?[A-Z0-9]{3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrivateRegistration();

    [GeneratedRegex(@"^(AF|NAV|ARM|MAR|COB|TOP|VIP|EXEC|SVC)\d+", RegexOptions.CultureInvariant)]
    private static partial Regex MilitaryCallsign();
}
