using System.IO;
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
using SmartFleet.Services.Telemetry;

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

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        if (allowedCorsOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = jwtSection.Get<JwtOptions>()
                ?? throw new InvalidOperationException("JWT settings not configured correctly.");

builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
builder.Services.Configure<TelemetryIngestionOptions>(builder.Configuration.GetSection("TelemetryIngestion"));

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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var path = context.HttpContext.Request.Path;
                    if (path.StartsWithSegments("/hubs/vehicles", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWithSegments("/hubs/chat", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = accessToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DashboardAccess", policy =>
        policy.RequireRole(UserRole.Coordinator.ToString(), UserRole.Admin.ToString()));

    options.AddPolicy("MapsAccess", policy =>
        policy.RequireRole(UserRole.Driver.ToString(), UserRole.Coordinator.ToString(), UserRole.Admin.ToString()));

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
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHostedService<VehicleTelemetryIngestionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrWhiteSpace(webRootPath))
{
    webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    app.Environment.WebRootPath = webRootPath;
}

var uploadsPath = Path.Combine(webRootPath, "uploads", "users");
try
{
    Directory.CreateDirectory(uploadsPath);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Failed to ensure profile image upload directory at {UploadsPath}", uploadsPath);
    throw;
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AppCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<VehicleHub>("/hubs/vehicles");

app.MapGet("/health", async (ApplicationDbContext context, CancellationToken cancellationToken) =>
    {
        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new { status = "Healthy" })
            : Results.StatusCode(503);
    })
    .WithOpenApi();

app.Run();




