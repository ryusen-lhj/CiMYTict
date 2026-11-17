/*
    CiMYTict - cancellation request workflow
    Run this script once against the existing CiMYTict SQL Server database.
    BookingStatus is stored as an integer; CancellationRequested uses value 4.
*/
IF COL_LENGTH('dbo.Bookings', 'CancellationReason') IS NULL
    ALTER TABLE dbo.Bookings ADD CancellationReason NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.Bookings', 'CancellationRequestedAt') IS NULL
    ALTER TABLE dbo.Bookings ADD CancellationRequestedAt DATETIME2 NULL;

IF COL_LENGTH('dbo.Bookings', 'CancellationDecision') IS NULL
    ALTER TABLE dbo.Bookings ADD CancellationDecision NVARCHAR(20) NULL;
