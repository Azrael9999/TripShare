/*
Allow larger site setting values for branding assets.
*/

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SiteSettings')
BEGIN
    ALTER TABLE dbo.SiteSettings ALTER COLUMN Value NVARCHAR(MAX) NOT NULL;
END
