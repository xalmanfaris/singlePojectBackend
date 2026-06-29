using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace YuGo.Data
{
    public static class DbInitializer
    {
        public static void Initialize(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            // Skip database creation on Azure SQL as the database is pre-created and CREATE DATABASE is not supported in this manner.
            if (builder.DataSource != null && !builder.DataSource.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    builder.InitialCatalog = "master";
                    using (var masterConnection = new SqlConnection(builder.ConnectionString))
                    {
                        masterConnection.Execute($"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{databaseName}') CREATE DATABASE [{databaseName}]");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database creation skipped/failed: {ex.Message}");
                }
            }

            
            using (var connection = new SqlConnection(connectionString))
            {
                var sql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
                    BEGIN
                        CREATE TABLE Users (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            FullName NVARCHAR(100) NOT NULL,
                            Email NVARCHAR(100) NOT NULL UNIQUE,
                            PasswordHash NVARCHAR(255) NULL, -- Nullable for social login
                            Provider NVARCHAR(20) DEFAULT 'Local', -- 'Google', 'Apple', 'Local'
                            ExternalId NVARCHAR(255) NULL,
                            Role NVARCHAR(20) DEFAULT 'User',
                            IsActive BIT DEFAULT 1,
                            RefreshToken NVARCHAR(255) NULL,
                            RefreshTokenExpiryTime DATETIME NULL,
                            CreatedAt DATETIME DEFAULT GETDATE(),
                            UpdatedAt DATETIME NULL,
                            LastLoginAt DATETIME NULL
                        );
                    END
                    
                    -- Ensure RefreshToken columns exist if table was already created
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RefreshToken')
                    BEGIN
                        ALTER TABLE Users ADD RefreshToken NVARCHAR(255) NULL;
                        ALTER TABLE Users ADD RefreshTokenExpiryTime DATETIME NULL;
                    END

                    -- Ensure PasswordHash is nullable for social login
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PasswordHash')
                    BEGIN
                        ALTER TABLE Users ALTER COLUMN PasswordHash NVARCHAR(255) NULL;
                    END

                    -- Ensure Social Login columns exist
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Provider')
                    BEGIN
                        ALTER TABLE Users ADD Provider NVARCHAR(20) NOT NULL DEFAULT 'Local';
                    END

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ExternalId')
                    BEGIN
                        ALTER TABLE Users ADD ExternalId NVARCHAR(255) NULL;
                    END
                    
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ProfileImageUrl')
                    BEGIN
                        ALTER TABLE Users ADD ProfileImageUrl NVARCHAR(255) NULL;
                    END
                    
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserProfiles')
                    BEGIN
                        CREATE TABLE UserProfiles (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            UserId INT NOT NULL UNIQUE,
                            PhoneNumber NVARCHAR(15),
                            Country NVARCHAR(50),
                            TravelType NVARCHAR(20),
                            BudgetPreference NVARCHAR(20),
                            TravelStyle NVARCHAR(50),
                            PreferredTransport NVARCHAR(50),
                            ProfileImageUrl NVARCHAR(255),
                            CreatedAt DATETIME DEFAULT GETDATE(),
                            UpdatedAt DATETIME NULL,
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                        );

                        -- Migrate existing data from Users to UserProfiles ONLY IF the columns exist in Users
                        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PhoneNumber')
                        BEGIN
                            EXEC('INSERT INTO UserProfiles (UserId, PhoneNumber, Country, TravelType, BudgetPreference, TravelStyle, ProfileImageUrl)
                                  SELECT Id, PhoneNumber, Country, TravelType, BudgetPreference, TravelStyle, ProfileImageUrl
                                  FROM Users');
                        END
                    END
                    ELSE
                    BEGIN
                        -- Ensure all columns exist in UserProfiles if table was already created
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserProfiles') AND name = 'PhoneNumber')
                            ALTER TABLE UserProfiles ADD PhoneNumber NVARCHAR(15);
                        
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserProfiles') AND name = 'Country')
                            ALTER TABLE UserProfiles ADD Country NVARCHAR(50);
                            
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserProfiles') AND name = 'TravelType')
                            ALTER TABLE UserProfiles ADD TravelType NVARCHAR(20);
                            
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserProfiles') AND name = 'BudgetPreference')
                            ALTER TABLE UserProfiles ADD BudgetPreference NVARCHAR(20);
                            
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserProfiles') AND name = 'TravelStyle')
                            ALTER TABLE UserProfiles ADD TravelStyle NVARCHAR(50);
                            
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserProfiles') AND name = 'PreferredTransport')
                            ALTER TABLE UserProfiles ADD PreferredTransport NVARCHAR(50);
                            
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserProfiles') AND name = 'ProfileImageUrl')
                            ALTER TABLE UserProfiles ADD ProfileImageUrl NVARCHAR(255);
                    END

                    -- Remove extra columns from Users table if they still exist
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PhoneNumber')
                    BEGIN
                        ALTER TABLE Users DROP COLUMN PhoneNumber;
                        ALTER TABLE Users DROP COLUMN Country;
                        ALTER TABLE Users DROP COLUMN TravelType;
                        ALTER TABLE Users DROP COLUMN BudgetPreference;
                        ALTER TABLE Users DROP COLUMN TravelStyle;
                        ALTER TABLE Users DROP COLUMN ProfileImageUrl;
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserSessions')
                    BEGIN
                        CREATE TABLE UserSessions (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            UserId INT NOT NULL,
                            IPAddress NVARCHAR(50),
                            Device NVARCHAR(255),
                            Location NVARCHAR(100),
                            LoginAt DATETIME DEFAULT GETDATE(),
                            IsActive BIT DEFAULT 1,
                            RefreshToken NVARCHAR(255) NULL,
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                        );
                    END
                    
                    -- Explicitly drop redundant session columns from Users table
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastLoginIP')
                    BEGIN
                        ALTER TABLE Users DROP COLUMN LastLoginIP;
                    END

                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastLoginDevice')
                    BEGIN
                        ALTER TABLE Users DROP COLUMN LastLoginDevice;
                    END

                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastLoginLocation')
                    BEGIN
                        ALTER TABLE Users DROP COLUMN LastLoginLocation;
                    END
                    
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TripPlans')
                    BEGIN
                        CREATE TABLE TripPlans (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            UserId INT NOT NULL,
                            Destination NVARCHAR(255) NOT NULL,
                            StartingLocation NVARCHAR(255),
                            StartDate DATETIME,
                            EndDate DATETIME,
                            Travelers INT,
                            TripDataJson NVARCHAR(MAX),
                            AiPlanJson NVARCHAR(MAX),
                            CreatedAt DATETIME DEFAULT GETDATE(),
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                        );
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StartedTrips')
                    BEGIN
                        CREATE TABLE StartedTrips (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            TripId INT NOT NULL UNIQUE,
                            UserId INT NOT NULL,
                            CheckedItemsJson NVARCHAR(MAX),
                            CurrentLocationIndex INT DEFAULT 0,
                            UpdatedAt DATETIME DEFAULT GETDATE(),
                            FOREIGN KEY (TripId) REFERENCES TripPlans(Id) ON DELETE CASCADE,
                            FOREIGN KEY (UserId) REFERENCES Users(Id)
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StartedTrips') AND name = 'CurrentLocationIndex')
                        BEGIN
                            ALTER TABLE StartedTrips ADD CurrentLocationIndex INT DEFAULT 0;
                        END
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LostItems')
                    BEGIN
                        CREATE TABLE LostItems (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            TripId INT NOT NULL,
                            UserId INT NOT NULL,
                            ItemName NVARCHAR(255) NOT NULL,
                            PredictedLocation NVARCHAR(MAX),
                            Reason NVARCHAR(MAX),
                            CreatedAt DATETIME DEFAULT GETDATE(),
                            FOREIGN KEY (TripId) REFERENCES TripPlans(Id) ON DELETE CASCADE,
                            FOREIGN KEY (UserId) REFERENCES Users(Id)
                        );
                    END";

                connection.Execute(sql);

                // Ensure StartDate and EndDate columns exist in TripPlans
                connection.Execute(@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TripPlans') AND name = 'StartDate')
                    BEGIN
                        ALTER TABLE TripPlans ADD StartDate DATETIME NULL;
                    END
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TripPlans') AND name = 'EndDate')
                    BEGIN
                        ALTER TABLE TripPlans ADD EndDate DATETIME NULL;
                    END
                ");

                // Notifications Table
                connection.Execute(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
                    BEGIN
                        CREATE TABLE Notifications (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            UserId INT NOT NULL,
                            TripId INT NULL,
                            Destination NVARCHAR(255),
                            Message NVARCHAR(MAX),
                            Type NVARCHAR(50),
                            Timestamp DATETIME DEFAULT GETDATE(),
                            IsRead BIT DEFAULT 0,
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                        )
                    END
                ");

                // Migration: Make TripId nullable on existing Notifications table (for global broadcasts)
                connection.Execute(@"
                    -- Drop FK constraint on TripId if it exists (constraint name is auto-generated)
                    DECLARE @fkName NVARCHAR(255);
                    SELECT @fkName = fk.name
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.columns c ON fkc.parent_column_id = c.column_id AND fkc.parent_object_id = c.object_id
                    WHERE fk.parent_object_id = OBJECT_ID('Notifications') AND c.name = 'TripId';
                    IF @fkName IS NOT NULL
                        EXEC('ALTER TABLE Notifications DROP CONSTRAINT ' + @fkName);

                    -- Alter TripId to be nullable if it isn't already
                    IF EXISTS (
                        SELECT 1 FROM sys.columns
                        WHERE object_id = OBJECT_ID('Notifications') AND name = 'TripId' AND is_nullable = 0
                    )
                    BEGIN
                        ALTER TABLE Notifications ALTER COLUMN TripId INT NULL;
                    END
                ");

                // Seed default admin if it doesn't exist
                var adminExists = connection.ExecuteScalar<int>("SELECT COUNT(1) FROM Users WHERE Role = 'Admin'") > 0;
                if (!adminExists)
                {
                    var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                    connection.Execute(@"
                        INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt)
                        VALUES ('System Administrator', 'admin@yougo.com', @PasswordHash, 'Admin', 1, GETDATE())",
                        new { PasswordHash = adminPasswordHash });
                }
            }
        }
    }
}
