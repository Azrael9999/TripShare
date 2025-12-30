/*
 TripShare schema v3
 - Live trip + booking status, driver location, pickup/drop pins
*/

-- Trip live status timestamps
IF COL_LENGTH('dbo.Trips', 'StatusUpdatedAt') IS NULL
    ALTER TABLE dbo.Trips ADD StatusUpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Trips_StatusUpdatedAt DEFAULT(SYSDATETIMEOFFSET());

IF COL_LENGTH('dbo.Trips', 'StartedAtUtc') IS NULL
    ALTER TABLE dbo.Trips ADD StartedAtUtc DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Trips', 'ArrivedAtUtc') IS NULL
    ALTER TABLE dbo.Trips ADD ArrivedAtUtc DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Trips', 'CompletedAtUtc') IS NULL
    ALTER TABLE dbo.Trips ADD CompletedAtUtc DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Trips', 'CancelledAtUtc') IS NULL
    ALTER TABLE dbo.Trips ADD CancelledAtUtc DATETIMEOFFSET NULL;

-- Trip live location
IF COL_LENGTH('dbo.Trips', 'CurrentLat') IS NULL
    ALTER TABLE dbo.Trips ADD CurrentLat FLOAT NULL;

IF COL_LENGTH('dbo.Trips', 'CurrentLng') IS NULL
    ALTER TABLE dbo.Trips ADD CurrentLng FLOAT NULL;

IF COL_LENGTH('dbo.Trips', 'CurrentHeading') IS NULL
    ALTER TABLE dbo.Trips ADD CurrentHeading FLOAT NULL;

IF COL_LENGTH('dbo.Trips', 'LocationUpdatedAt') IS NULL
    ALTER TABLE dbo.Trips ADD LocationUpdatedAt DATETIMEOFFSET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Trips_Status_UpdatedAt' AND object_id = OBJECT_ID('dbo.Trips'))
    CREATE INDEX IX_Trips_Status_UpdatedAt ON dbo.Trips(Status, UpdatedAt);

-- Booking pickup/dropoff pins
IF COL_LENGTH('dbo.Bookings', 'PickupLat') IS NULL
    ALTER TABLE dbo.Bookings ADD PickupLat FLOAT NOT NULL CONSTRAINT DF_Bookings_PickupLat DEFAULT(0);
IF COL_LENGTH('dbo.Bookings', 'PickupLng') IS NULL
    ALTER TABLE dbo.Bookings ADD PickupLng FLOAT NOT NULL CONSTRAINT DF_Bookings_PickupLng DEFAULT(0);
IF COL_LENGTH('dbo.Bookings', 'DropoffLat') IS NULL
    ALTER TABLE dbo.Bookings ADD DropoffLat FLOAT NOT NULL CONSTRAINT DF_Bookings_DropoffLat DEFAULT(0);
IF COL_LENGTH('dbo.Bookings', 'DropoffLng') IS NULL
    ALTER TABLE dbo.Bookings ADD DropoffLng FLOAT NOT NULL CONSTRAINT DF_Bookings_DropoffLng DEFAULT(0);
IF COL_LENGTH('dbo.Bookings', 'PickupPlaceName') IS NULL
    ALTER TABLE dbo.Bookings ADD PickupPlaceName NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Bookings', 'DropoffPlaceName') IS NULL
    ALTER TABLE dbo.Bookings ADD DropoffPlaceName NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Bookings', 'PickupPlaceId') IS NULL
    ALTER TABLE dbo.Bookings ADD PickupPlaceId NVARCHAR(128) NULL;
IF COL_LENGTH('dbo.Bookings', 'DropoffPlaceId') IS NULL
    ALTER TABLE dbo.Bookings ADD DropoffPlaceId NVARCHAR(128) NULL;

-- Booking workflow timestamps
IF COL_LENGTH('dbo.Bookings', 'StatusUpdatedAt') IS NULL
    ALTER TABLE dbo.Bookings ADD StatusUpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Bookings_StatusUpdatedAt DEFAULT(SYSDATETIMEOFFSET());
IF COL_LENGTH('dbo.Bookings', 'ProgressStatus') IS NULL
    ALTER TABLE dbo.Bookings ADD ProgressStatus INT NOT NULL CONSTRAINT DF_Bookings_ProgressStatus DEFAULT(0);
IF COL_LENGTH('dbo.Bookings', 'ProgressUpdatedAt') IS NULL
    ALTER TABLE dbo.Bookings ADD ProgressUpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Bookings_ProgressUpdatedAt DEFAULT(SYSDATETIMEOFFSET());

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Trip_Status' AND object_id = OBJECT_ID('dbo.Bookings'))
    CREATE INDEX IX_Bookings_Trip_Status ON dbo.Bookings(TripId, Status, ProgressStatus);
