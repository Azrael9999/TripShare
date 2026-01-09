/*
Purpose: prevent legacy non-null columns from blocking inserts after schema changes.
- Adds defaults for legacy columns that are no longer written by the current EF model.
*/

-- Legacy Trips.DepartureTime column (superseded by DepartureTimeUtc)
IF COL_LENGTH('dbo.Trips', 'DepartureTime') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Trips')
          AND c.name = 'DepartureTime'
    )
    BEGIN
        ALTER TABLE dbo.Trips ADD CONSTRAINT DF_Trips_DepartureTime DEFAULT(SYSDATETIMEOFFSET()) FOR DepartureTime;
    END
END

-- TripRoutePoints.CreatedAt should default to now for legacy rows
IF COL_LENGTH('dbo.TripRoutePoints', 'CreatedAt') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.TripRoutePoints')
          AND c.name = 'CreatedAt'
    )
    BEGIN
        ALTER TABLE dbo.TripRoutePoints ADD CONSTRAINT DF_TripRoutePoints_CreatedAt DEFAULT(SYSDATETIMEOFFSET()) FOR CreatedAt;
    END
END

-- Legacy TripSegments.PriceAmount column (superseded by Price)
IF COL_LENGTH('dbo.TripSegments', 'PriceAmount') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.TripSegments')
          AND c.name = 'PriceAmount'
    )
    BEGIN
        ALTER TABLE dbo.TripSegments ADD CONSTRAINT DF_TripSegments_PriceAmount DEFAULT(0) FOR PriceAmount;
    END
END
