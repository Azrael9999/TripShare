/*
 Default admin account (idempotent).
*/

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = 'admin@tripshare.local')
BEGIN
    INSERT INTO dbo.Users (
        Id,
        Email,
        EmailVerified,
        DisplayName,
        PhotoUrl,
        PhoneNumber,
        PhoneVerified,
        AuthProvider,
        ProviderUserId,
        IsDriver,
        IsSuspended,
        Role,
        CreatedAt,
        LastLoginAt,
        PasswordHash,
        PasswordSalt
    )
    VALUES (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'admin@tripshare.local',
        1,
        'Default Admin',
        NULL,
        '+94000000000',
        1,
        'password',
        'admin@tripshare.local',
        0,
        0,
        'superadmin',
        SYSDATETIMEOFFSET(),
        NULL,
        'fGx5yBUPxt/JJ1pjBChDHTn3XPA/WdgvMosfRtG5iDI=',
        'kRzj2wsbG8iTHhAeiJfCtA=='
    );
END
ELSE
BEGIN
    UPDATE dbo.Users
    SET Role = 'superadmin',
        AuthProvider = 'password',
        ProviderUserId = 'admin@tripshare.local',
        PasswordHash = 'fGx5yBUPxt/JJ1pjBChDHTn3XPA/WdgvMosfRtG5iDI=',
        PasswordSalt = 'kRzj2wsbG8iTHhAeiJfCtA==',
        EmailVerified = 1,
        PhoneVerified = 1,
        PhoneNumber = COALESCE(PhoneNumber, '+94000000000')
    WHERE Email = 'admin@tripshare.local';
END
