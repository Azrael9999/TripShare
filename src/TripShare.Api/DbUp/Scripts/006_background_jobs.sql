/*
Purpose: durable background job storage for notifications and other queued work.
*/

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BackgroundJobs')
BEGIN
    CREATE TABLE dbo.BackgroundJobs
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BackgroundJobs PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Payload NVARCHAR(4000) NOT NULL,
        Status INT NOT NULL,
        Attempts INT NOT NULL,
        MaxAttempts INT NOT NULL,
        RunAfter DATETIMEOFFSET NOT NULL,
        LastError NVARCHAR(1000) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE INDEX IX_BackgroundJobs_Status_RunAfter ON dbo.BackgroundJobs(Status, RunAfter, Attempts);
END

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SiteSettings')
BEGIN
    ALTER TABLE dbo.SiteSettings ALTER COLUMN Value NVARCHAR(4000) NOT NULL;
END
