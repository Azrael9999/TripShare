/*
Purpose: align Trip and Booking schema with current domain models.
- Adds missing pricing and schedule columns referenced by background jobs and services.
- Backfills values from legacy columns when available to preserve existing data.
*/

-- Booking price + cancellation columns
IF COL_LENGTH('dbo.Bookings', 'PriceTotal') IS NULL
BEGIN
    ALTER TABLE dbo.Bookings ADD PriceTotal DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Bookings_PriceTotal DEFAULT(0);
END

IF COL_LENGTH('dbo.Bookings', 'PriceTotal') IS NOT NULL AND COL_LENGTH('dbo.Bookings', 'CalculatedPrice') IS NOT NULL
BEGIN
    EXEC('UPDATE dbo.Bookings SET PriceTotal = CalculatedPrice WHERE PriceTotal = 0');
END

IF COL_LENGTH('dbo.Bookings', 'CancellationReason') IS NULL
BEGIN
    ALTER TABLE dbo.Bookings ADD CancellationReason NVARCHAR(400) NULL;
END
IF COL_LENGTH('dbo.Bookings', 'CancellationReason') IS NOT NULL AND COL_LENGTH('dbo.Bookings', 'CancelReason') IS NOT NULL
BEGIN
    EXEC('UPDATE dbo.Bookings SET CancellationReason = CancelReason WHERE CancellationReason IS NULL AND CancelReason IS NOT NULL');
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.Bookings')
      AND c.name = 'Status'
      AND t.name IN ('nvarchar', 'varchar', 'nchar', 'char')
)
BEGIN
    ALTER TABLE dbo.Bookings ADD StatusInt INT NOT NULL CONSTRAINT DF_Bookings_StatusInt DEFAULT(0);
    EXEC('UPDATE dbo.Bookings SET StatusInt = COALESCE(TRY_CONVERT(INT, Status),
        CASE UPPER(Status)
            WHEN ''PENDING'' THEN 0
            WHEN ''ACCEPTED'' THEN 1
            WHEN ''REJECTED'' THEN 2
            WHEN ''CANCELLED'' THEN 3
            WHEN ''COMPLETED'' THEN 4
            ELSE 0
        END)');
    ALTER TABLE dbo.Bookings DROP COLUMN Status;
    EXEC sp_rename ''dbo.Bookings.StatusInt'', ''Status'', ''COLUMN'';
END

-- Trip pricing and schedule columns
IF COL_LENGTH('dbo.Trips', 'BaseCurrencyRate') IS NULL
BEGIN
    ALTER TABLE dbo.Trips ADD BaseCurrencyRate DECIMAL(18, 6) NOT NULL CONSTRAINT DF_Trips_BaseCurrencyRate DEFAULT(1);
END

IF COL_LENGTH('dbo.Trips', 'DefaultPricePerSeat') IS NULL
BEGIN
    ALTER TABLE dbo.Trips ADD DefaultPricePerSeat DECIMAL(18, 2) NULL;
END

IF COL_LENGTH('dbo.Trips', 'DepartureTimeUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Trips ADD DepartureTimeUtc DATETIMEOFFSET NOT NULL CONSTRAINT DF_Trips_DepartureTimeUtc DEFAULT(SYSDATETIMEOFFSET());
END

IF COL_LENGTH('dbo.Trips', 'DepartureTimeUtc') IS NOT NULL AND COL_LENGTH('dbo.Trips', 'DepartureTime') IS NOT NULL
BEGIN
    EXEC('UPDATE dbo.Trips SET DepartureTimeUtc = DepartureTime WHERE DepartureTime IS NOT NULL');
END

IF COL_LENGTH('dbo.Trips', 'SeatsTotal') IS NULL
BEGIN
    ALTER TABLE dbo.Trips ADD SeatsTotal INT NOT NULL CONSTRAINT DF_Trips_SeatsTotal DEFAULT(0);
END

IF COL_LENGTH('dbo.Trips', 'SeatsTotal') IS NOT NULL AND COL_LENGTH('dbo.Trips', 'SeatsAvailable') IS NOT NULL
BEGIN
    EXEC('UPDATE dbo.Trips SET SeatsTotal = SeatsAvailable WHERE SeatsTotal = 0 AND SeatsAvailable IS NOT NULL');
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.Trips')
      AND c.name = 'Status'
      AND t.name IN ('nvarchar', 'varchar', 'nchar', 'char')
)
BEGIN
    ALTER TABLE dbo.Trips ADD StatusInt INT NOT NULL CONSTRAINT DF_Trips_StatusInt DEFAULT(0);
    EXEC('UPDATE dbo.Trips SET StatusInt = COALESCE(TRY_CONVERT(INT, Status),
        CASE UPPER(Status)
            WHEN ''SCHEDULED'' THEN 0
            WHEN ''ENROUTE'' THEN 1
            WHEN ''ARRIVED'' THEN 2
            WHEN ''INPROGRESS'' THEN 3
            WHEN ''COMPLETED'' THEN 4
            WHEN ''CANCELLED'' THEN 5
            ELSE 0
        END)');
    ALTER TABLE dbo.Trips DROP COLUMN Status;
    EXEC sp_rename ''dbo.Trips.StatusInt'', ''Status'', ''COLUMN'';
END

-- Trip segment pricing and capacity columns
IF COL_LENGTH('dbo.TripSegments', 'Price') IS NULL
BEGIN
    ALTER TABLE dbo.TripSegments ADD Price DECIMAL(18, 2) NOT NULL CONSTRAINT DF_TripSegments_Price DEFAULT(0);
END

IF COL_LENGTH('dbo.TripSegments', 'Price') IS NOT NULL AND COL_LENGTH('dbo.TripSegments', 'PriceAmount') IS NOT NULL
BEGIN
    EXEC('UPDATE dbo.TripSegments SET Price = PriceAmount WHERE Price = 0');
END

IF COL_LENGTH('dbo.TripSegments', 'BookedSeats') IS NULL
BEGIN
    ALTER TABLE dbo.TripSegments ADD BookedSeats INT NOT NULL CONSTRAINT DF_TripSegments_BookedSeats DEFAULT(0);
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.TripRoutePoints')
      AND c.name = 'Type'
      AND t.name IN ('nvarchar', 'varchar', 'nchar', 'char')
)
BEGIN
    ALTER TABLE dbo.TripRoutePoints ADD TypeInt INT NOT NULL CONSTRAINT DF_TripRoutePoints_TypeInt DEFAULT(0);
    EXEC('UPDATE dbo.TripRoutePoints SET TypeInt = COALESCE(TRY_CONVERT(INT, Type),
        CASE UPPER(Type)
            WHEN ''START'' THEN 0
            WHEN ''STOP'' THEN 1
            WHEN ''END'' THEN 2
            ELSE 0
        END)');
    ALTER TABLE dbo.TripRoutePoints DROP COLUMN Type;
    EXEC sp_rename ''dbo.TripRoutePoints.TypeInt'', ''Type'', ''COLUMN'';
END
