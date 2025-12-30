using TripShare.Domain.Entities;

namespace TripShare.Application.Contracts;

public sealed record UpdateProfileRequest(string DisplayName, string? PhotoUrl, string? PhoneNumber);

public sealed record EmergencyContactRequest(string Name, string? PhoneNumber, string? Email, bool ShareLiveTripsByDefault);
public sealed record EmergencyContactResponse(Guid Id, string Name, string? PhoneNumber, string? Email, bool ShareLiveTripsByDefault);

public sealed record CreateShareLinkRequest(Guid TripId, int ExpiresMinutes, Guid? EmergencyContactId);
public sealed record ShareLinkResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record PanicRequest(Guid? TripId, Guid? BookingId, SafetyIncidentType Type, string Summary);

public sealed record MessageThreadDto(Guid Id, Guid? TripId, Guid? BookingId, Guid ParticipantAId, Guid ParticipantBId, bool IsClosed, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record MessageDto(Guid Id, Guid ThreadId, Guid SenderId, string Body, bool IsSystem, DateTimeOffset SentAt);
public sealed record CreateThreadRequest(Guid BookingId);
public sealed record SendMessageRequest(string Body);

public sealed record IdentityVerificationSubmission(string DocumentType, string DocumentReference, string? KycProvider, string? KycReference);
public sealed record IdentityVerificationReview(bool Approve, string? Note);
public sealed record IdentityVerificationDto(Guid Id, IdentityVerificationStatus Status, string DocumentType, string DocumentReference, DateTimeOffset SubmittedAt, DateTimeOffset? ReviewedAt, string? ReviewerNote, string? KycProvider, string? KycReference);

public sealed record AccountExportDto(
    string Email,
    string DisplayName,
    bool EmailVerified,
    bool PhoneVerified,
    bool IdentityVerified,
    IReadOnlyList<object> Trips,
    IReadOnlyList<object> Bookings,
    IReadOnlyList<object> Ratings,
    IReadOnlyList<object> Reports,
    IReadOnlyList<object> Messages);
