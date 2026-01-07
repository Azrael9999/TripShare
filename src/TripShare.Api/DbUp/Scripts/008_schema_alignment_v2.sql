/*
Purpose: follow-up schema alignment for legacy databases where earlier scripts already ran.
- Ensures TripSegments pricing/capacity columns exist for older installs.
- Converts legacy string enum columns to int-backed enums to match domain models.
*/

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

-- Convert legacy string enums to int-backed enums
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
    EXEC sp_rename 'dbo.Trips.StatusInt', 'Status', 'COLUMN';
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
    EXEC sp_rename 'dbo.Bookings.StatusInt', 'Status', 'COLUMN';
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
    EXEC sp_rename 'dbo.TripRoutePoints.TypeInt', 'Type', 'COLUMN';
END
