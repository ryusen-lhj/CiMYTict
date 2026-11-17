/* CiMYTict auditorium format schema update. Safe to run more than once. */
SELECT DB_NAME() AS CurrentDatabase;
IF OBJECT_ID(N'dbo.CinemaHalls', N'U') IS NULL
    THROW 51001, 'dbo.CinemaHalls was not found. Select the database used by CiMYTict.', 1;
IF COL_LENGTH(N'dbo.CinemaHalls', N'HallType') IS NULL
    ALTER TABLE dbo.CinemaHalls ADD HallType INT NOT NULL CONSTRAINT DF_CinemaHalls_HallType DEFAULT (0);
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'CinemaHalls' AND COLUMN_NAME = N'HallType';
