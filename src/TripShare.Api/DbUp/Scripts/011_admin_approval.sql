/*
 Add admin approval gate for non-default admin accounts.
*/

IF COL_LENGTH('dbo.Users', 'AdminApproved') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD AdminApproved BIT NOT NULL CONSTRAINT DF_Users_AdminApproved DEFAULT(0);
END

UPDATE dbo.Users
SET AdminApproved = 1
WHERE Email = 'admin@tripshare.local';
