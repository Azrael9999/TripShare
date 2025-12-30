/*
 Driver verification + site settings
 - Adds driver verification fields
 - Adds SiteSettings key/value table with default flag DriverVerificationRequired = false
*/

-- Driver verification fields
IF COL_LENGTH('dbo.Users', 'DriverVerified') IS NULL
    ALTER TABLE dbo.Users ADD DriverVerified BIT NOT NULL CONSTRAINT DF_Users_DriverVerified DEFAULT(0);

IF COL_LENGTH('dbo.Users', 'DriverVerifiedAt') IS NULL
    ALTER TABLE dbo.Users ADD DriverVerifiedAt DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Users', 'DriverVerificationNote') IS NULL
    ALTER TABLE dbo.Users ADD DriverVerificationNote NVARCHAR(400) NULL;

-- Site settings key/value
IF OBJECT_ID('dbo.SiteSettings','U') IS NULL
BEGIN
    CREATE TABLE dbo.SiteSettings(
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Key] NVARCHAR(120) NOT NULL,
        [Value] NVARCHAR(400) NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE UNIQUE INDEX IX_SiteSettings_Key ON dbo.SiteSettings([Key]);
END

-- Seed: driver verification required flag (default false)
IF NOT EXISTS (SELECT 1 FROM dbo.SiteSettings WHERE [Key] = 'DriverVerificationRequired')
BEGIN
    INSERT INTO dbo.SiteSettings(Id, [Key], [Value], UpdatedAt)
    VALUES (NEWID(), 'DriverVerificationRequired', 'false', SYSDATETIMEOFFSET());
END
