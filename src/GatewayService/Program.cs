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
        options.Authority = "http://keycloak:8080/realms/flight-booking";
        options.Audience = "flight-booking-client";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "http://keycloak:8080/realms/flight-booking"
        };
        
        // Добавьте для отладки
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated successfully");
                return Task.CompletedTask;
            }
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
    client.BaseAddress = new Uri("http://flight_service:8060");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IBonusClient, BonusClient>(client =>
{
    client.BaseAddress = new Uri("http://bonus_service:8050" );
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ITicketClient, TicketClient>(client =>
{
    client.BaseAddress = new Uri("http://ticket_service:8070");
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

// Добавьте это для отладки
app.MapGet("/", () => "Gateway Service is running! Go to /swagger for API documentation");

Console.WriteLine("Application is starting...");
Console.WriteLine("Swagger will be available at: http://localhost:8080/swagger");

app.Run();