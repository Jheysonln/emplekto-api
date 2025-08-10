
using EmplekTo.Auth;
using EmplekTo.Data.Repositories;
using EmplekTo.Middleware;
using EmplekTo.Services;
using EmplekTo.Services.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ================================================================================================
// CONFIGURACIÓN DE LOGGING CON SERILOG
// ================================================================================================
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/emplekto-.log", rollingInterval: RollingInterval.Day);
});

// ================================================================================================
// CONFIGURACIÓN DE SERVICIOS
// ================================================================================================
var services = builder.Services;
var configuration = builder.Configuration;

// Configuraciones
services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
services.Configure<GoogleAuthSettings>(configuration.GetSection(GoogleAuthSettings.SectionName));

// HttpClient para servicios externos (Google OAuth)
services.AddHttpClient<IGoogleAuthService, GoogleAuthService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Emplekto-API/1.0");
});

// Repositorios
services.AddScoped<IAuthRepository, AuthRepository>();
services.AddScoped<IUserRepository, UserRepository>();

// Servicios de negocio
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<IJwtTokenService, JwtTokenService>();
services.AddScoped<IGoogleAuthService, GoogleAuthService>();

// ================================================================================================
// CONFIGURACIÓN DE AUTENTICACIÓN JWT
// ================================================================================================
var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("JWT Settings are not properly configured");
}

services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = builder.Environment.IsProduction(); // Solo HTTPS en producción
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
    };

    // Configuración para obtener token de cookies también
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Intentar obtener token del header Authorization primero
            var accessToken = context.Request.Headers["Authorization"]
                .FirstOrDefault()?.Split(" ").Last();

            // Si no hay token en header, intentar obtener de cookie
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = context.Request.Cookies["accessToken"];
            }

            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Add("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});

// ================================================================================================
// CONFIGURACIÓN DE AUTORIZACIÓN
// ================================================================================================
services.AddAuthorization(options =>
{
    // Políticas personalizadas para diferentes roles
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ModeratorOrAdmin", policy => policy.RequireRole("Admin", "Moderator"));
    options.AddPolicy("EmployerOrAdmin", policy => policy.RequireRole("Employer", "Admin"));
    options.AddPolicy("JobSeekerOnly", policy => policy.RequireRole("JobSeeker"));
});

// ================================================================================================
// CONFIGURACIÓN DE CORS
// ================================================================================================
services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "https://localhost:3000" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials() // Necesario para cookies
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// ================================================================================================
// CONTROLADORES Y SWAGGER
// ================================================================================================
services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Personalizar respuesta de validación automática
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            var response = new
            {
                Success = false,
                Message = "Datos inválidos",
                Errors = errors,
                Timestamp = DateTime.UtcNow
            };

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });

// Configuración de Swagger/OpenAPI
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Emplekto API",
        Version = "v1",
        Description = "API para plataforma de empleo similar a ComputraBajo/Indeed",
        Contact = new OpenApiContact
        {
            Name = "Emplekto Team",
            Email = "dev@emplekto.com"
        }
    });

    // Configuración para JWT en Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando Bearer scheme. Ejemplo: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    // Incluir comentarios XML
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ================================================================================================
// CONFIGURACIÓN DE CACHE Y OTROS SERVICIOS
// ================================================================================================
services.AddMemoryCache();
// Health Checks - versión más explícita
services.AddHealthChecks()
    .AddCheck("sql-server", () =>
    {
        try
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(
                configuration.GetConnectionString("DefaultConnection"));
            connection.Open();
            return HealthCheckResult.Healthy("SQL Server connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server connection failed", ex);
        }
    });
// ================================================================================================
// CONSTRUCCIÓN DE LA APLICACIÓN
// ================================================================================================
var app = builder.Build();

// ================================================================================================
// CONFIGURACIÓN DEL PIPELINE DE MIDDLEWARE
// ================================================================================================

// Logging de requests
app.UseSerilogRequestLogging();

// Swagger solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Emplekto API V1");
        options.RoutePrefix = "swagger";
    });
}

// Middleware personalizado para manejo de errores globales
app.UseMiddleware<GlobalExceptionMiddleware>();

// Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// HTTPS Redirection (solo en producción)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// CORS - Debe ir antes de Authentication
app.UseCors("AllowFrontend");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Health checks
app.MapHealthChecks("/health");

// API Controllers
app.MapControllers();

// Endpoint de información de la API
app.MapGet("/", () => new
{
    Name = "Emplekto API",
    Version = "1.0.0",
    Environment = app.Environment.EnvironmentName,
    Timestamp = DateTime.UtcNow
});

// ================================================================================================
// LOGGING DE INICIO Y EJECUCIÓN
// ================================================================================================
try
{
    Log.Information("Starting Emplekto API");
    Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
    Log.Information("JWT Issuer: {Issuer}", jwtSettings.Issuer);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}