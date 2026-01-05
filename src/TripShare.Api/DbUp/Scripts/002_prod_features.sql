/*
 TripShare schema v2 (production features)
 - Trip lifecycle and booking workflow maturity
 - Notifications, Reports, User blocks, Vehicle
 - Booking expiry + contact reveal
*/

-- Trip: add workflow fields
IF COL_LENGTH('dbo.Trips', 'Status') IS NULL
    ALTER TABLE dbo.Trips ADD Status INT NOT NULL CONSTRAINT DF_Trips_Status DEFAULT(0);

IF COL_LENGTH('dbo.Trips', 'InstantBook') IS NULL
    ALTER TABLE dbo.Trips ADD InstantBook BIT NOT NULL CONSTRAINT DF_Trips_InstantBook DEFAULT(0);

IF COL_LENGTH('dbo.Trips', 'BookingCutoffMinutes') IS NULL
    ALTER TABLE dbo.Trips ADD BookingCutoffMinutes INT NOT NULL CONSTRAINT DF_Trips_BookingCutoff DEFAULT(60);

IF COL_LENGTH('dbo.Trips', 'PendingBookingExpiryMinutes') IS NULL
    ALTER TABLE dbo.Trips ADD PendingBookingExpiryMinutes INT NOT NULL CONSTRAINT DF_Trips_PendingExpiry DEFAULT(30);

-- Booking: expiry + completion + contact
IF COL_LENGTH('dbo.Bookings', 'PendingExpiresAt') IS NULL
    ALTER TABLE dbo.Bookings ADD PendingExpiresAt DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Bookings', 'CompletedAt') IS NULL
    ALTER TABLE dbo.Bookings ADD CompletedAt DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Bookings', 'ContactRevealed') IS NULL
    ALTER TABLE dbo.Bookings ADD ContactRevealed BIT NOT NULL CONSTRAINT DF_Bookings_ContactRevealed DEFAULT(0);

-- Vehicles (one per user)
IF OBJECT_ID('dbo.Vehicles','U') IS NULL
BEGIN
    CREATE TABLE dbo.Vehicles(
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        OwnerUserId UNIQUEIDENTIFIER NOT NULL,
        Make NVARCHAR(80) NOT NULL,
        Model NVARCHAR(80) NOT NULL,
        Color NVARCHAR(40) NOT NULL,
        PlateNumber NVARCHAR(32) NULL,
        Seats INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE UNIQUE INDEX IX_Vehicles_OwnerUserId ON dbo.Vehicles(OwnerUserId);
    ALTER TABLE dbo.Vehicles ADD CONSTRAINT FK_Vehicles_Users_OwnerUserId FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE;
END

-- Notifications
IF OBJECT_ID('dbo.Notifications','U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications(
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Type INT NOT NULL,
        Title NVARCHAR(140) NOT NULL,
        Body NVARCHAR(600) NOT NULL,
        TripId UNIQUEIDENTIFIER NULL,
        BookingId UNIQUEIDENTIFIER NULL,
        IsRead BIT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        ReadAt DATETIMEOFFSET NULL
    );
    CREATE INDEX IX_Notifications_User_IsRead_CreatedAt ON dbo.Notifications(UserId, IsRead, CreatedAt DESC);
    ALTER TABLE dbo.Notifications ADD CONSTRAINT FK_Notifications_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE;
END

-- User blocks
IF OBJECT_ID('dbo.UserBlocks','U') IS NULL
BEGIN
    CREATE TABLE dbo.UserBlocks(
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        BlockerUserId UNIQUEIDENTIFIER NOT NULL,
        BlockedUserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE UNIQUE INDEX IX_UserBlocks_Blocker_Blocked ON dbo.UserBlocks(BlockerUserId, BlockedUserId);
    ALTER TABLE dbo.UserBlocks ADD CONSTRAINT FK_UserBlocks_Blocker FOREIGN KEY (BlockerUserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE;
    -- Avoid multiple cascade paths by keeping only the blocker side cascading.
    ALTER TABLE dbo.UserBlocks ADD CONSTRAINT FK_UserBlocks_Blocked FOREIGN KEY (BlockedUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
END

-- Reports
IF OBJECT_ID('dbo.Reports','U') IS NULL
BEGIN
    CREATE TABLE dbo.Reports(
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ReporterUserId UNIQUEIDENTIFIER NOT NULL,
        TargetUserId UNIQUEIDENTIFIER NULL,
        TripId UNIQUEIDENTIFIER NULL,
        BookingId UNIQUEIDENTIFIER NULL,
        TargetType INT NOT NULL,
        Reason NVARCHAR(120) NOT NULL,
        Details NVARCHAR(2000) NULL,
        Status INT NOT NULL,
        AdminNote NVARCHAR(2000) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE INDEX IX_Reports_Status_CreatedAt ON dbo.Reports(Status, CreatedAt DESC);
    ALTER TABLE dbo.Reports ADD CONSTRAINT FK_Reports_Reporter FOREIGN KEY (ReporterUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
END
