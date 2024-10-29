using FHussien_PreInterviewTask.Classes;
using FHussien_PreInterviewTask.Helper;
using FHussien_PreInterviewTask.Interfaces;
using FHussien_PreInterviewTask.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGenWithAuth();

builder.Services.Configure<DbSettings>(builder.Configuration.GetSection("DbSettings"));
// Add connection string 

// Add Dapper configuration
builder.Services.AddSingleton<DataContext>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", p => p.RequireRole("Admin"));
    options.AddPolicy("UserPolicy", p => p.RequireRole("User"));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };
    });

var app = builder.Build();

// ensure database and tables exist
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    await context.Init();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/users", async (IUserRepository repo) => await repo.GetAllUsersAsync()).RequireAuthorization();
app.MapGet("/users/{id}", async (int id, IUserRepository repo) => {
    try
    {
        return Results.Ok(await repo.GetUserByIdAsync(id));
    }
    catch (Exception ex)
    {
        return Results.NotFound("Failed: " + ex.Message);
    }
}).RequireAuthorization("AdminPolicy"); 
app.MapPost("/users", async (Result user, IUserRepository repo) =>
{
    try
    {
        var id = await repo.AddUserAsync(user);
        return Results.Ok(value: "Success: The User was added with Id " + id + ".");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(error: "Failed: " + ex.Message);
    }

}).RequireAuthorization("AdminPolicy");
app.MapPut("/users/{id}", async (int id, Request user, IUserRepository repo) =>
{
    try
    {
        user.Id = id;
        await repo.UpdateUserAsync(user);
        return Results.Ok(value: "Success: The User with Id " + id + ".");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(error: "Failed: " + ex.Message);
    }
}).RequireAuthorization("AdminPolicy");
app.MapDelete("/users/{id}", async (int id, IUserRepository repo) =>
{
    try
    {
        await repo.DeleteUserAsync(id);
        return Results.Ok(value: "Success: User deleted.");
    }
    catch (Exception)
    {
        return Results.BadRequest(error: "Failed: User does not exist.");
    }
}).RequireAuthorization("AdminPolicy");

app.MapPost("/login", async (LoginRequest request, IUserRepository repo) =>
{
    try
    {
        var res = await repo.LoginAsync(request);
        return Results.Ok(value: res);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(error: ex.Message);
    }
});

app.UseAuthentication();
app.UseAuthorization(); 
app.Run();

