# HBWatchParty — Backend

YouTube videolarını birlikte, senkron şekilde izlemeyi sağlayan tarayıcı eklentisinin ASP.NET Core (.NET 10) backend'i. Oda bazlı video senkronizasyonu, yazılı sohbet ve WebRTC sesli sohbet sinyalleşmesi sağlar.

## Özellikler

- **Watch party:** Odadaki herkes için oynat/duraklat/ileri sar ve video değiştirme senkronu; arka plan servisiyle kayma düzeltme
- **Sohbet:** Oda içi mesajlaşma ve "yazıyor…" göstergesi
- **Sesli sohbet:** P2P (mesh) WebRTC; sunucu yalnızca SDP/ICE sinyallerini iletir
- **Hesaplar:** ASP.NET Identity + JWT ile kayıt/giriş, görünen ad
- **Odalar:** Kısa kodlu oda oluşturma, üyelerin veritabanında tutulması

## Teknolojiler

ASP.NET Core 10 · SignalR · Entity Framework Core (SQL Server) · ASP.NET Identity · JWT Bearer

## Kurulum

```bash
cd YouTubeParty

# Gizli ayarlar (repoya eklenmez)
dotnet user-secrets set "Jwt:Key" "<en az 32 karakterlik rastgele anahtar>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<SQL Server bağlantı cümlesi>"

dotnet ef database update
dotnet run
```

Varsayılan adres: `http://localhost:5203` / `https://localhost:7070`

## API

| Metot | Endpoint                  | Açıklama                    |
|-------|---------------------------|-----------------------------|
| POST  | `/api/auth/register`      | Kayıt ol                    |
| POST  | `/api/auth/login`         | Giriş yap (JWT döner)       |
| POST  | `/api/rooms`              | Yeni oda oluştur            |
| GET   | `/api/rooms/{code}`       | Oda bilgisini getir         |
| PUT   | `/api/users/display-name` | Görünen adı güncelle        |

## SignalR Hub'ları

Token, `access_token` query string parametresiyle gönderilir.

- **`/watchpartyhub`** — `JoinRoom`, `LeaveRoom`, `ChangeVideo`, `SendPlay`, `SendPause`, `SendSeek`, `ReportPlaybackTime`, `SendChatMessage`, `SendTyping`, `SendSystemLog`
- **`/voicehub`** — `JoinVoiceRoom`, `SendVoiceSignal`, `LeaveVoiceRoom`

Sesli sohbetin ayrıntılı işleyişi için: [SESLI_SOHBET_REHBERI.md](SESLI_SOHBET_REHBERI.md)

## Proje Yapısı

```
YouTubeParty/
├── Controllers/   REST endpoint'leri (Auth, Rooms, Users)
├── Hubs/          SignalR hub'ları ve oynatma senkron servisi
├── Data/          EF Core DbContext
├── Models/        Entity'ler (User, Room, RoomMember)
├── DTOs/          İstek/yanıt modelleri
├── Services/      JWT ve oda kodu üretimi
└── Migrations/    EF Core migration'ları
```
