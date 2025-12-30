namespace TripShare.Application.Contracts;

using TripShare.Domain.Entities;

public sealed record CreateBookingRequest(
    Guid TripId,
    Guid PickupRoutePointId,
    Guid DropoffRoutePointId,
    double PickupLat,
    double PickupLng,
    double DropoffLat,
    double DropoffLng,
    string? PickupPlaceName,
    string? PickupPlaceId,
    string? DropoffPlaceName,
    string? DropoffPlaceId,
    int Seats
);

public sealed record BookingDto(
    Guid Id,
    Guid TripId,
    Guid PassengerId,
    Guid PickupRoutePointId,
    Guid DropoffRoutePointId,
    double PickupLat,
    double PickupLng,
    double DropoffLat,
    double DropoffLng,
    string? PickupPlaceName,
    string? PickupPlaceId,
    string? DropoffPlaceName,
    string? DropoffPlaceId,
    int Seats,
    decimal PriceTotal,
    string Currency,
    string Status,
    string ProgressStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset StatusUpdatedAt,
    DateTimeOffset ProgressUpdatedAt,
    DateTimeOffset? CompletedAt
);

public sealed record SetBookingStatusRequest(string Status, string? Reason);

public sealed record UpdateBookingProgressRequest(string Progress, string? Note);

public sealed record CreateRatingRequest(Guid BookingId, int Stars, string? Comment);
