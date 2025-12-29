-- Phone verification flag
IF COL_LENGTH('dbo.Users', 'PhoneVerified') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PhoneVerified BIT NOT NULL CONSTRAINT DF_Users_PhoneVerified DEFAULT(0);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_PhoneNumber' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE INDEX IX_Users_PhoneNumber ON dbo.Users(PhoneNumber);

-- SMS OTP tokens
IF OBJECT_ID('dbo.SmsOtpTokens','U') IS NULL
BEGIN
CREATE TABLE dbo.SmsOtpTokens (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SmsOtpTokens PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    PhoneNumber NVARCHAR(64) NOT NULL,
    TokenHash NVARCHAR(256) NOT NULL,
    ExpiresAt DATETIMEOFFSET NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    UsedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_SmsOtpTokens_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id)
);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmsOtpTokens_UserId' AND object_id = OBJECT_ID('dbo.SmsOtpTokens'))
    CREATE INDEX IX_SmsOtpTokens_UserId ON dbo.SmsOtpTokens(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmsOtpTokens_PhoneNumber' AND object_id = OBJECT_ID('dbo.SmsOtpTokens'))
    CREATE INDEX IX_SmsOtpTokens_PhoneNumber ON dbo.SmsOtpTokens(PhoneNumber);
