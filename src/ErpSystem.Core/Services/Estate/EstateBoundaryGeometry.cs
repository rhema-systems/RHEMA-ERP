using System.Text.Json;

namespace ErpSystem.Core.Services.Estate;

public readonly record struct EstateBoundaryPoint(double Northing, double Easting);

public readonly record struct EstateBoundaryMeasurement(int BeaconCount, decimal AreaSquareFeet);

public static class EstateBoundaryGeometry
{
    private const double Tolerance = 0.000001;
    private const int MaximumBeaconCount = 500;

    public static EstateBoundaryMeasurement ValidateContained(
        string parentBoundaryCoordinates,
        string demarcationBoundaryCoordinates)
    {
        var parent = ParsePolygon(parentBoundaryCoordinates, "main cadastral boundary");
        var child = ParsePolygon(demarcationBoundaryCoordinates, "demarcation boundary");

        // A demarcation is a child parcel; saving it must never redefine the acquired cadastral polygon.
        if (child.Any(point => PointLocation(point, parent) < 0)
            || HasProperBoundaryCrossing(child, parent))
        {
            throw new InvalidOperationException(
                "The demarcation boundary must fall completely within the main cadastral boundary.");
        }

        return new EstateBoundaryMeasurement(child.Count, decimal.Round((decimal)PolygonArea(child), 4));
    }

    public static bool Overlaps(string firstBoundaryCoordinates, string secondBoundaryCoordinates)
    {
        var first = ParsePolygon(firstBoundaryCoordinates, "saved demarcation boundary");
        var second = ParsePolygon(secondBoundaryCoordinates, "demarcation boundary");

        if (HasProperBoundaryCrossing(first, second)
            || first.Any(point => PointLocation(point, second) > 0)
            || second.Any(point => PointLocation(point, first) > 0))
        {
            return true;
        }

        // This catches an identical polygon while still allowing neighbouring parcels to share an edge.
        return first.All(point => PointLocation(point, second) == 0)
            && second.All(point => PointLocation(point, first) == 0);
    }

    public static IReadOnlyList<EstateBoundaryPoint> ParsePolygon(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Record the {label} coordinates.");
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"The {label} must be a coordinate array.");
            }

            var points = document.RootElement
                .EnumerateArray()
                .Select(ReadPoint)
                .ToList();

            if (points.Count > 1 && PointsEqual(points[0], points[^1]))
            {
                points.RemoveAt(points.Count - 1);
            }

            if (points.Count < 3)
            {
                throw new InvalidOperationException($"The {label} requires at least three beacons.");
            }

            if (points.Count > MaximumBeaconCount)
            {
                throw new InvalidOperationException(
                    $"The {label} cannot contain more than {MaximumBeaconCount} beacons.");
            }

            if (points.Select(point => (Round(point.Northing), Round(point.Easting))).Distinct().Count() < 3)
            {
                throw new InvalidOperationException($"The {label} requires at least three distinct beacons.");
            }

            if (HasSelfIntersection(points))
            {
                throw new InvalidOperationException($"The {label} cannot cross itself.");
            }

            if (PolygonArea(points) <= Tolerance)
            {
                throw new InvalidOperationException($"The {label} has no measurable area.");
            }

            return points;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"The {label} coordinates are not valid JSON.");
        }
    }

    private static EstateBoundaryPoint ReadPoint(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Array)
        {
            var values = item.EnumerateArray().Take(2).ToArray();
            if (values.Length == 2
                && values[0].TryGetDouble(out var northing)
                && values[1].TryGetDouble(out var easting)
                && double.IsFinite(northing)
                && double.IsFinite(easting))
            {
                return new EstateBoundaryPoint(northing, easting);
            }
        }

        if (item.ValueKind == JsonValueKind.Object
            && TryReadNumber(item, ["northing", "northingFeet"], out var objectNorthing)
            && TryReadNumber(item, ["easting", "eastingFeet"], out var objectEasting))
        {
            return new EstateBoundaryPoint(objectNorthing, objectEasting);
        }

        throw new InvalidOperationException(
            "Every boundary beacon must contain numeric northing and easting coordinates.");
    }

    private static bool TryReadNumber(
        JsonElement item,
        IReadOnlyCollection<string> propertyNames,
        out double value)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (!propertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetDouble(out value)
                && double.IsFinite(value))
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && double.TryParse(
                    property.Value.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value)
                && double.IsFinite(value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool HasSelfIntersection(IReadOnlyList<EstateBoundaryPoint> polygon)
    {
        for (var first = 0; first < polygon.Count; first++)
        {
            var firstNext = (first + 1) % polygon.Count;
            for (var second = first + 1; second < polygon.Count; second++)
            {
                var secondNext = (second + 1) % polygon.Count;
                if (first == second || firstNext == second || secondNext == first)
                {
                    continue;
                }

                if (SegmentsIntersect(polygon[first], polygon[firstNext], polygon[second], polygon[secondNext]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasProperBoundaryCrossing(
        IReadOnlyList<EstateBoundaryPoint> first,
        IReadOnlyList<EstateBoundaryPoint> second)
    {
        for (var firstIndex = 0; firstIndex < first.Count; firstIndex++)
        {
            var firstNext = (firstIndex + 1) % first.Count;
            for (var secondIndex = 0; secondIndex < second.Count; secondIndex++)
            {
                var secondNext = (secondIndex + 1) % second.Count;
                if (SegmentsProperlyIntersect(
                    first[firstIndex],
                    first[firstNext],
                    second[secondIndex],
                    second[secondNext]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int PointLocation(EstateBoundaryPoint point, IReadOnlyList<EstateBoundaryPoint> polygon)
    {
        var inside = false;
        for (var index = 0; index < polygon.Count; index++)
        {
            var next = (index + 1) % polygon.Count;
            var first = polygon[index];
            var second = polygon[next];
            if (PointOnSegment(point, first, second))
            {
                return 0;
            }

            var intersects = (first.Northing > point.Northing) != (second.Northing > point.Northing)
                && point.Easting < (second.Easting - first.Easting)
                    * (point.Northing - first.Northing)
                    / (second.Northing - first.Northing)
                    + first.Easting;
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside ? 1 : -1;
    }

    private static bool SegmentsIntersect(
        EstateBoundaryPoint firstStart,
        EstateBoundaryPoint firstEnd,
        EstateBoundaryPoint secondStart,
        EstateBoundaryPoint secondEnd)
    {
        var firstOrientation = Orientation(firstStart, firstEnd, secondStart);
        var secondOrientation = Orientation(firstStart, firstEnd, secondEnd);
        var thirdOrientation = Orientation(secondStart, secondEnd, firstStart);
        var fourthOrientation = Orientation(secondStart, secondEnd, firstEnd);

        if (Opposite(firstOrientation, secondOrientation) && Opposite(thirdOrientation, fourthOrientation))
        {
            return true;
        }

        return Math.Abs(firstOrientation) <= Tolerance && PointOnSegment(secondStart, firstStart, firstEnd)
            || Math.Abs(secondOrientation) <= Tolerance && PointOnSegment(secondEnd, firstStart, firstEnd)
            || Math.Abs(thirdOrientation) <= Tolerance && PointOnSegment(firstStart, secondStart, secondEnd)
            || Math.Abs(fourthOrientation) <= Tolerance && PointOnSegment(firstEnd, secondStart, secondEnd);
    }

    private static bool SegmentsProperlyIntersect(
        EstateBoundaryPoint firstStart,
        EstateBoundaryPoint firstEnd,
        EstateBoundaryPoint secondStart,
        EstateBoundaryPoint secondEnd)
    {
        var firstOrientation = Orientation(firstStart, firstEnd, secondStart);
        var secondOrientation = Orientation(firstStart, firstEnd, secondEnd);
        var thirdOrientation = Orientation(secondStart, secondEnd, firstStart);
        var fourthOrientation = Orientation(secondStart, secondEnd, firstEnd);
        return Opposite(firstOrientation, secondOrientation) && Opposite(thirdOrientation, fourthOrientation);
    }

    private static bool PointOnSegment(
        EstateBoundaryPoint point,
        EstateBoundaryPoint start,
        EstateBoundaryPoint end)
        => Math.Abs(Orientation(start, end, point)) <= Tolerance
            && point.Northing >= Math.Min(start.Northing, end.Northing) - Tolerance
            && point.Northing <= Math.Max(start.Northing, end.Northing) + Tolerance
            && point.Easting >= Math.Min(start.Easting, end.Easting) - Tolerance
            && point.Easting <= Math.Max(start.Easting, end.Easting) + Tolerance;

    private static double Orientation(
        EstateBoundaryPoint first,
        EstateBoundaryPoint second,
        EstateBoundaryPoint third)
        => (second.Easting - first.Easting) * (third.Northing - first.Northing)
            - (second.Northing - first.Northing) * (third.Easting - first.Easting);

    private static bool Opposite(double first, double second)
        => first > Tolerance && second < -Tolerance || first < -Tolerance && second > Tolerance;

    private static bool PointsEqual(EstateBoundaryPoint first, EstateBoundaryPoint second)
        => Math.Abs(first.Northing - second.Northing) <= Tolerance
            && Math.Abs(first.Easting - second.Easting) <= Tolerance;

    private static double PolygonArea(IReadOnlyList<EstateBoundaryPoint> polygon)
    {
        var twiceArea = 0d;
        for (var index = 0; index < polygon.Count; index++)
        {
            var next = (index + 1) % polygon.Count;
            twiceArea += polygon[index].Easting * polygon[next].Northing
                - polygon[next].Easting * polygon[index].Northing;
        }

        return Math.Abs(twiceArea) / 2d;
    }

    private static double Round(double value) => Math.Round(value, 6);
}
