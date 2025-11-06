using GatewayService;
using GatewayService.HttpClients;
using GatewayService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.IdentityModel.Tokens;

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


builder.Services.AddSingleton<CircuitBreaker>();
builder.Services.AddSingleton<IRetryQueue, RetryQueue>();
builder.Services.AddHostedService<RetryQueue>(provider =>
    (RetryQueue)provider.GetRequiredService<IRetryQueue>());
// Register HTTP clients
builder.Services.AddHttpClient<IFlightClient, FlightClient>(client =>
{
    client.BaseAddress = new Uri("http://flights-service:8060");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IBonusClient, BonusClient>(client =>
{
    client.BaseAddress = new Uri("http://bonus-service:8050" );
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ITicketClient, TicketClient>(client =>
{
    client.BaseAddress = new Uri("http://tickets-service:8070");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register services
builder.Services.AddScoped<IGatewayService, GatewayService.Services.GatewayService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();
// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flight Booking System Gateway v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/manage/health", () => Results.Ok(new { status = "Healthy", service = "Gateway" }));
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
// Добавьте это для отладки
app.MapGet("/", () => "Gateway Service is running! Go to /swagger for API documentation");

Console.WriteLine("Application is starting...");
Console.WriteLine("Swagger will be available at: http://localhost:8080/swagger");

app.Run();