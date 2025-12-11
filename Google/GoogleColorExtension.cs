using Google.Apis.Sheets.v4.Data;

namespace BensEngineeringMetrics.Google;

public static class GoogleColorExtension
{
    // Convenience: create a Google Sheets Color (nullable doubles) from System.Drawing.Color
    public static Color ToGoogleColor(this System.Drawing.Color c)
    {
        var (r, g, b, a) = (c.R / 255.0, c.G / 255.0, c.B / 255.0, c.A / 255.0);
        return new Color
        {
            Red = (float)r,
            Green = (float)g,
            Blue = (float)b,
            Alpha = (float)a
        };
    }
}
