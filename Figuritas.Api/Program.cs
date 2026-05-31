using System.Text;
using System.Text.Json.Serialization;
using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
                ?? throw new InvalidOperationException("Missing 'Jwt:Key' configuration in appsettings.json");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorLocalPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5280", "http://localhost:5048")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    SeedData.EnsureSeedData(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("BlazorLocalPolicy");

app.MapControllers();

app.Run();
