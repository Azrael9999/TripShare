using System;

namespace TripShare.Api.Helpers;

public static class GeoHelper
{
    public static double DistanceInKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lng2 - lng1);

        lat1 = DegreesToRadians(lat1);
        lat2 = DegreesToRadians(lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    public static bool IsValidCoordinate(double lat, double lng)
        => lat is >= -90 and <= 90 && lng is >= -180 and <= 180;

    private static double DegreesToRadians(double deg) => deg * (Math.PI / 180d);
}
