/*
Purpose: harden account lifecycle and safety surface
- Adds password/auth lifecycle columns to Users
- Adds emergency contacts, in-app messaging, trip share links, safety incidents, and identity verification tables
*/

-- Password auth + lifecycle fields
IF COL_LENGTH('dbo.Users', 'PasswordHash') IS NULL
    ALTER TABLE dbo.Users ADD PasswordHash NVARCHAR(400) NULL;

IF COL_LENGTH('dbo.Users', 'PasswordSalt') IS NULL
    ALTER TABLE dbo.Users ADD PasswordSalt NVARCHAR(200) NULL;

IF COL_LENGTH('dbo.Users', 'IdentityVerified') IS NULL
    ALTER TABLE dbo.Users ADD IdentityVerified BIT NOT NULL CONSTRAINT DF_Users_IdentityVerified DEFAULT 0;

IF COL_LENGTH('dbo.Users', 'IdentityVerifiedAt') IS NULL
    ALTER TABLE dbo.Users ADD IdentityVerifiedAt DATETIMEOFFSET NULL;

IF COL_LENGTH('dbo.Users', 'IsDeleted') IS NULL
    ALTER TABLE dbo.Users ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT 0;

IF COL_LENGTH('dbo.Users', 'DeletedAt') IS NULL
    ALTER TABLE dbo.Users ADD DeletedAt DATETIMEOFFSET NULL;

-- Emergency contacts
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmergencyContacts')
BEGIN
    CREATE TABLE dbo.EmergencyContacts
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmergencyContacts PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        PhoneNumber NVARCHAR(64) NULL,
        Email NVARCHAR(320) NULL,
        ShareLiveTripsByDefault BIT NOT NULL CONSTRAINT DF_EmergencyContacts_Share DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT FK_EmergencyContacts_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
    CREATE UNIQUE INDEX IX_EmergencyContacts_UserId ON dbo.EmergencyContacts(UserId);
END

-- Messaging threads
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MessageThreads')
BEGIN
    CREATE TABLE dbo.MessageThreads
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MessageThreads PRIMARY KEY,
        TripId UNIQUEIDENTIFIER NULL,
        BookingId UNIQUEIDENTIFIER NULL,
        ParticipantAId UNIQUEIDENTIFIER NOT NULL,
        ParticipantBId UNIQUEIDENTIFIER NOT NULL,
        IsClosed BIT NOT NULL CONSTRAINT DF_MessageThreads_IsClosed DEFAULT 0,
        ClosedReason NVARCHAR(400) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT FK_MessageThreads_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips(Id),
        CONSTRAINT FK_MessageThreads_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(Id),
        CONSTRAINT FK_MessageThreads_ParticipantA FOREIGN KEY (ParticipantAId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_MessageThreads_ParticipantB FOREIGN KEY (ParticipantBId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_MessageThreads_BookingTrip ON dbo.MessageThreads(BookingId, TripId);
    CREATE INDEX IX_MessageThreads_Participants ON dbo.MessageThreads(ParticipantAId, ParticipantBId);
END

-- Messages
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Messages')
BEGIN
    CREATE TABLE dbo.Messages
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Messages PRIMARY KEY,
        ThreadId UNIQUEIDENTIFIER NOT NULL,
        SenderId UNIQUEIDENTIFIER NOT NULL,
        Body NVARCHAR(2000) NOT NULL,
        IsSystem BIT NOT NULL CONSTRAINT DF_Messages_IsSystem DEFAULT 0,
        SentAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT FK_Messages_Threads FOREIGN KEY (ThreadId) REFERENCES dbo.MessageThreads(Id),
        CONSTRAINT FK_Messages_Sender FOREIGN KEY (SenderId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_Messages_Thread ON dbo.Messages(ThreadId, SentAt);
END

-- Trip share links
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TripShareLinks')
BEGIN
    CREATE TABLE dbo.TripShareLinks
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TripShareLinks PRIMARY KEY,
        TripId UNIQUEIDENTIFIER NOT NULL,
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        EmergencyContactId UNIQUEIDENTIFIER NULL,
        Token NVARCHAR(120) NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        RevokedAt DATETIMEOFFSET NULL,
        CONSTRAINT FK_TripShareLinks_Trips FOREIGN KEY (TripId) REFERENCES dbo.Trips(Id),
        CONSTRAINT FK_TripShareLinks_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_TripShareLinks_Contacts FOREIGN KEY (EmergencyContactId) REFERENCES dbo.EmergencyContacts(Id)
    );
    CREATE INDEX IX_TripShareLinks_TripId ON dbo.TripShareLinks(TripId);
    CREATE UNIQUE INDEX IX_TripShareLinks_Token ON dbo.TripShareLinks(Token);
END

-- Safety incidents
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SafetyIncidents')
BEGIN
    CREATE TABLE dbo.SafetyIncidents
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SafetyIncidents PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        TripId UNIQUEIDENTIFIER NULL,
        BookingId UNIQUEIDENTIFIER NULL,
        Type INT NOT NULL,
        Summary NVARCHAR(800) NOT NULL,
        Status INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL,
        EscalatedAt DATETIMEOFFSET NULL,
        ResolvedAt DATETIMEOFFSET NULL,
        ResolvedByUserId UNIQUEIDENTIFIER NULL,
        ResolutionNote NVARCHAR(600) NULL,
        CONSTRAINT FK_SafetyIncidents_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_SafetyIncidents_Trip FOREIGN KEY (TripId) REFERENCES dbo.Trips(Id),
        CONSTRAINT FK_SafetyIncidents_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(Id),
        CONSTRAINT FK_SafetyIncidents_Resolver FOREIGN KEY (ResolvedByUserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_SafetyIncidents_Status ON dbo.SafetyIncidents(Status, CreatedAt);
END

-- Identity verification requests
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'IdentityVerificationRequests')
BEGIN
    CREATE TABLE dbo.IdentityVerificationRequests
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_IdentityVerificationRequests PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        DocumentType NVARCHAR(80) NOT NULL,
        DocumentReference NVARCHAR(200) NOT NULL,
        Status INT NOT NULL,
        ReviewerNote NVARCHAR(600) NULL,
        ReviewedByUserId UNIQUEIDENTIFIER NULL,
        SubmittedAt DATETIMEOFFSET NOT NULL,
        ReviewedAt DATETIMEOFFSET NULL,
        KycProvider NVARCHAR(120) NULL,
        KycReference NVARCHAR(200) NULL,
        CONSTRAINT FK_IdentityVerificationRequests_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_IdentityVerificationRequests_Reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_IdentityVerificationRequests_UserId ON dbo.IdentityVerificationRequests(UserId);
    CREATE INDEX IX_IdentityVerificationRequests_Status ON dbo.IdentityVerificationRequests(Status, SubmittedAt);
END
