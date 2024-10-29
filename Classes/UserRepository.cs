﻿using FHussien_PreInterviewTask.Interfaces;
using FHussien_PreInterviewTask.Models;
using FHussien_PreInterviewTask.Helper;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using Microsoft.Extensions.Options;
using System.Text;
using LoginRequest = FHussien_PreInterviewTask.Models.LoginRequest;

namespace FHussien_PreInterviewTask.Classes
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;
        private TokenService _tokenService;
        private DbSettings _dbSettings;
        private HashGenerator generator = new HashGenerator();

        public UserRepository(IOptions<DbSettings> dbSettings, TokenService tokenService)
        {
            _dbSettings = dbSettings.Value;
            _tokenService = tokenService;
            _connectionString = $"Server={_dbSettings.Server}; Database={_dbSettings.Database};{_dbSettings.Other};";
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);


        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            using var connection = CreateConnection();
            var userQueryByEmail = @"SELECT *
                                      FROM [dbo].[Users] as u
                                      INNER JOIN [dbo].[Roles] as r ON u.RoleId = r.Id
                                      INNER JOIN [dbo].[Companies] as comp ON u.CompanyId = comp.Id
                                      INNER JOIN [dbo].[UserInformations] as uinfo ON u.UserInformationId = uinfo.Id
                                      WHERE Email = '" + request.Email + "' AND UserName = '" + request.UserName + "'";
            //var user = await connection.QueryFirstOrDefaultAsync<User>(userQueryByEmail);

            var user = await connection.QueryAsync<User, Role, Company, UserInformation, Result>(userQueryByEmail, (user, role, company, userInfo) =>
            {
                user.Role = role;
                user.Company = company;
                user.UserInformation = userInfo;
                return new Result
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Salt = user.Salt,
                    Password = user.Password,
                    Role = user.Role.Name,
                    Company = user.Company.Name + " - " + user.Company.Location,
                    FullName = user.UserInformation.FirstName + ' ' + user.UserInformation.LastName,
                    Birthdate = user.UserInformation.Birthdate
                };
            }, splitOn: "Id,Id,Id");

            var u = user.FirstOrDefault();

            if (u == null)
            {
                throw new Exception("User does not exist.");
            }

            var verifyPassword = generator.VerifyPassword(request.Password, u.Password, Convert.FromHexString(u.Salt!));
            if (!verifyPassword)
            {
                throw new UnauthorizedAccessException();
            }
            // Generate token
            var token = _tokenService.Create(u);

            return new AuthResponse { UserId = u.Id, Username = u.UserName, Token = token };
        }

        public async Task<int> AddUserAsync(Result user)
        {
            using var connection = CreateConnection();
            var isUserExistingQuery = @"SELECT COUNT(*) 
                        FROM [dbo].[Users] as u
                        WHERE UserName = '" + user.UserName + "' OR Email = '" + user.Email + "'";
            var isUserExisting = await connection.QueryFirstOrDefaultAsync<int>(isUserExistingQuery);

            if (isUserExisting > 0)
            {
                throw new Exception("User already exisiting.");
            }

            // insert user information first
            var insertUserInformationQuery = @"INSERT INTO [dbo].[UserInformations] ([FirstName],[LastName],[Birthdate])
                            VALUES ('" + user.FullName.Split(" ")[0] + "','" + user.FullName.Split(" ")[1] + "',(CAST('" + user.Birthdate.ToString("MM/dd/yyyy") + "' AS DATETIME)));SELECT CAST(SCOPE_IDENTITY() as int);";


            var userInfoId = await connection.ExecuteScalarAsync<int>(insertUserInformationQuery);

            // get the Company Id

            var companySelectIdQuery = @"SELECT * FROM [dbo].[Companies] 
                                        WHERE [Name] = '" + user.Company.Split("-")[0].Trim() + "'";

            var companyId = (await connection.QueryFirstOrDefaultAsync<Company>(companySelectIdQuery)).Id;

            // get the Role Id 
            var roleSelectIdQuery = @"SELECT * FROM [dbo].[Roles]  
                                        WHERE [Name] = '" + user.Role + "'";

            var role = await connection.QueryFirstOrDefaultAsync<Role>(roleSelectIdQuery);

            if (role == null)
            {
                throw new Exception("Role not existing. Kindly choose either Admin or User");
            }

            var hashedPassword = generator.HashPassword(user.Password, out var salt);
            var insertUserQuery = @"INSERT INTO [dbo].[Users] ([UserName] ,[Password] ,[Email] ,[RoleId] ,[CompanyId] ,[UserInformationId])
                                    VALUES ('" + user.UserName + "' ,'" + hashedPassword + "' ,'" + user.Email + "' , " + role?.Id + " ," + companyId + ", " + userInfoId + ");SELECT CAST(SCOPE_IDENTITY() as int);";

            var userId = await connection.ExecuteScalarAsync<int>(insertUserQuery);
            return userId;
        }

        public async Task<int> DeleteUserAsync(int id)
        {
            using var connection = CreateConnection();
            var isUserExistingQuery = @"SELECT *
                        FROM [dbo].[Users] as u
                        WHERE Id = " + id;
            var isUserExisting = await connection.QueryFirstOrDefaultAsync<User>(isUserExistingQuery);

            if (isUserExisting == null)
            {
                throw new Exception("User is not existing");
            }

            var deleteUserQuery = @"DELETE FROM [dbo].[Users] 
                                        WHERE Id = " + id + " ;";
            var deleteUser = await connection.ExecuteAsync(deleteUserQuery);

            var deleteUserInfoQuery = @"DELETE FROM [dbo].[UserInformations] 
                                        WHERE Id = " + isUserExisting.UserInformationId + " ;";
            var deleteUserInfo = await connection.ExecuteAsync(deleteUserInfoQuery);

            return deleteUser;
        }

        public async Task<IEnumerable<Result>> GetAllUsersAsync()
        {
            var query = @"SELECT *
                      FROM [dbo].[Users] as u
                      INNER JOIN [dbo].[Roles] as r ON u.RoleId = r.Id
                      INNER JOIN [dbo].[Companies] as comp ON u.CompanyId = comp.Id
                      INNER JOIN [dbo].[UserInformations] as uinfo ON u.UserInformationId = uinfo.Id";
            using var connection = CreateConnection();

            var users = await connection.QueryAsync<User, Role, Company, UserInformation, Result>(query, (user, role, company, userInfo) =>
            {
                user.Role = role;
                user.Company = company;
                user.UserInformation = userInfo;
                return new Result
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Password = user.Password,
                    Role = user.Role.Name,
                    Company = user.Company.Name + " - " + user.Company.Location,
                    FullName = user.UserInformation.FirstName + ' ' + user.UserInformation.LastName,
                    Birthdate = user.UserInformation.Birthdate
                };
            }, splitOn: "Id,Id,Id");


            return users;
        }

        public async Task<Result?> GetUserByIdAsync(int id)
        {
            var query = @"SELECT *
                      FROM [dbo].[Users] as u
                      INNER JOIN [dbo].[Roles] as r ON u.RoleId = r.Id
                      INNER JOIN [dbo].[Companies] as comp ON u.CompanyId = comp.Id
                      INNER JOIN [dbo].[UserInformations] as uinfo ON u.UserInformationId = uinfo.Id
                      WHERE u.[Id] = " + id;
            using var connection = CreateConnection();

            var user = await connection.QueryAsync<User, Role, Company, UserInformation, Result>(query, (user, role, company, userInfo) =>
            {
                user.Role = role;
                user.Company = company;
                user.UserInformation = userInfo;
                return new Result
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Password = user.Password,
                    Role = user.Role.Name,
                    Company = user.Company.Name + " - " + user.Company.Location,
                    FullName = user.UserInformation.FirstName + ' ' + user.UserInformation.LastName,
                    Birthdate = user.UserInformation.Birthdate
                };
            }, splitOn: "Id,Id,Id");


            return user.FirstOrDefault() == null ? throw new Exception("User not existing") : user.FirstOrDefault();
        }

        public async Task<int> UpdateUserAsync(Request user)
        {
            using var connection = CreateConnection();
            var isUserExistingQuery = @"SELECT *
                                FROM [dbo].[Users] as u
                                WHERE Id = " + user.Id + " AND UserName = '" + user.UserName + "' AND Email = '" + user.Email + "'";
            var isUserExisting = await connection.QueryFirstOrDefaultAsync<User>(isUserExistingQuery);

            if (isUserExisting == null)
            {
                throw new Exception("User is not existing");
            }

            // get the Role Id 
            var roleSelectIdQuery = @"SELECT * FROM [dbo].[Roles]  
                                        WHERE [Name] = '" + user.Role + "'";

            var role = await connection.QueryFirstOrDefaultAsync<Role>(roleSelectIdQuery);

            if (role == null)
            {
                throw new Exception("Role not existing. Kindly choose either Admin or User");
            }

            var updateUserQuery = @"UPDATE [dbo].[Users]
                                   SET [UserName] = '" + user.UserName + "' ,[Password] = '" + user.Password + "' ,[Email] = '" + user.Email + "',[RoleId] = " + role.Id + " WHERE Id = " + user.Id;

            var update = await connection.ExecuteAsync(updateUserQuery);

            var updateUserInfoQuery = @"UPDATE [dbo].[UserInformations]
                                        SET [FirstName] = '" + user.FullName.Split(" ")[0] + "' ,[LastName] = '" + user.FullName.Split(" ")[1] + "' ,[Birthdate] = (CAST('" + user.Birthdate.ToString("MM/dd/yyyy") + "' AS DATETIME)) WHERE Id = " + isUserExisting.UserInformationId;

            var updateUInfo = await connection.ExecuteAsync(updateUserInfoQuery);

            return update;
        }
    }
}