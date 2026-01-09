/*
 Promote default admin to superadmin for admin management UI access.
*/

UPDATE dbo.Users
SET Role = 'superadmin',
    AdminApproved = 1
WHERE Email = 'admin@tripshare.local';
