using TripShare.Domain.Entities;

namespace TripShare.Application.Contracts;

public sealed record RoutePointDto(
    Guid Id,
    int OrderIndex,
    RoutePointType Type,
    double Lat,
    double Lng,
    string DisplayAddress
);

public sealed record SegmentDto(
    Guid Id,
    int OrderIndex,
    Guid FromRoutePointId,
    Guid ToRoutePointId,
    decimal Price,
    string Currency
);

public sealed record TripSummaryDto(
    Guid Id,
    Guid DriverId,
    string DriverName,
    string? DriverPhotoUrl,
    DateTimeOffset DepartureTimeUtc,
    int SeatsTotal,
    string Currency,
    TripStatus Status,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? ArrivedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    double? CurrentLat,
    double? CurrentLng,
    double? CurrentHeading,
    DateTimeOffset? LocationUpdatedAtUtc,
    IReadOnlyList<RoutePointDto> RoutePoints,
    IReadOnlyList<SegmentDto> Segments,
    string? Notes
);

public sealed record CreateTripRequest(
    DateTimeOffset DepartureTimeUtc,
    int SeatsTotal,
    string Currency,
    string? Notes,
    bool InstantBook,
    int BookingCutoffMinutes,
    int PendingBookingExpiryMinutes,
    IReadOnlyList<CreateRoutePointRequest> RoutePoints,
    IReadOnlyList<CreateSegmentPriceRequest> SegmentPrices
);

public sealed record CreateRoutePointRequest(
    int OrderIndex,
    RoutePointType Type,
    double Lat,
    double Lng,
    string DisplayAddress,
    string? PlaceId
);

public sealed record CreateSegmentPriceRequest(
    int OrderIndex,
    decimal Price
);

public sealed record SearchTripsRequest(
    string? Query,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    decimal? MaxPricePerSeat,
    decimal? MinDriverRating,
    bool VerifiedDriversOnly,
    int Page = 1,
    int PageSize = 20
);

public sealed record UpdateTripStatusRequest(string Status, string? Reason);

public sealed record UpdateTripLocationRequest(double Lat, double Lng, double? Heading);

public sealed record EtaResultDto(Guid BookingId, int EtaToPickupSeconds, int EtaToDropoffSeconds, DateTimeOffset CalculatedAtUtc);

public sealed record TripEtaResponse(Guid TripId, IReadOnlyList<EtaResultDto> Etas);

public sealed record TripLocationUpdateDto(
    Guid TripId,
    double Lat,
    double Lng,
    double? Heading,
    DateTimeOffset UpdatedAtUtc);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
