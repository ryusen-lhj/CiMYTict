/*
    CiMYTict - cancellation request workflow
    Manual fallback for existing databases. The application also applies this
    idempotent patch automatically at startup after EnsureCreated().
    BookingStatus is stored as an integer; CancellationRequested uses value 4.
*/
SELECT DB_NAME() AS CurrentDatabase;

IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL
    THROW 51000, 'dbo.Bookings was not found. Select the database used by CiMYTict.', 1;

IF COL_LENGTH(N'dbo.Bookings', N'CancellationReason') IS NULL
    ALTER TABLE dbo.Bookings ADD CancellationReason NVARCHAR(500) NULL;

IF COL_LENGTH(N'dbo.Bookings', N'CancellationRequestedAt') IS NULL
    ALTER TABLE dbo.Bookings ADD CancellationRequestedAt DATETIME2 NULL;

IF COL_LENGTH(N'dbo.Bookings', N'CancellationDecision') IS NULL
    ALTER TABLE dbo.Bookings ADD CancellationDecision NVARCHAR(20) NULL;

SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo'
  AND TABLE_NAME = N'Bookings'
  AND COLUMN_NAME IN (N'CancellationReason', N'CancellationRequestedAt', N'CancellationDecision')
ORDER BY COLUMN_NAME;
