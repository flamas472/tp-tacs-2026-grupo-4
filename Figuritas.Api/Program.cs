using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Figuritas.Api.Hubs;
using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Figuritas.Api.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var secretKey = builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT secret key is not configured. " +
                    "Set the 'Jwt:Key' value via the 'Jwt__Key' environment variable before starting the application.");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };

    // SignalR WebSocket connections cannot send HTTP headers, so the JWT token
    // is passed as the 'access_token' query parameter during the upgrade handshake.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/api/notification-hub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },

        // After the JWT signature is verified, check that the bearer account is still
        // active and that the token was not issued before a security event (e.g. ban).
        //
        // Flow:
        //   1. Parse the "iat" (issued-at) claim — emitted explicitly by AuthService.GenerateToken.
        //   2. Fetch the user from the database (one lightweight read per authenticated request).
        //   3. Reject with Fail() if:
        //        a. The user no longer exists.
        //        b. The user is currently banned.
        //        c. The token's iat predates user.TokenValidFrom (set at ban time).
        //
        // context.Fail() causes the authentication middleware to respond with HTTP 401
        // without executing any controller action.
        OnTokenValidated = async context =>
        {
            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return;

            var iatClaim = context.Principal?.FindFirst("iat")?.Value;
            if (!long.TryParse(iatClaim, out var iatUnix))
                return;

            var tokenIssuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;

            var userRepo = context.HttpContext.RequestServices
                .GetRequiredService<IUserRepository>();
            var user = userRepo.GetById(userId);

            if (user == null || user.Banned)
            {
                context.Fail("User account is not accessible.");
                return;
            }

            if (user.TokenValidFrom.HasValue && tokenIssuedAt < user.TokenValidFrom.Value)
            {
                context.Fail("Token was issued before the last account security event.");
            }
        }
    };
});

// Rate limiting — policies read configuration at request time so that integration
// tests can set "RateLimit:*Enabled" = false via in-memory config overrides without
// the startup-time configuration issue affecting AddRateLimiter.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
    {
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var enabled = config.GetValue<bool>("RateLimit:LoginEnabled", defaultValue: true);

        if (!enabled)
            return RateLimitPartition.GetNoLimiter("login-disabled");

        var permitLimit = config.GetValue<int>("RateLimit:LoginPermitLimit", defaultValue: 5);
        var windowSeconds = config.GetValue<int>("RateLimit:LoginWindowSeconds", defaultValue: 60);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.AddPolicy("register", httpContext =>
    {
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var enabled = config.GetValue<bool>("RateLimit:RegisterEnabled", defaultValue: true);

        if (!enabled)
            return RateLimitPartition.GetNoLimiter("register-disabled");

        var permitLimit = config.GetValue<int>("RateLimit:RegisterPermitLimit", defaultValue: 5);
        var windowSeconds = config.GetValue<int>("RateLimit:RegisterWindowSeconds", defaultValue: 60);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.AddPolicy("refresh", httpContext =>
    {
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var enabled = config.GetValue<bool>("RateLimit:RefreshEnabled", defaultValue: true);

        if (!enabled)
            return RateLimitPartition.GetNoLimiter("refresh-disabled");

        var permitLimit = config.GetValue<int>("RateLimit:RefreshPermitLimit", defaultValue: 20);
        var windowSeconds = config.GetValue<int>("RateLimit:RefreshWindowSeconds", defaultValue: 60);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = 429;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddSignalR();

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IIdGenerator, MongoIdGenerator>();

// Register services
builder.Services.AddScoped<NationalTeamService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<StickerService>();
builder.Services.AddScoped<ExchangeProposalService>();
builder.Services.AddScoped<ExchangeService>();
builder.Services.AddScoped<AuctionService>();
builder.Services.AddScoped<SuggestionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<AuctionWatchlistService>();
builder.Services.AddScoped<AdminAnalyticsService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddHostedService<AuctionEndingWorker>();

// Register repositories with interface mappings
builder.Services.AddScoped<IExchangeRepository, ExchangeRepository>();
builder.Services.AddScoped<INationalTeamRepository, NationalTeamRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IStickerRepository, StickerRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserStickerRepository, UserStickerRepository>();
builder.Services.AddScoped<IExchangeProposalRepository, ExchangeProposalRepository>();
builder.Services.AddScoped<IAuctionRepository, AuctionRepository>();
builder.Services.AddScoped<IAuctionOfferRepository, AuctionOfferRepository>();
builder.Services.AddScoped<IMissingStickerRepository, MissingStickerRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IAuctionWatchlistRepository, AuctionWatchlistRepository>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Figuritas API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGci..."
    });

    options.AddSecurityRequirement(document =>
    {
        var requirement = new OpenApiSecurityRequirement();
        var schemeReference = new OpenApiSecuritySchemeReference("Bearer", document);
        requirement.Add(schemeReference, new List<string>());
        return requirement;
    });
});

var allowedOrigins = builder.Configuration.GetValue<string>("AllowedOrigins")
    ?.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorLocalPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    SeedData.EnsureSeedData(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    using (var devScope = app.Services.CreateScope())
    {
        Figuritas.Api.Infrastructure.Persistence.DevSeedData.SeedDevData(devScope.ServiceProvider);
    }

    app.UseSwagger();
    app.UseSwaggerUI();

    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/")
        {
            context.Response.Redirect("/swagger/index.html", permanent: false);
            return;
        }
        await next();
    });
}

app.UseCors("BlazorLocalPolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/api/notification-hub");

app.Run();
