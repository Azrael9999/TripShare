/*
 TripShare schema v1
 Idempotent-ish: uses IF NOT EXISTS guards where feasible.
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'dbo')
BEGIN
    EXEC('CREATE SCHEMA dbo');
END
-- Users
IF OBJECT_ID('dbo.Users','U') IS NULL
BEGIN
CREATE TABLE dbo.Users (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email NVARCHAR(320) NOT NULL,
    EmailVerified BIT NOT NULL CONSTRAINT DF_Users_EmailVerified DEFAULT(0),
    DisplayName NVARCHAR(120) NOT NULL,
    PhotoUrl NVARCHAR(1000) NULL,
    PhoneNumber NVARCHAR(64) NULL,
    AuthProvider NVARCHAR(32) NOT NULL,
    ProviderUserId NVARCHAR(128) NOT NULL,
    IsDriver BIT NOT NULL CONSTRAINT DF_Users_IsDriver DEFAULT(0),
    IsSuspended BIT NOT NULL CONSTRAINT DF_Users_IsSuspended DEFAULT(0),
    Role NVARCHAR(32) NOT NULL CONSTRAINT DF_Users_Role DEFAULT('user'),
    CreatedAt DATETIMEOFFSET NOT NULL,
    LastLoginAt DATETIMEOFFSET NULL
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users(Email);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Provider' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX IX_Users_Provider ON dbo.Users(AuthProvider, ProviderUserId);
-- RefreshTokens
IF OBJECT_ID('dbo.RefreshTokens','U') IS NULL
BEGIN
CREATE TABLE dbo.RefreshTokens (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    TokenHash NVARCHAR(256) NOT NULL,
    ExpiresAt DATETIMEOFFSET NOT NULL,
    RevokedAt DATETIMEOFFSET NULL,
    ReplacedByTokenHash NVARCHAR(256) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CreatedIp NVARCHAR(64) NULL,
    UserAgent NVARCHAR(256) NULL,
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id)
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_UserId' AND object_id = OBJECT_ID('dbo.RefreshTokens'))
    CREATE INDEX IX_RefreshTokens_UserId ON dbo.RefreshTokens(UserId);
-- EmailVerificationTokens
IF OBJECT_ID('dbo.EmailVerificationTokens','U') IS NULL
BEGIN
CREATE TABLE dbo.EmailVerificationTokens (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmailVerificationTokens PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    TokenHash NVARCHAR(256) NOT NULL,
    ExpiresAt DATETIMEOFFSET NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    UsedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_EmailVerificationTokens_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id)
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailVerificationTokens_UserId' AND object_id = OBJECT_ID('dbo.EmailVerificationTokens'))
    CREATE INDEX IX_EmailVerificationTokens_UserId ON dbo.EmailVerificationTokens(UserId);
-- Trips
IF OBJECT_ID('dbo.Trips','U') IS NULL
BEGIN
CREATE TABLE dbo.Trips (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Trips PRIMARY KEY,
    DriverId UNIQUEIDENTIFIER NOT NULL,
    DepartureTimeUtc DATETIMEOFFSET NOT NULL,
    SeatsTotal INT NOT NULL,
    BaseCurrencyRate DECIMAL(18, 6) NOT NULL CONSTRAINT DF_Trips_BaseCurrencyRate DEFAULT(1),
    Currency NVARCHAR(8) NOT NULL,
    IsPublic BIT NOT NULL CONSTRAINT DF_Trips_IsPublic DEFAULT(1),
    DefaultPricePerSeat DECIMAL(18, 2) NULL,
    Notes NVARCHAR(2000) NULL,
    Status INT NOT NULL CONSTRAINT DF_Trips_Status DEFAULT(0),
    StatusUpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Trips_StatusUpdatedAt DEFAULT(SYSDATETIMEOFFSET()),
    StartedAtUtc DATETIMEOFFSET NULL,
    ArrivedAtUtc DATETIMEOFFSET NULL,
    CompletedAtUtc DATETIMEOFFSET NULL,
    CancelledAtUtc DATETIMEOFFSET NULL,
    InstantBook BIT NOT NULL CONSTRAINT DF_Trips_InstantBook DEFAULT(0),
    BookingCutoffMinutes INT NOT NULL CONSTRAINT DF_Trips_BookingCutoff DEFAULT(60),
    PendingBookingExpiryMinutes INT NOT NULL CONSTRAINT DF_Trips_PendingExpiry DEFAULT(30),
    CurrentLat FLOAT NULL,
    CurrentLng FLOAT NULL,
    CurrentHeading FLOAT NULL,
    LocationUpdatedAt DATETIMEOFFSET NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_Trips_Users FOREIGN KEY(DriverId) REFERENCES dbo.Users(Id)
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Trips_DriverId' AND object_id = OBJECT_ID('dbo.Trips'))
    CREATE INDEX IX_Trips_DriverId ON dbo.Trips(DriverId);
-- TripRoutePoints
IF OBJECT_ID('dbo.TripRoutePoints','U') IS NULL
BEGIN
CREATE TABLE dbo.TripRoutePoints (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TripRoutePoints PRIMARY KEY,
    TripId UNIQUEIDENTIFIER NOT NULL,
    OrderIndex INT NOT NULL,
    Type INT NOT NULL,
    Lat FLOAT NOT NULL,
    Lng FLOAT NOT NULL,
    DisplayAddress NVARCHAR(400) NOT NULL,
    PlaceId NVARCHAR(128) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_TripRoutePoints_Trips FOREIGN KEY(TripId) REFERENCES dbo.Trips(Id) ON DELETE CASCADE
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TripRoutePoints_Trip_Order' AND object_id = OBJECT_ID('dbo.TripRoutePoints'))
    CREATE UNIQUE INDEX UX_TripRoutePoints_Trip_Order ON dbo.TripRoutePoints(TripId, OrderIndex);
-- TripSegments
IF OBJECT_ID('dbo.TripSegments','U') IS NULL
BEGIN
CREATE TABLE dbo.TripSegments (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TripSegments PRIMARY KEY,
    TripId UNIQUEIDENTIFIER NOT NULL,
    OrderIndex INT NOT NULL,
    FromRoutePointId UNIQUEIDENTIFIER NOT NULL,
    ToRoutePointId UNIQUEIDENTIFIER NOT NULL,
    DistanceKm DECIMAL(10,3) NULL,
    Price DECIMAL(18,2) NOT NULL,
    Currency NVARCHAR(8) NOT NULL,
    BookedSeats INT NOT NULL CONSTRAINT DF_TripSegments_BookedSeats DEFAULT(0),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT FK_TripSegments_Trips FOREIGN KEY(TripId) REFERENCES dbo.Trips(Id) ON DELETE CASCADE,
    CONSTRAINT FK_TripSegments_FromPoint FOREIGN KEY(FromRoutePointId) REFERENCES dbo.TripRoutePoints(Id),
    CONSTRAINT FK_TripSegments_ToPoint FOREIGN KEY(ToRoutePointId) REFERENCES dbo.TripRoutePoints(Id)
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TripSegments_Trip_Order' AND object_id = OBJECT_ID('dbo.TripSegments'))
    CREATE UNIQUE INDEX UX_TripSegments_Trip_Order ON dbo.TripSegments(TripId, OrderIndex);
-- Bookings
IF OBJECT_ID('dbo.Bookings','U') IS NULL
BEGIN
CREATE TABLE dbo.Bookings (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY,
    TripId UNIQUEIDENTIFIER NOT NULL,
    PassengerId UNIQUEIDENTIFIER NOT NULL,
    PickupRoutePointId UNIQUEIDENTIFIER NOT NULL,
    DropoffRoutePointId UNIQUEIDENTIFIER NOT NULL,
    PickupLat FLOAT NOT NULL CONSTRAINT DF_Bookings_PickupLat DEFAULT(0),
    PickupLng FLOAT NOT NULL CONSTRAINT DF_Bookings_PickupLng DEFAULT(0),
    DropoffLat FLOAT NOT NULL CONSTRAINT DF_Bookings_DropoffLat DEFAULT(0),
    DropoffLng FLOAT NOT NULL CONSTRAINT DF_Bookings_DropoffLng DEFAULT(0),
    PickupPlaceName NVARCHAR(200) NULL,
    DropoffPlaceName NVARCHAR(200) NULL,
    PickupPlaceId NVARCHAR(128) NULL,
    DropoffPlaceId NVARCHAR(128) NULL,
    Seats INT NOT NULL,
    PriceTotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_Bookings_PriceTotal DEFAULT(0),
    Currency NVARCHAR(8) NOT NULL,
    Status INT NOT NULL CONSTRAINT DF_Bookings_Status DEFAULT(0),
    ProgressStatus INT NOT NULL CONSTRAINT DF_Bookings_ProgressStatus DEFAULT(0),
    PendingExpiresAt DATETIMEOFFSET NULL,
    CompletedAt DATETIMEOFFSET NULL,
    ContactRevealed BIT NOT NULL CONSTRAINT DF_Bookings_ContactRevealed DEFAULT(0),
    CancellationReason NVARCHAR(400) NULL,
    StatusUpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Bookings_StatusUpdatedAt DEFAULT(SYSDATETIMEOFFSET()),
    ProgressUpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Bookings_ProgressUpdatedAt DEFAULT(SYSDATETIMEOFFSET()),
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_Bookings_Trips FOREIGN KEY(TripId) REFERENCES dbo.Trips(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Bookings_Passenger FOREIGN KEY(PassengerId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Bookings_Pickup FOREIGN KEY(PickupRoutePointId) REFERENCES dbo.TripRoutePoints(Id),
    CONSTRAINT FK_Bookings_Dropoff FOREIGN KEY(DropoffRoutePointId) REFERENCES dbo.TripRoutePoints(Id)
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Trip_Passenger' AND object_id = OBJECT_ID('dbo.Bookings'))
    CREATE INDEX IX_Bookings_Trip_Passenger ON dbo.Bookings(TripId, PassengerId);
-- BookingSegmentAllocations
IF OBJECT_ID('dbo.BookingSegmentAllocations','U') IS NULL
BEGIN
CREATE TABLE dbo.BookingSegmentAllocations (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BookingSegmentAllocations PRIMARY KEY,
    BookingId UNIQUEIDENTIFIER NOT NULL,
    TripSegmentId UNIQUEIDENTIFIER NOT NULL,
    SeatsAllocated INT NOT NULL,
    CONSTRAINT FK_BookingSegmentAllocations_Bookings FOREIGN KEY(BookingId) REFERENCES dbo.Bookings(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BookingSegmentAllocations_TripSegments FOREIGN KEY(TripSegmentId) REFERENCES dbo.TripSegments(Id)
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BookingSegmentAllocations_Booking_Segment' AND object_id = OBJECT_ID('dbo.BookingSegmentAllocations'))
    CREATE UNIQUE INDEX UX_BookingSegmentAllocations_Booking_Segment ON dbo.BookingSegmentAllocations(BookingId, TripSegmentId);
-- Ratings
IF OBJECT_ID('dbo.Ratings','U') IS NULL
BEGIN
CREATE TABLE dbo.Ratings (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Ratings PRIMARY KEY,
    BookingId UNIQUEIDENTIFIER NOT NULL,
    FromUserId UNIQUEIDENTIFIER NOT NULL,
    ToUserId UNIQUEIDENTIFIER NOT NULL,
    Stars INT NOT NULL,
    Comment NVARCHAR(800) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_Ratings_Bookings FOREIGN KEY(BookingId) REFERENCES dbo.Bookings(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Ratings_FromUser FOREIGN KEY(FromUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Ratings_ToUser FOREIGN KEY(ToUserId) REFERENCES dbo.Users(Id)
);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Ratings_Booking_FromUser' AND object_id = OBJECT_ID('dbo.Ratings'))
    CREATE UNIQUE INDEX UX_Ratings_Booking_FromUser ON dbo.Ratings(BookingId, FromUserId);
