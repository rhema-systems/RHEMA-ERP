using System.Globalization;
using System.Text.Json;

namespace ErpSystem.Core.Models.HR;

/// <summary>A WGS-84 vertex of a polygon geofence.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// Parsing and geometry for polygon geofence zones.
///
/// <para>The zone stores its boundary as JSON in <c>GeofenceZone.PolygonCoordinatesJson</c>. The
/// documented shape is <c>[{"lat":5.6,"lng":-0.18}, ...]</c>; the parser also accepts
/// <c>latitude</c>/<c>longitude</c>/<c>lon</c> keys and bare <c>[lat, lng]</c> pairs, because the
/// value used to be typed by hand into a textarea. A closing vertex equal to the first is dropped.</para>
///
/// <para>Containment is ray casting on degrees, which is exact enough for a work site (tens to a few
/// thousand metres). Distances use a local equirectangular projection around the punch, which is
/// within a fraction of a percent at that scale. Neither is meant for continent-sized polygons.</para>
/// </summary>
public static class GeofencePolygon
{
    private const double EarthRadiusMetres = 6371000;

    /// <summary>Fewest vertices that enclose an area.</summary>
    public const int MinimumVertices = 3;

    public static bool TryParse(string? json, out IReadOnlyList<GeoPoint> points, out string? error)
    {
        points = Array.Empty<GeoPoint>();
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Polygon coordinates are required for a polygon zone.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = "Polygon coordinates must be a JSON array of {\"lat\", \"lng\"} points.";
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "Polygon coordinates must be a JSON array of {\"lat\", \"lng\"} points.";
                return false;
            }

            var list = new List<GeoPoint>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                index++;
                if (!TryReadPoint(element, out var point))
                {
                    error = $"Point {index} is not a valid {{\"lat\", \"lng\"}} pair.";
                    return false;
                }

                if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
                {
                    error = $"Point {index} is outside the valid latitude/longitude range.";
                    return false;
                }

                list.Add(point);
            }

            // A GeoJSON-style ring repeats its first vertex at the end; the ray cast closes the ring itself.
            if (list.Count > 1 && list[0] == list[^1])
                list.RemoveAt(list.Count - 1);

            if (list.Count < MinimumVertices)
            {
                error = $"A polygon zone needs at least {MinimumVertices} distinct points.";
                return false;
            }

            points = list;
            return true;
        }
    }

    /// <summary>Canonical <c>[{"lat":..,"lng":..}]</c> form, invariant culture.</summary>
    public static string ToJson(IReadOnlyList<GeoPoint> points)
        => "[" + string.Join(",", points.Select(p =>
            "{\"lat\":" + p.Latitude.ToString("R", CultureInfo.InvariantCulture) +
            ",\"lng\":" + p.Longitude.ToString("R", CultureInfo.InvariantCulture) + "}")) + "]";

    /// <summary>Ray casting: true when the point lies inside the ring (points on an edge count as inside).</summary>
    public static bool Contains(IReadOnlyList<GeoPoint> polygon, double latitude, double longitude)
    {
        if (polygon.Count < MinimumVertices) return false;

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (yi, xi) = (polygon[i].Latitude, polygon[i].Longitude);
            var (yj, xj) = (polygon[j].Latitude, polygon[j].Longitude);

            if (IsOnSegment(latitude, longitude, yi, xi, yj, xj))
                return true;

            var crosses = (yi > latitude) != (yj > latitude)
                          && longitude < (xj - xi) * (latitude - yi) / (yj - yi) + xi;
            if (crosses) inside = !inside;
        }

        return inside;
    }

    /// <summary>Metres from the point to the nearest edge of the ring, whether inside or outside.</summary>
    public static double DistanceToBoundaryMetres(IReadOnlyList<GeoPoint> polygon, double latitude, double longitude)
    {
        if (polygon.Count == 0) return double.NaN;

        // Local flat projection centred on the punch: x = east, y = north, both in metres.
        var cosLat = Math.Cos(ToRadians(latitude));
        (double X, double Y) Project(GeoPoint p) => (
            ToRadians(p.Longitude - longitude) * cosLat * EarthRadiusMetres,
            ToRadians(p.Latitude - latitude) * EarthRadiusMetres);

        var best = double.MaxValue;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = Project(polygon[j]);
            var b = Project(polygon[i]);
            best = Math.Min(best, DistanceToSegment(a, b));
        }

        return best;
    }

    private static bool TryReadPoint(JsonElement element, out GeoPoint point)
    {
        point = default;

        if (element.ValueKind == JsonValueKind.Array)
        {
            var parts = element.EnumerateArray().ToList();
            if (parts.Count < 2 || !TryNumber(parts[0], out var lat) || !TryNumber(parts[1], out var lng))
                return false;
            point = new GeoPoint(lat, lng);
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        double? latitude = null, longitude = null;
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "lat":
                case "latitude":
                    if (TryNumber(property.Value, out var la)) latitude = la;
                    break;
                case "lng":
                case "lon":
                case "long":
                case "longitude":
                    if (TryNumber(property.Value, out var lo)) longitude = lo;
                    break;
            }
        }

        if (latitude is null || longitude is null)
            return false;

        point = new GeoPoint(latitude.Value, longitude.Value);
        return true;
    }

    private static bool TryNumber(JsonElement element, out double value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetDouble(out value) && double.IsFinite(value);
        if (element.ValueKind == JsonValueKind.String)
            return double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        return false;
    }

    private static bool IsOnSegment(double py, double px, double ay, double ax, double by, double bx)
    {
        const double epsilon = 1e-12;
        var cross = (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        if (Math.Abs(cross) > epsilon) return false;
        return px >= Math.Min(ax, bx) - epsilon && px <= Math.Max(ax, bx) + epsilon
            && py >= Math.Min(ay, by) - epsilon && py <= Math.Max(ay, by) + epsilon;
    }

    private static double DistanceToSegment((double X, double Y) a, (double X, double Y) b)
    {
        // The punch is the origin in the projected frame.
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0)
            return Math.Sqrt(a.X * a.X + a.Y * a.Y);

        var t = Math.Clamp(-(a.X * dx + a.Y * dy) / lengthSquared, 0, 1);
        var cx = a.X + t * dx;
        var cy = a.Y + t * dy;
        return Math.Sqrt(cx * cx + cy * cy);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
