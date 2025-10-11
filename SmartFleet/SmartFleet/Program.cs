using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartFleet.Authorization;
using SmartFleet.Data;
using SmartFleet.Models;
using SmartFleet.Hubs;
using SmartFleet.Options;
using SmartFleet.Services;
using SmartFleet.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartFleet", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by a space and your token.",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    options.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

builder.Services.AddMemoryCache();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = jwtSection.Get<JwtOptions>()
                ?? throw new InvalidOperationException("JWT settings not configured correctly.");

builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.PostConfigure<MqttOptions>(options =>
{
    var host = Environment.GetEnvironmentVariable("MQTT_HOST");
    if (!string.IsNullOrWhiteSpace(host))
    {
        options.Host = host;
    }

    var portValue = Environment.GetEnvironmentVariable("MQTT_PORT");
    if (int.TryParse(portValue, out var port))
    {
        options.Port = port;
    }

    var username = Environment.GetEnvironmentVariable("MQTT_USERNAME");
    if (!string.IsNullOrWhiteSpace(username))
    {
        options.Username = username;
    }

    var password = Environment.GetEnvironmentVariable("MQTT_PASSWORD");
    if (!string.IsNullOrWhiteSpace(password))
    {
        options.Password = password;
    }
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DashboardAccess", policy =>
        policy.RequireRole(UserRole.Coordinator.ToString(), UserRole.Admin.ToString()));

    options.AddPolicy("MapsAccess", policy =>
        policy.RequireRole(UserRole.Driver.ToString(), UserRole.Admin.ToString()));

    options.AddPolicy("RoleAdminAccess", policy =>
        policy.RequireRole(UserRole.Admin.ToString()));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IChatNotifier, SignalRChatNotifier>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IVehicleTelemetryService, VehicleTelemetryService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITelemetryMessageProcessor, TelemetryMessageProcessor>();
builder.Services.AddScoped<ITelemetryDeadLetterSink, TelemetryDeadLetterSink>();
builder.Services.AddSingleton<ITelemetryIngestionMonitor, TelemetryIngestionMonitor>();
builder.Services.AddSingleton<ITelemetryAnalyticsQueue, TelemetryAnalyticsQueue>();
builder.Services.AddHostedService<MqttTelemetryHostedService>();
builder.Services.AddHostedService<TelemetryAnalyticsWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.MapGet("/health", async (ApplicationDbContext context, ITelemetryIngestionMonitor monitor, CancellationToken cancellationToken) =>
    {
        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return Results.StatusCode(503);
        }

        var telemetry = monitor.GetSnapshot();
        return Results.Ok(new
        {
            status = "Healthy",
            telemetry
        });
    })
    .WithOpenApi();

app.Run();




