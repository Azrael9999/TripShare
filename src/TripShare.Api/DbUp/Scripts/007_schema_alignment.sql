/*
Purpose: align Trip and Booking schema with current domain models.
- Adds missing pricing and schedule columns referenced by background jobs and services.
- Backfills values from legacy columns when available to preserve existing data.
*/

-- Booking price + cancellation columns
IF COL_LENGTH('dbo.Bookings', 'PriceTotal') IS NULL
BEGIN
    ALTER TABLE dbo.Bookings ADD PriceTotal DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Bookings_PriceTotal DEFAULT(0);
    IF COL_LENGTH('dbo.Bookings', 'CalculatedPrice') IS NOT NULL
        UPDATE dbo.Bookings SET PriceTotal = CalculatedPrice;
END

IF COL_LENGTH('dbo.Bookings', 'CancellationReason') IS NULL
BEGIN
    ALTER TABLE dbo.Bookings ADD CancellationReason NVARCHAR(400) NULL;
END
IF COL_LENGTH('dbo.Bookings', 'CancelReason') IS NOT NULL
    UPDATE dbo.Bookings SET CancellationReason = CancelReason WHERE CancellationReason IS NULL AND CancelReason IS NOT NULL;

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
    IF COL_LENGTH('dbo.Trips', 'DepartureTime') IS NOT NULL
        UPDATE dbo.Trips SET DepartureTimeUtc = DepartureTime;
END

IF COL_LENGTH('dbo.Trips', 'SeatsTotal') IS NULL
BEGIN
    ALTER TABLE dbo.Trips ADD SeatsTotal INT NOT NULL CONSTRAINT DF_Trips_SeatsTotal DEFAULT(0);
    IF COL_LENGTH('dbo.Trips', 'SeatsAvailable') IS NOT NULL
        UPDATE dbo.Trips SET SeatsTotal = SeatsAvailable;
END
