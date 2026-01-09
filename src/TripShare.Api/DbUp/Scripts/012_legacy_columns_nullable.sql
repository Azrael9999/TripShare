/*
Purpose: relax legacy non-null columns that are no longer written by the current EF model.
- Alters legacy columns to allow NULLs to prevent insert failures if defaults are missing.
*/

DECLARE @sql nvarchar(max);

-- Trips.SeatsAvailable
SELECT @sql =
    'ALTER TABLE dbo.Trips ALTER COLUMN SeatsAvailable ' +
    CASE
        WHEN DATA_TYPE IN ('decimal','numeric') THEN DATA_TYPE + '(' + CAST(NUMERIC_PRECISION AS nvarchar(10)) + ',' + CAST(NUMERIC_SCALE AS nvarchar(10)) + ')'
        WHEN DATA_TYPE IN ('varchar','nvarchar','char','nchar') THEN DATA_TYPE + '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'max' ELSE CAST(CHARACTER_MAXIMUM_LENGTH AS nvarchar(10)) END + ')'
        WHEN DATA_TYPE IN ('datetime2','datetimeoffset','time') THEN DATA_TYPE + '(' + CAST(DATETIME_PRECISION AS nvarchar(10)) + ')'
        ELSE DATA_TYPE
    END + ' NULL'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'Trips'
  AND COLUMN_NAME = 'SeatsAvailable'
  AND IS_NULLABLE = 'NO';
IF @sql IS NOT NULL EXEC(@sql);

-- Trips.DepartureTime
SET @sql = NULL;
SELECT @sql =
    'ALTER TABLE dbo.Trips ALTER COLUMN DepartureTime ' +
    CASE
        WHEN DATA_TYPE IN ('decimal','numeric') THEN DATA_TYPE + '(' + CAST(NUMERIC_PRECISION AS nvarchar(10)) + ',' + CAST(NUMERIC_SCALE AS nvarchar(10)) + ')'
        WHEN DATA_TYPE IN ('varchar','nvarchar','char','nchar') THEN DATA_TYPE + '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'max' ELSE CAST(CHARACTER_MAXIMUM_LENGTH AS nvarchar(10)) END + ')'
        WHEN DATA_TYPE IN ('datetime2','datetimeoffset','time') THEN DATA_TYPE + '(' + CAST(DATETIME_PRECISION AS nvarchar(10)) + ')'
        ELSE DATA_TYPE
    END + ' NULL'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'Trips'
  AND COLUMN_NAME = 'DepartureTime'
  AND IS_NULLABLE = 'NO';
IF @sql IS NOT NULL EXEC(@sql);

-- TripSegments.PriceAmount
SET @sql = NULL;
SELECT @sql =
    'ALTER TABLE dbo.TripSegments ALTER COLUMN PriceAmount ' +
    CASE
        WHEN DATA_TYPE IN ('decimal','numeric') THEN DATA_TYPE + '(' + CAST(NUMERIC_PRECISION AS nvarchar(10)) + ',' + CAST(NUMERIC_SCALE AS nvarchar(10)) + ')'
        WHEN DATA_TYPE IN ('varchar','nvarchar','char','nchar') THEN DATA_TYPE + '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'max' ELSE CAST(CHARACTER_MAXIMUM_LENGTH AS nvarchar(10)) END + ')'
        WHEN DATA_TYPE IN ('datetime2','datetimeoffset','time') THEN DATA_TYPE + '(' + CAST(DATETIME_PRECISION AS nvarchar(10)) + ')'
        ELSE DATA_TYPE
    END + ' NULL'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'TripSegments'
  AND COLUMN_NAME = 'PriceAmount'
  AND IS_NULLABLE = 'NO';
IF @sql IS NOT NULL EXEC(@sql);
