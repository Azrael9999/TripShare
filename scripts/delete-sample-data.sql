/*
Remove sample data inserted by scripts/sample-data.sql
*/

DELETE FROM dbo.Messages WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb6';
DELETE FROM dbo.MessageThreads WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb5';
DELETE FROM dbo.TripShareLinks WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb7';
DELETE FROM dbo.SafetyIncidents WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb8';
DELETE FROM dbo.IdentityVerificationRequests WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb9';
DELETE FROM dbo.Reports WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3';
DELETE FROM dbo.UserBlocks WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2';
DELETE FROM dbo.Notifications WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1';
DELETE FROM dbo.Ratings WHERE Id = '99999999-9999-9999-9999-999999999990';
DELETE FROM dbo.BookingSegmentAllocations WHERE Id IN ('88888888-8888-8888-8888-888888888880', '88888888-8888-8888-8888-888888888881');
DELETE FROM dbo.Bookings WHERE Id = '44444444-4444-4444-4444-444444444444';
DELETE FROM dbo.TripSegments WHERE Id IN ('77777777-7777-7777-7777-777777777770', '77777777-7777-7777-7777-777777777771');
DELETE FROM dbo.TripRoutePoints WHERE Id IN (
    '66666666-6666-6666-6666-666666666660',
    '66666666-6666-6666-6666-666666666661',
    '66666666-6666-6666-6666-666666666662'
);
DELETE FROM dbo.Trips WHERE Id = '33333333-3333-3333-3333-333333333333';
DELETE FROM dbo.Vehicles WHERE Id = '55555555-5555-5555-5555-555555555555';
DELETE FROM dbo.EmergencyContacts WHERE Id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4';
DELETE FROM dbo.Users WHERE Id IN (
    '11111111-1111-1111-1111-111111111111',
    '22222222-2222-2222-2222-222222222222'
);
