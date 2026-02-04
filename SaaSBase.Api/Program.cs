using SaaSBase.Api.Infrastructure;
using SaaSBase.Application;
using SaaSBase.Application.Implementations;
using SaaSBase.Application.Services;
using SaaSBase.Infrastructure.Persistence;
using SaaSBase.Infrastructure.Services;
using Asp.Versioning;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SendGrid;
using Serilog;
using StackExchange.Redis;
using System;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

// Npgsql timestamp behavior: allow non-UTC DateTimeOffset for backward-compat
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IBackgroundContextService, BackgroundContextService>();
builder.Services.AddScoped<IBackgroundOperationService, BackgroundOperationService>();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Caching - Redis only (removed IMemoryCache to prevent memory leaks)
var redisConnectionString = builder.Configuration.GetSection("Redis").GetValue<string>("Configuration") 
    ?? builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});

// Register IConnectionMultiplexer for pattern-based cache invalidation
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

builder.Services.AddResponseCaching(); // Required for [ResponseCache] attributes with VaryByQueryKeys
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IPerformanceService, PerformanceService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(SaaSBase.Application.MappingProfiles.AutoMapperProfile).Assembly);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Application Services
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IMenuService, MenuService>(); // ✅ Register MenuService
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<IFileService, FileService>();

// Master Data Services
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IPositionService, PositionService>();

// Email and SMS services
builder.Services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsService>();

// Import/Export service
builder.Services.AddScoped<IImportExportService, ImportExportService>();

// Demo Data Service
builder.Services.AddScoped<IDemoDataService, DemoDataService>();
builder.Services.AddScoped<ISystemService, SystemService>();

// SendGrid configuration
var sendGridApiKey = builder.Configuration["SendGrid:ApiKey"];
if (!string.IsNullOrEmpty(sendGridApiKey))
{
    builder.Services.AddSingleton<ISendGridClient>(provider => new SendGridClient(sendGridApiKey));
}
else
{
    // Fallback for development - log warning
    Log.Warning("SendGrid API key not configured. Email functionality will be disabled.");
    builder.Services.AddSingleton<ISendGridClient>(provider => new SendGridClient("dummy-key"));
}

// Background Services
builder.Services.AddHostedService<SaaSBase.Api.BackgroundServices.FileCleanupService>();
builder.Services.AddHostedService<SaaSBase.Api.BackgroundServices.SessionCleanupService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SaaSBase API", Version = "v1" });
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
}).AddMvc();

// CORS - Configure based on environment
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Development: Allow all origins for local development
        options.AddPolicy("Default", policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    }
    else
    {
        // Production: Restrict to configured origins
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { builder.Configuration["AppSettings:BaseUrl"] ?? "https://localhost:4200" };
        
        options.AddPolicy("Default", policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    }
});

// ProblemDetails
builder.Services.AddProblemDetails(options =>
{
    options.IncludeExceptionDetails = (ctx, ex) => builder.Environment.IsDevelopment();
    
    // Map UnauthorizedAccessException to 401 status code
    options.MapToStatusCode<UnauthorizedAccessException>(StatusCodes.Status401Unauthorized);
    
    // Customize ProblemDetails to include exception message in detail field
    // This callback is called before writing the ProblemDetails response
    options.OnBeforeWriteDetails = (ctx, problem) =>
    {
        // Get the exception from HttpContext.Features
        var exceptionFeature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is UnauthorizedAccessException ex && !string.IsNullOrEmpty(ex.Message))
        {
            // Set the exception message in detail field
            problem.Detail = ex.Message;
        }
    };
});

// Correlation Id via custom middleware (no DI registration needed)

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    // AuthPolicy: More lenient for authenticated users to allow page navigation with multiple concurrent requests
    options.AddPolicy("AuthPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30, // Increased from 5 to 30 to handle page navigation with multiple concurrent API calls
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 10 // Increased from 2 to 10 to handle burst of requests during page navigation
            }));
});

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"] ?? "");
if (key.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes (256 bits). Update configuration.");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// Global authorization: require authenticated users by default
builder.Services.AddAuthorization(options =>
{
	options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.Build();
});

var app = builder.Build();

// Apply only pending migrations and seed on first run
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pending = await context.Database.GetPendingMigrationsAsync();
    if (pending.Any())
    {
        await context.Database.MigrateAsync();
    }
    await SeedData.SeedAsync(context);

}

// Middleware
app.UseProblemDetails();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Default");
app.UseRateLimiter();

// Response caching middleware (required for VaryByQueryKeys)
app.UseResponseCaching();

// Static files support for media uploads - must be before authentication
app.UseStaticFiles();

app.UseAuthentication();
app.UseMiddleware<TenantBindingMiddleware>();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
