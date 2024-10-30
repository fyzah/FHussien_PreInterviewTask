using FHussien_PreInterviewTask.Classes;
using FHussien_PreInterviewTask.Helper;
using FHussien_PreInterviewTask.Interfaces;
using FHussien_PreInterviewTask.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

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
builder.Services.AddHttpContextAccessor();
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

builder.Services.AddRateLimiter(limiterOptions =>
{
    limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiterOptions.AddPolicy("jwt", partitioner: httpContext => {
        var accessToken = httpContext.GetTokenAsync("access_token").Result;

        return !string.IsNullOrEmpty(accessToken) ?
            RateLimitPartition.GetFixedWindowLimiter(accessToken, options => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            })
            : RateLimitPartition.GetFixedWindowLimiter("Anon", options => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

var app = builder.Build();

// ensure database and tables exist
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    await context.Init();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/users", async (IUserRepository repo) => await repo.GetAllUsersAsync()).RequireRateLimiting("jwt").RequireAuthorization();
app.MapGet("/users/{id}", async (int id, IUserRepository repo) => {
    try
    {
        return Results.Ok(await repo.GetUserByIdAsync(id));
    }
    catch (Exception ex)
    {
        return Results.NotFound("Failed: " + ex.Message);
    }
}).RequireRateLimiting("jwt").RequireAuthorization("AdminPolicy");
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

}).RequireRateLimiting("jwt").RequireAuthorization("AdminPolicy");
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
}).RequireRateLimiting("jwt").RequireAuthorization("AdminPolicy");
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
}).RequireRateLimiting("jwt").RequireAuthorization("AdminPolicy");

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
}).RequireRateLimiting("jwt");

app.Run();

