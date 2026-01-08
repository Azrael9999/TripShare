/*
Sample data for TripShare (idempotent inserts).
Run after schema creation.
*/

-- Users
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = '11111111-1111-1111-1111-111111111111')
BEGIN
    INSERT INTO dbo.Users (Id, Email, EmailVerified, DisplayName, PhotoUrl, PhoneNumber, AuthProvider, ProviderUserId, IsDriver, IsSuspended, Role, CreatedAt, LastLoginAt, DriverVerified, IdentityVerified, PhoneVerified)
    VALUES (
        '11111111-1111-1111-1111-111111111111',
        'driver.sample@tripshare.local',
        1,
        'Sample Driver',
        NULL,
        '+94770000001',
        'sms',
        '+94770000001',
        1,
        0,
        'user',
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET(),
        1,
        0,
        1
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = '22222222-2222-2222-2222-222222222222')
BEGIN
    INSERT INTO dbo.Users (Id, Email, EmailVerified, DisplayName, PhotoUrl, PhoneNumber, AuthProvider, ProviderUserId, IsDriver, IsSuspended, Role, CreatedAt, LastLoginAt, DriverVerified, IdentityVerified, PhoneVerified)
    VALUES (
        '22222222-2222-2222-2222-222222222222',
        'rider.sample@tripshare.local',
        1,
        'Sample Rider',
        NULL,
        '+94770000002',
        'sms',
        '+94770000002',
        0,
        0,
        'user',
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET(),
        0,
        0,
        1
    );
END

-- Vehicle
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE Id = '55555555-5555-5555-5555-555555555555')
BEGIN
    INSERT INTO dbo.Vehicles (Id, OwnerUserId, Make, Model, Color, PlateNumber, Seats, CreatedAt, UpdatedAt)
    VALUES (
        '55555555-5555-5555-5555-555555555555',
        '11111111-1111-1111-1111-111111111111',
        'Toyota',
        'Prius',
        'Blue',
        'WP-AAA-1234',
        4,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET()
    );
END

-- Trip
IF NOT EXISTS (SELECT 1 FROM dbo.Trips WHERE Id = '33333333-3333-3333-3333-333333333333')
BEGIN
    INSERT INTO dbo.Trips (
        Id,
        DriverId,
        DepartureTimeUtc,
        SeatsTotal,
        BaseCurrencyRate,
        Currency,
        IsPublic,
        DefaultPricePerSeat,
        Notes,
        Status,
        StatusUpdatedAt,
        InstantBook,
        BookingCutoffMinutes,
        PendingBookingExpiryMinutes,
        CurrentLat,
        CurrentLng,
        CurrentHeading,
        LocationUpdatedAt,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        '33333333-3333-3333-3333-333333333333',
        '11111111-1111-1111-1111-111111111111',
        DATEADD(day, 1, SYSUTCDATETIME()),
        3,
        1,
        'LKR',
        1,
        750,
        'Morning commute with AC.',
        0,
        SYSDATETIMEOFFSET(),
        1,
        60,
        30,
        6.9271,
        79.8612,
        90,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET()
    );
END

-- Route points
IF NOT EXISTS (SELECT 1 FROM dbo.TripRoutePoints WHERE Id = '66666666-6666-6666-6666-666666666660')
BEGIN
    INSERT INTO dbo.TripRoutePoints (Id, TripId, OrderIndex, Type, Lat, Lng, DisplayAddress, PlaceId, CreatedAt)
    VALUES ('66666666-6666-6666-6666-666666666660', '33333333-3333-3333-3333-333333333333', 0, 0, 6.9271, 79.8612, 'Colombo Fort Station', 'place-colombo', SYSDATETIMEOFFSET());
END

IF NOT EXISTS (SELECT 1 FROM dbo.TripRoutePoints WHERE Id = '66666666-6666-6666-6666-666666666661')
BEGIN
    INSERT INTO dbo.TripRoutePoints (Id, TripId, OrderIndex, Type, Lat, Lng, DisplayAddress, PlaceId, CreatedAt)
    VALUES ('66666666-6666-6666-6666-666666666661', '33333333-3333-3333-3333-333333333333', 1, 1, 6.8731, 79.8847, 'Bambalapitiya', 'place-bamba', SYSDATETIMEOFFSET());
END

IF NOT EXISTS (SELECT 1 FROM dbo.TripRoutePoints WHERE Id = '66666666-6666-6666-6666-666666666662')
BEGIN
    INSERT INTO dbo.TripRoutePoints (Id, TripId, OrderIndex, Type, Lat, Lng, DisplayAddress, PlaceId, CreatedAt)
    VALUES ('66666666-6666-6666-6666-666666666662', '33333333-3333-3333-3333-333333333333', 2, 2, 6.8650, 79.8990, 'Nugegoda', 'place-nugegoda', SYSDATETIMEOFFSET());
END

-- Segments
IF NOT EXISTS (SELECT 1 FROM dbo.TripSegments WHERE Id = '77777777-7777-7777-7777-777777777770')
BEGIN
    INSERT INTO dbo.TripSegments (Id, TripId, OrderIndex, FromRoutePointId, ToRoutePointId, DistanceKm, Price, Currency, BookedSeats)
    VALUES ('77777777-7777-7777-7777-777777777770', '33333333-3333-3333-3333-333333333333', 0, '66666666-6666-6666-6666-666666666660', '66666666-6666-6666-6666-666666666661', 6.5, 350, 'LKR', 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.TripSegments WHERE Id = '77777777-7777-7777-7777-777777777771')
BEGIN
    INSERT INTO dbo.TripSegments (Id, TripId, OrderIndex, FromRoutePointId, ToRoutePointId, DistanceKm, Price, Currency, BookedSeats)
    VALUES ('77777777-7777-7777-7777-777777777771', '33333333-3333-3333-3333-333333333333', 1, '66666666-6666-6666-6666-666666666661', '66666666-6666-6666-6666-666666666662', 3.4, 400, 'LKR', 1);
END

-- Booking
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE Id = '44444444-4444-4444-4444-444444444444')
BEGIN
    INSERT INTO dbo.Bookings (
        Id,
        TripId,
        PassengerId,
        PickupRoutePointId,
        DropoffRoutePointId,
        PickupLat,
        PickupLng,
        DropoffLat,
        DropoffLng,
        PickupPlaceName,
        DropoffPlaceName,
        Seats,
        PriceTotal,
        Currency,
        Status,
        ProgressStatus,
        PendingExpiresAt,
        CompletedAt,
        ContactRevealed,
        StatusUpdatedAt,
        ProgressUpdatedAt,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        '44444444-4444-4444-4444-444444444444',
        '33333333-3333-3333-3333-333333333333',
        '22222222-2222-2222-2222-222222222222',
        '66666666-6666-6666-6666-666666666660',
        '66666666-6666-6666-6666-666666666662',
        6.9271,
        79.8612,
        6.8650,
        79.8990,
        'Colombo Fort Station',
        'Nugegoda',
        1,
        750,
        'LKR',
        4,
        4,
        NULL,
        DATEADD(hour, -2, SYSDATETIMEOFFSET()),
        1,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET(),
        DATEADD(day, -1, SYSDATETIMEOFFSET()),
        SYSDATETIMEOFFSET()
    );
END

-- Booking segment allocations
IF NOT EXISTS (SELECT 1 FROM dbo.BookingSegmentAllocations WHERE Id = '88888888-8888-8888-8888-888888888880')
BEGIN
    INSERT INTO dbo.BookingSegmentAllocations (Id, BookingId, TripSegmentId, SeatsAllocated)
    VALUES ('88888888-8888-8888-8888-888888888880', '44444444-4444-4444-4444-444444444444', '77777777-7777-7777-7777-777777777770', 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.BookingSegmentAllocations WHERE Id = '88888888-8888-8888-8888-888888888881')
BEGIN
    INSERT INTO dbo.BookingSegmentAllocations (Id, BookingId, TripSegmentId, SeatsAllocated)
    VALUES ('88888888-8888-8888-8888-888888888881', '44444444-4444-4444-4444-444444444444', '77777777-7777-7777-7777-777777777771', 1);
END

-- Ratings
IF NOT EXISTS (SELECT 1 FROM dbo.Ratings WHERE Id = '99999999-9999-9999-9999-999999999990')
BEGIN
    INSERT INTO dbo.Ratings (Id, BookingId, FromUserId, ToUserId, Stars, Comment, CreatedAt)
    VALUES (
        '99999999-9999-9999-9999-999999999990',
        '44444444-4444-4444-4444-444444444444',
        '22222222-2222-2222-2222-222222222222',
        '11111111-1111-1111-1111-111111111111',
        5,
        'Great ride, smooth driving.',
        SYSDATETIMEOFFSET()
    );
END

-- Notifications
IF NOT EXISTS (SELECT 1 FROM dbo.Notifications WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1')
BEGIN
    INSERT INTO dbo.Notifications (Id, UserId, Type, Title, Body, TripId, BookingId, IsRead, CreatedAt, ReadAt)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1',
        '22222222-2222-2222-2222-222222222222',
        1,
        'Booking completed',
        'Your booking was marked completed. Leave a rating!',
        '33333333-3333-3333-3333-333333333333',
        '44444444-4444-4444-4444-444444444444',
        0,
        SYSDATETIMEOFFSET(),
        NULL
    );
END

-- User blocks
IF NOT EXISTS (SELECT 1 FROM dbo.UserBlocks WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2')
BEGIN
    INSERT INTO dbo.UserBlocks (Id, BlockerUserId, BlockedUserId, CreatedAt)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2',
        '22222222-2222-2222-2222-222222222222',
        '11111111-1111-1111-1111-111111111111',
        SYSDATETIMEOFFSET()
    );
END

-- Reports
IF NOT EXISTS (SELECT 1 FROM dbo.Reports WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3')
BEGIN
    INSERT INTO dbo.Reports (Id, ReporterUserId, TargetUserId, TripId, BookingId, TargetType, Reason, Details, Status, AdminNote, CreatedAt, UpdatedAt)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3',
        '22222222-2222-2222-2222-222222222222',
        '11111111-1111-1111-1111-111111111111',
        '33333333-3333-3333-3333-333333333333',
        '44444444-4444-4444-4444-444444444444',
        0,
        'Unsafe driving',
        'Driver was speeding.',
        0,
        NULL,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET()
    );
END

-- Emergency contact
IF NOT EXISTS (SELECT 1 FROM dbo.EmergencyContacts WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4')
BEGIN
    INSERT INTO dbo.EmergencyContacts (Id, UserId, Name, PhoneNumber, Email, ShareLiveTripsByDefault, CreatedAt, UpdatedAt)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4',
        '22222222-2222-2222-2222-222222222222',
        'Emergency Contact',
        '+94770000009',
        'emergency@tripshare.local',
        1,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET()
    );
END

-- Message thread + message
IF NOT EXISTS (SELECT 1 FROM dbo.MessageThreads WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb5')
BEGIN
    INSERT INTO dbo.MessageThreads (Id, TripId, BookingId, ParticipantAId, ParticipantBId, IsClosed, ClosedReason, CreatedAt, UpdatedAt)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb5',
        '33333333-3333-3333-3333-333333333333',
        '44444444-4444-4444-4444-444444444444',
        '11111111-1111-1111-1111-111111111111',
        '22222222-2222-2222-2222-222222222222',
        0,
        NULL,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET()
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Messages WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb6')
BEGIN
    INSERT INTO dbo.Messages (Id, ThreadId, SenderId, Body, IsSystem, SentAt)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb6',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb5',
        '11111111-1111-1111-1111-111111111111',
        'Pickup in 10 minutes.',
        0,
        SYSDATETIMEOFFSET()
    );
END

-- Trip share link
IF NOT EXISTS (SELECT 1 FROM dbo.TripShareLinks WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb7')
BEGIN
    INSERT INTO dbo.TripShareLinks (Id, TripId, CreatedByUserId, EmergencyContactId, Token, ExpiresAt, CreatedAt, RevokedAt)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb7',
        '33333333-3333-3333-3333-333333333333',
        '22222222-2222-2222-2222-222222222222',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4',
        'sample-share-token',
        DATEADD(day, 1, SYSDATETIMEOFFSET()),
        SYSDATETIMEOFFSET(),
        NULL
    );
END

-- Safety incident
IF NOT EXISTS (SELECT 1 FROM dbo.SafetyIncidents WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb8')
BEGIN
    INSERT INTO dbo.SafetyIncidents (Id, UserId, TripId, BookingId, Type, Summary, Status, CreatedAt, UpdatedAt, EscalatedAt, ResolvedAt, ResolvedByUserId, ResolutionNote)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb8',
        '22222222-2222-2222-2222-222222222222',
        '33333333-3333-3333-3333-333333333333',
        '44444444-4444-4444-4444-444444444444',
        1,
        'Reported aggressive driving behavior.',
        0,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET(),
        NULL,
        NULL,
        NULL,
        NULL
    );
END

-- Identity verification request
IF NOT EXISTS (SELECT 1 FROM dbo.IdentityVerificationRequests WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb9')
BEGIN
    INSERT INTO dbo.IdentityVerificationRequests (Id, UserId, DocumentType, DocumentReference, Status, ReviewerNote, ReviewedByUserId, SubmittedAt, ReviewedAt, KycProvider, KycReference)
    VALUES (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb9',
        '11111111-1111-1111-1111-111111111111',
        'DriverLicense',
        'DL-123456',
        0,
        NULL,
        NULL,
        SYSDATETIMEOFFSET(),
        NULL,
        'Manual',
        'KYC-001'
    );
END
