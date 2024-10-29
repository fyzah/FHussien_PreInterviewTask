using Microsoft.Extensions.Options;
using Dapper;
using System;
using FHussien_PreInterviewTask.Models;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Runtime.CompilerServices;
using FHussien_PreInterviewTask.Helper;

namespace FHussien_PreInterviewTask.Classes
{
    public class DataContext
    {
        private DbSettings _dbSettings;

        public DataContext(IOptions<DbSettings> dbSettings)
        {
            _dbSettings = dbSettings.Value;
        }

        public IDbConnection CreateConnection()
        {
            var connectionString = $"Server={_dbSettings.Server}; Database={_dbSettings.Database};{_dbSettings.Other};";
            return new SqlConnection(connectionString);
        }

        public async Task Init()
        {
            try
            {
                await _initDatabase();
                await _initTables();
                await _seedData();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task _initDatabase()
        {
            var connectionString = $"Server={_dbSettings.Server};{_dbSettings.Other};";
            using var connection = new SqlConnection(connectionString);
            var query = $"IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = '{_dbSettings.Database}') BEGIN CREATE DATABASE {_dbSettings.Database} END";
            await connection.ExecuteAsync(query);
        }

        public async Task _initTables()
        {
            using var connection = CreateConnection();
            await _initCompanies();
            await _initRoles();
            await _initUserInformations();
            await _initUsers();

            async Task _initCompanies()
            {
                var createCompaniesTableQuery = @"IF NOT EXISTS(SELECT object_id FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Companies]') and TYPE = 'U')
	                                                BEGIN 
		                                                CREATE TABLE [dbo].[Companies](
		                                                [Id] [int] IDENTITY(1,1) PRIMARY KEY,
		                                                [Name] [nvarchar](max) NOT NULL,
		                                                [Location] [nvarchar](max) NOT NULL);
	                                                END";
                await connection.ExecuteAsync(createCompaniesTableQuery);
            }

            async Task _initRoles()
            {
                var createRolesTableQuery = @"IF NOT EXISTS(SELECT object_id FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') and TYPE = 'U')
	                                                BEGIN 
		                                                CREATE TABLE [dbo].[Roles](
		                                                [Id] [int] IDENTITY(1,1) PRIMARY KEY,
		                                                [Name] [nvarchar](max) NOT NULL,
		                                                [Description] [nvarchar](max) NOT NULL)
	                                                END;";
                await connection.ExecuteAsync(createRolesTableQuery);
            }

            async Task _initUserInformations()
            {
                var createUserInformationsTableQuery = @"IF NOT EXISTS(SELECT object_id FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserInformations]') and TYPE = 'U')
	                                            BEGIN 
		                                            CREATE TABLE [dbo].[UserInformations](
		                                            [Id] [int] IDENTITY(1,1) PRIMARY KEY,
		                                            [FirstName] [nvarchar](max) NOT NULL,
		                                            [LastName] [nvarchar](max) NOT NULL,
		                                            [Birthdate] [date] NOT NULL)
	                                            END;";
                await connection.ExecuteAsync(createUserInformationsTableQuery);
            }


            async Task _initUsers()
            {
                var createUsersTableQuery = @"IF NOT EXISTS(SELECT object_id FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') and TYPE = 'U')
	                                                BEGIN 
		                                                CREATE TABLE [dbo].[Users](
		                                                [Id] [int] IDENTITY(1,1) NOT NULL,
		                                                [UserName] [nvarchar](max) NOT NULL,
		                                                [Password] [nvarchar](max) NOT NULL,
                                                        [Salt] [nvarchar](max),
		                                                [Email] [nvarchar](max) NOT NULL,
		                                                [RoleId] [int] FOREIGN KEY REFERENCES Roles(Id),
		                                                [CompanyId] [int] FOREIGN KEY REFERENCES Companies(Id),
		                                                [UserInformationId] [int] FOREIGN KEY REFERENCES UserInformations(Id))
	                                                END;";
                await connection.ExecuteAsync(createUsersTableQuery);
            }
        }
        public async Task _seedData()
        {
            using var connection = CreateConnection();
            await _seedCompanies();
            await _seedRoles();
            await _seedUsersAndUserInformations();

            async Task _seedCompanies()
            {
                var insertQuery = @"INSERT INTO [dbo].[Companies]
                                               ([Name]
                                               ,[Location])
                                    SELECT 'Sample Company', 'Sample Location'
                                    WHERE NOT EXISTS(SELECT * FROM [dbo].[Companies] WHERE [Name] = 'Sample Company' AND [Location] = 'Sample Location');

                                    INSERT INTO [dbo].[Companies]
                                               ([Name]
                                               ,[Location])
                                    SELECT 'Test Company', 'Test Location'
                                    WHERE NOT EXISTS(SELECT * FROM [dbo].[Companies] WHERE [Name] = 'Test Company' AND [Location] = 'Test Location');";

                await connection.ExecuteAsync(insertQuery);
            }

            async Task _seedRoles()
            {
                var insertQuery = @"INSERT INTO [dbo].[Roles]
                                               ([Name]
                                               ,[Description])
                                    SELECT 'Admin', 'Administrator'
                                    WHERE NOT EXISTS(SELECT * FROM [dbo].[Roles] WHERE [Name] = 'Admin' AND [Description] = 'Administrator');

                                    INSERT INTO [dbo].[Roles]
                                               ([Name]
                                               ,[Description])
                                    SELECT 'User', 'Flat User'
                                    WHERE NOT EXISTS(SELECT * FROM [dbo].[Roles] WHERE [Name] = 'User' AND [Description] = 'Flat User');";
                await connection.ExecuteAsync(insertQuery);

            }

            async Task _seedUsersAndUserInformations()
            {
                HashGenerator generator = new HashGenerator();

                var user1HashPwd = generator.HashPassword("jdoe123", out var user1Salt);
                var user2HashPwd = generator.HashPassword("jdelacruz123", out var user2Salt);

                var insertQuery = @"INSERT INTO [dbo].[UserInformations]
                                               ([FirstName]
                                               ,[LastName]
                                               ,[Birthdate])
                                    SELECT 'John', 'Doe', (CAST('10/28/1996' AS DATETIME))
                                    WHERE NOT EXISTS (SELECT * FROM [dbo].[UserInformations] WHERE [FirstName] = 'John' AND [LastName] = 'Doe');

                                    INSERT INTO [dbo].[Users]
                                               ([UserName]
                                               ,[Password]
                                               ,[Salt] 
                                               ,[Email]
                                               ,[RoleId]
                                               ,[CompanyId]
                                               ,[UserInformationId])
                                    SELECT 'jdoe', '" + user1HashPwd + "', '" + Convert.ToHexString(user1Salt) + "', 'john.doe@email.com', 1, 1, 1 WHERE NOT EXISTS (SELECT * FROM [dbo].[Users] WHERE [UserName] = 'jdoe' AND [Email] = 'john.doe@email.com')";

                var insertQuery2 = @"INSERT INTO [dbo].[UserInformations]
                                               ([FirstName]
                                               ,[LastName]
                                               ,[Birthdate])
                                    SELECT 'Juan', 'Dela Cruz', (CAST('10/28/1996' AS DATETIME))
                                    WHERE NOT EXISTS (SELECT * FROM [dbo].[UserInformations] WHERE [FirstName] = 'Juan' AND [LastName] = 'Dela Cruz');

                                    INSERT INTO [dbo].[Users]
                                               ([UserName]
                                               ,[Password]
                                               ,[Salt] 
                                               ,[Email]
                                               ,[RoleId]
                                               ,[CompanyId]
                                               ,[UserInformationId])
                                    SELECT 'jdelacruz', '" + user2HashPwd + "', '" + Convert.ToHexString(user2Salt) + "', 'juan.delacruz@email.com', 2, 1, 2 WHERE NOT EXISTS (SELECT * FROM [dbo].[Users] WHERE [UserName] = 'jdelacruz' AND [Email] = 'juan.delacruz@email.com')";

                var insertQ = insertQuery + " " + insertQuery2;
                await connection.ExecuteAsync(insertQ);
            }
        }

    }
}
