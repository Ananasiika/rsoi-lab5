using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TicketService.Database;
using TicketService.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Flight Booking System Gateway",
        Version = "v1",
        Description = "Gateway API for Flight Booking System"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please enter JWT with Bearer into field",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8081/realms/flight-booking";
        options.Audience = "flight-booking-client";
        options.RequireHttpsMetadata = false; // Только для разработки
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "http://localhost:8081/realms/flight-booking",
            ValidAudience = "flight-booking-client"
        };
    });

builder.Services.AddAuthorization();


// Database configuration
builder.Services.AddDbContext<TicketDatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")),
                ServiceLifetime.Transient, ServiceLifetime.Transient);

// Services
builder.Services.AddScoped<ITicketService, TicketService.Services.TicketService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

app.UseAuthorization();
app.UseAuthentication();
app.MapControllers();
app.MapGet("/manage/health", () => Results.Ok(new { status = "Healthy", service = "ticket" }));
app.MapPost("/api/v1/authorize", async (LoginRequest request) =>
{
    using var httpClient = new HttpClient();
    var tokenRequest = new List<KeyValuePair<string, string>>
    {
        new("client_id", "flight-booking-client"),
        new("client_secret", "flight-booking-secret-2025-rsoi-lab5"), // Получите из Keycloak
        new("username", request.Email),
        new("password", request.Password),
        new("grant_type", "password"),
        new("scope", "openid profile email")
    };

    var response = await httpClient.PostAsync(
        "http://localhost:8081/realms/flight-booking/protocol/openid-connect/token",
        new FormUrlEncodedContent(tokenRequest));

    if (response.IsSuccessStatusCode)
    {
        var content = await response.Content.ReadAsStringAsync();
        return Results.Ok(content);
    }
    
    return Results.Unauthorized();
});
app.Run();