/*
Password reset tokens.
*/

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PasswordResetTokens')
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(256) NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UsedAt DATETIMEOFFSET NULL,
        CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens(UserId);
    CREATE UNIQUE INDEX UX_PasswordResetTokens_TokenHash ON dbo.PasswordResetTokens(TokenHash);
END
