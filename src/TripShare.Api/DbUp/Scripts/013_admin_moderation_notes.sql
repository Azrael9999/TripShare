IF COL_LENGTH('dbo.Users', 'SuspensionNote') IS NULL
    ALTER TABLE dbo.Users ADD SuspensionNote NVARCHAR(400) NULL;

IF COL_LENGTH('dbo.Users', 'SuspensionUpdatedAt') IS NULL
    ALTER TABLE dbo.Users ADD SuspensionUpdatedAt DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Trips', 'VisibilityNote') IS NULL
    ALTER TABLE dbo.Trips ADD VisibilityNote NVARCHAR(400) NULL;

IF COL_LENGTH('dbo.Trips', 'VisibilityUpdatedAt') IS NULL
    ALTER TABLE dbo.Trips ADD VisibilityUpdatedAt DATETIMEOFFSET NULL;
