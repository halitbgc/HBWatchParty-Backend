using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using YouTubeParty.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. SignalR servisini ekleyin
builder.Services.AddSignalR();

builder.Services.AddSingleton<PlaybackSyncTracker>();
builder.Services.AddHostedService<PlaybackSyncBackgroundService>();


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

// 4. Hub endpoint'ini haritalayın
app.MapHub<WatchPartyHub>("/watchpartyhub");
app.MapHub<VoiceSignalingHub>("/voicehub");


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
