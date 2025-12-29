namespace TripShare.Application.Contracts;

public sealed record CreateBookingRequest(
    Guid TripId,
    Guid PickupRoutePointId,
    Guid DropoffRoutePointId,
    int Seats
);

public sealed record BookingDto(
    Guid Id,
    Guid TripId,
    Guid PassengerId,
    Guid PickupRoutePointId,
    Guid DropoffRoutePointId,
    int Seats,
    decimal PriceTotal,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt
);

public sealed record SetBookingStatusRequest(string Status, string? Reason);

public sealed record CreateRatingRequest(Guid BookingId, int Stars, string? Comment);
