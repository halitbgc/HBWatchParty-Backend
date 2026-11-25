using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using YouTubeParty.Data;
using YouTubeParty.Hubs;
using YouTubeParty.Models;
using YouTubeParty.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. SignalR servisini ekleyin
builder.Services.AddSignalR();

builder.Services.AddSingleton<PlaybackSyncTracker>();
builder.Services.AddHostedService<PlaybackSyncBackgroundService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Kullanıcı hesapları (Identity Core - cookie/UI olmadan, sadece UserManager/SignInManager)
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRoomCodeGenerator, RoomCodeGenerator>();

// JWT Bearer authentication (REST + SignalR hub'ları için ortak)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Jwt:Key yapılandırılmamış (user-secrets veya ortam değişkeni ile ayarlanmalı).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // SignalR WebSocket/SSE bağlantılarında header set edilemediği için
        // token'ı query string'deki access_token üzerinden okuyoruz (yalnızca hub path'leri için).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/watchpartyhub") || path.StartsWithSegments("/voicehub")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();


// 2. CORS Politikasını tanımlayın
builder.Services.AddCors(options =>
{
    options.AddPolicy("ExtensionPolicy", policy =>
    {
        // Geliştirme aşamasında her yerden gelen isteklere izin veriyoruz
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // SignalR için zorunlu
    });
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 3. CORS politikasını aktifleştirin (Sıralama önemlidir, MapHub'dan önce olmalı)
app.UseCors("ExtensionPolicy");

// 3b. Authentication/Authorization (henüz hiçbir hub veya controller'da [Authorize] yok,
// bu yüzden mevcut davranış değişmiyor - altyapı hazır hale getiriliyor)
app.UseAuthentication();
app.UseAuthorization();

// 4. Hub endpoint'ini haritalayın
app.MapHub<WatchPartyHub>("/watchpartyhub");
app.MapHub<VoiceSignalingHub>("/voicehub");


app.UseHttpsRedirection();

app.MapControllers();

app.Run();
