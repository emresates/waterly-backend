# Waterly API

Waterly, React Native ile geliştirilen su içme takip uygulamasının backend'idir. Kullanıcılar günlük su tüketimlerini kaydeder, kişisel hedef belirler, hatırlatma ayarlarını saklar, geçmiş ve istatistiklerini görür. İsteğe bağlı olarak Apple Health / Health Connect özet verileriyle basit, açıklanabilir aktivite önerileri alır.

Tek bir ASP.NET Core Web API projesidir. Katmanlı mimari, CQRS, MediatR veya repository pattern yoktur: **Controller → Service → EF Core → PostgreSQL**.

---

## Kullanılan teknolojiler

| Alan | Teknoloji |
|---|---|
| Framework | ASP.NET Core Web API (.NET 10), C#, nullable reference types açık |
| Veritabanı | PostgreSQL + Entity Framework Core 10 (Code First, migrations) |
| Sağlayıcı | Npgsql.EntityFrameworkCore.PostgreSQL |
| Kimlik doğrulama | JWT Bearer access token + veritabanında hash'li, rotation uygulanan refresh token |
| Parola | ASP.NET Core `PasswordHasher<T>` (PBKDF2) |
| Dokümantasyon | Swagger / OpenAPI (Swashbuckle) |
| Güvenlik | HTTPS yönlendirme, HSTS, CORS, yerleşik Rate Limiting |
| Test | xUnit + `WebApplicationFactory`, gerçek PostgreSQL üzerinde |

## Klasör yapısı

```text
waterly-backend/
├── Waterly.slnx
├── Waterly.Api/                 ← tek production projesi
│   ├── Controllers/             HTTP isteğini alır, servisi çağırır, ApiResponse döner. İş mantığı yok.
│   ├── Services/                Tüm iş kuralları ve EF Core sorguları burada.
│   ├── Interfaces/              Her servis için bir interface (DI ve test için).
│   ├── Models/                  EF Core entity'leri (veritabanı tabloları).
│   ├── DTOs/                    API'ye giren/çıkan sınıflar. Entity'ler asla doğrudan dönülmez.
│   │   └── Responses/           ApiResponse<T> ve PagedResponse<T>
│   ├── Data/AppDbContext.cs     DbSet'ler, ilişkiler, index'ler, constraint'ler
│   ├── Middleware/              ExceptionMiddleware: tüm hataları tek formatta döndürür
│   ├── Exceptions/              AppException ve sabit hata kodları (ErrorCodes)
│   ├── Helpers/                 Tarih/saat dilimi, ilerleme hesabı, sayfalama yardımcıları
│   ├── Settings/                JwtSettings (appsettings'ten bağlanır)
│   ├── Migrations/              EF Core migration'ları (InitialCreate)
│   └── Program.cs               Servis kayıtları ve HTTP pipeline
└── Waterly.Api.Tests/           İş kurallarını doğrulayan testler
```

## Bir istek nasıl işlenir?

Örnek: `POST /api/hydration/intakes`

1. **JWT middleware** `Authorization: Bearer ...` başlığını doğrular. Geçersizse `401 unauthorized` döner.
2. **Model validation** DTO üzerindeki Data Annotation'ları kontrol eder. Hata varsa `400 validationFailed` döner.
3. **`HydrationController.AddIntake`** kullanıcı id'sini yalnızca token'daki `sub` claim'inden alır ve `IHydrationService.AddIntakeAsync` çağırır.
4. **`HydrationService`**:
   - Aynı `(UserId, ClientEventId)` daha önce kaydedildiyse yeni kayıt açmaz, eskisini döner.
   - Miktarı, zamanı ve saat dilimini doğrular.
   - Kaydın hangi **yerel güne** ait olduğunu hesaplar (`LocalDate`).
   - `db.WaterIntakes.Add(...)` + `SaveChangesAsync` ile PostgreSQL'e yazar.
   - `GoalService.GetProgressForDateAsync` ile o günün hedefini ve toplamını hesaplar.
5. Controller sonucu `ApiResponse<T>` içinde döner (`201` yeni kayıt, `200` daha önce işlenmiş kayıt).
6. Herhangi bir adımda `AppException` fırlatılırsa **ExceptionMiddleware** onu yakalar ve standart hata formatına çevirir.

---

## Kurulum

### 1. Gereksinimler

- .NET 10 SDK
- PostgreSQL 14+ (geliştirmede 18 ile test edildi)
- EF Core CLI: `dotnet tool install --global dotnet-ef` (veya `dotnet tool update --global dotnet-ef`)

### 2. PostgreSQL

macOS (Homebrew):

```bash
brew install postgresql@18
brew services start postgresql@18
createdb waterly_dev
```

Docker ile:

```bash
docker run -d --name waterly-postgres -p 5432:5432 \
  -e POSTGRES_USER=waterly -e POSTGRES_PASSWORD=waterly -e POSTGRES_DB=waterly_dev \
  postgres:18
```

### 3. Gizli ayarlar (connection string ve JWT anahtarı)

Gizli bilgiler **kaynak kodda tutulmaz**. `appsettings.json` içinde bu alanlar bilerek boştur; uygulama bunlar olmadan açılmaz.

Geliştirme ortamında `dotnet user-secrets` kullanın (değerler proje dışında, kullanıcı profilinizde saklanır):

```bash
cd Waterly.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=waterly_dev;Username=waterly;Password=waterly"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

`Jwt:SigningKey` en az 32 byte olmalıdır.

### 4. Migration

```bash
cd Waterly.Api

# Veritabanını en son migration'a getir (tabloları oluşturur)
dotnet ef database update

# Model değiştiğinde yeni migration eklemek için
dotnet ef migrations add AddSomething

# Production'a uygulanacak SQL'i script olarak almak için
dotnet ef migrations script --idempotent -o migrate.sql

# Ya da tek dosyalık çalıştırılabilir migration paketi
dotnet ef migrations bundle --self-contained -o efbundle
./efbundle --connection "<production connection string>"
```

Uygulama açılışta migration **uygulamaz**; bu bilinçli bir tercih. Production'da migration, deploy adımının bir parçası olarak (script veya bundle ile) kontrollü şekilde çalıştırılmalıdır.

### 5. Çalıştırma

```bash
cd Waterly.Api
dotnet run --launch-profile http     # http://localhost:5290
dotnet run --launch-profile https    # https://localhost:7296
```

Sunucu durumu: `GET http://localhost:5290/health`

```json
{ "status": "Healthy", "checks": [ { "name": "database", "status": "Healthy" } ] }
```

### 6. Testler

Testler gerçek PostgreSQL kullanır. Her çalıştırmada rastgele isimli bir veritabanı oluşturulur, migration'lar uygulanır ve sonunda silinir.

```bash
# Varsayılan: Host=localhost;Port=5432;Username=<işletim sistemi kullanıcınız>
dotnet test

# Farklı bir sunucu/kullanıcı için
WATERLY_TEST_CONNECTION="Host=localhost;Port=5432;Username=waterly;Password=waterly" dotnet test
```

Test edilen kurallar: su eklendiğinde toplamın artması, aynı `clientEventId` ile çift kayıt oluşmaması (paralel isteklerde dahil), yüzde/kalan hesabı, gece yarısı ve yaz saati sınırları, seyahat/saat dilimi değişikliği, kullanıcı verilerinin izolasyonu, refresh token tekrar kullanımı, logout, streak hesabı, hedef değişiminin geçmiş günleri etkilememesi, hatırlatma saatleri, aktivite önerisi, hesap silmede tüm verinin silinmesi.

---

## Swagger ile test etme

1. Uygulamayı Development ortamında çalıştırın ve `http://localhost:5290/swagger` adresini açın.
2. `POST /api/auth/register` veya `POST /api/auth/login` çağırın, yanıttaki `data.accessToken` değerini kopyalayın.
3. Sağ üstteki **Authorize** butonuna basın, token'ı yapıştırın (başına `Bearer` yazmayın).
4. Artık korumalı endpoint'leri deneyebilirsiniz.

Production'da Swagger kapalıdır. Açmak için `Swagger__Enabled=true`.

## JWT ile yetkilendirilmiş istek

```bash
# Kayıt
curl -X POST http://localhost:5290/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Ayşe","email":"ayse@example.com","password":"Passw0rd!","timeZone":"Europe/Istanbul"}'

# Token ile istek
curl http://localhost:5290/api/hydration/today \
  -H "Authorization: Bearer <accessToken>"

# Su ekleme
curl -X POST http://localhost:5290/api/hydration/intakes \
  -H "Authorization: Bearer <accessToken>" -H "Content-Type: application/json" \
  -d '{"amountMl":250,"consumedAt":"2026-11-23T13:30:00Z","clientEventId":"3f1c9a52-7c1e-4a8e-9a43-0f3b2b7c9d11"}'
```

---

## Environment variable listesi

ASP.NET Core'da `:` yerine environment variable'larda `__` (iki alt çizgi) kullanılır.

| Değişken | Zorunlu | Açıklama | Örnek |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✅ | PostgreSQL bağlantısı | `Host=db;Port=5432;Database=waterly;Username=waterly;Password=***;SSL Mode=Require` |
| `Jwt__SigningKey` | ✅ | HMAC-SHA256 imza anahtarı, en az 32 byte | `openssl rand -base64 48` çıktısı |
| `Jwt__Issuer` | | Token issuer | `Waterly.Api` |
| `Jwt__Audience` | | Token audience | `Waterly.Mobile` |
| `Jwt__AccessTokenMinutes` | | Access token süresi (dk) | `15` |
| `Jwt__RefreshTokenDays` | | Refresh token süresi (gün) | `30` |
| `Cors__AllowedOrigins__0`, `__1`, ... | | Tarayıcı istemcileri için izinli origin'ler (wildcard yok) | `https://app.waterly.com` |
| `RateLimiting__AuthPermitLimitPerMinute` | | IP başına dakikalık login/register/refresh limiti | `10` |
| `RateLimiting__GeneralPermitLimitPerMinute` | | Kullanıcı (veya IP) başına dakikalık genel limit | `120` |
| `Swagger__Enabled` | | Development dışında Swagger'ı açar | `false` |
| `ASPNETCORE_ENVIRONMENT` | | `Development` / `Production` | `Production` |
| `ASPNETCORE_URLS` | | Dinlenecek adres | `http://+:8080` |

> Not: Uygulama bir reverse proxy (Nginx, load balancer, PaaS) arkasında çalışacaksa, gerçek istemci IP'sinin rate limiting'e ulaşması için proxy'nizi tanıyan bir `ForwardedHeaders` yapılandırması eklemeniz gerekir.

---

## API özeti

Tüm yanıtlar aynı formattadır:

```json
{ "success": true, "message": "İşlem başarılı.", "statusCode": 200, "data": { } }
```

```json
{ "success": false, "message": "Su tüketim kaydı bulunamadı.", "statusCode": 404, "errorCode": "intakeNotFound", "data": null }
```

Validation hatalarında ek olarak alan bazlı `errors` döner:

```json
{ "success": false, "statusCode": 400, "errorCode": "validationFailed", "message": "Gönderilen bilgiler geçersiz.",
  "errors": { "email": ["Geçerli bir e-posta adresi girin."] }, "data": null }
```

Hata kodları: `userNotFound`, `emailAlreadyExists`, `invalidCredentials`, `invalidToken`, `unauthorized`, `forbidden`, `intakeNotFound`, `invalidWaterAmount`, `invalidConsumedAt`, `invalidTimeZone`, `invalidDateRange`, `invalidReminderSettings`, `goalNotFound`, `validationFailed`, `tooManyRequests`, `internalServerError`.

| Metot | Yol | Açıklama |
|---|---|---|
| POST | `/api/auth/register` | Kayıt + token. Varsayılan hedef (2000 ml) ve hatırlatma ayarları oluşturulur |
| POST | `/api/auth/login` | Giriş, `accessToken` + `refreshToken` |
| POST | `/api/auth/refresh` | `{ refreshToken }` → yeni token çifti (eskisi iptal) |
| POST | `/api/auth/logout` | `{ refreshToken }` iptal edilir (auth gerekli) |
| GET / PUT | `/api/users/me` | Profil görüntüleme / güncelleme (`name`, `timeZone`) |
| DELETE | `/api/users/me` | Body: `{ password }`. Hesap ve tüm verileri silinir |
| GET | `/api/hydration/today` | Bugünün ilerlemesi + bugünkü kayıtlar |
| POST | `/api/hydration/intakes` | Su ekle (idempotent, `clientEventId` zorunlu) |
| POST | `/api/hydration/intakes/sync` | Çevrimdışı kuyruğu toplu gönder (en fazla 100) |
| GET | `/api/hydration/intakes` | `?from&to&page&pageSize` (varsayılan: bugün) |
| PUT / DELETE | `/api/hydration/intakes/{id}` | Kaydı düzenle / sil |
| GET | `/api/hydration/history` | Günlük özetler, `?from&to&page&pageSize` (varsayılan son 30 gün) |
| GET / PUT | `/api/goals/current` | Aktif hedef + bugünkü ilerleme / hedef güncelle `{ targetMl }` |
| GET | `/api/goals/history` | Hedef değişiklik geçmişi |
| GET / PUT | `/api/reminders/settings` | Hatırlatma ayarları + hesaplanmış `dailyReminderTimes` |
| GET | `/api/statistics/daily` | `?date` — saatlik dağılım dahil |
| GET | `/api/statistics/weekly` | `?endDate` — son 7 gün |
| GET | `/api/statistics/monthly` | Parametresiz son 30 gün, veya `?year=2026&month=11` |
| GET | `/api/statistics/streak` | `currentStreak`, `longestStreak` |
| POST / GET | `/api/health/activity` | Günlük aktivite özetleri gönder / listele |
| GET | `/api/health/recommendation` | `?date` — aktivite bazlı, otomatik değişiklik yapmayan öneri |
| GET | `/health` | Sunucu ve veritabanı durumu (auth gerekmez) |

---

## Önemli tasarım kararları

**Tarih ve saat.** Gerçek anlar (`ConsumedAt`, `CreatedAt`) `timestamp with time zone` olarak UTC saklanır. Bir su kaydının hangi güne ait olduğu, kayıt anında kullanıcının IANA saat dilimiyle hesaplanıp `LocalDate` (`date`) sütununa yazılır. Böylece yaz saati geçişleri doğru işlenir. Kullanıcı sonradan saat dilimini değiştirse veya seyahat etse bile eski kayıtlar aynı günde kalır. Seyahat sırasında mobil uygulama cihazın o anki saat dilimini `timeZone` alanında gönderebilir. Hatırlatma saatleri `TimeOnly` (`time`), takvim günleri `DateOnly` (`date`) olarak tutulur.

**Hedef geçmişi.** Hedef satırları hiç güncellenmez. Her değişiklik, `EffectiveFrom = kullanıcının bugünü` ile yeni bir satır ekler. Bir günün hedefi, `EffectiveFrom <= o gün` olan en yeni satırdır. Bu yüzden bugün hedefi değiştirmek dünün istatistiğini bozmaz.

**Idempotency.** `WaterIntakes` tablosunda `(UserId, ClientEventId)` üzerinde unique index vardır. Servis önce mevcut kaydı arar. İki istek aynı anda gelirse veritabanı yalnızca birini kabul eder; diğeri unique ihlalini yakalayıp kazanan kaydı döner. Her iki durumda da yanıtta `alreadyProcessed: true` olur.

**Refresh token.** Token'ın kendisi değil SHA-256 hash'i saklanır. Her yenilemede eski token iptal edilip yenisi verilir (rotation). İptal işlemi `WHERE RevokedAt IS NULL` koşullu tek bir UPDATE olduğu için aynı token'la gelen eşzamanlı iki istekten yalnızca biri başarılı olur. Daha önce rotate edilmiş bir token tekrar gelirse token çalınmış sayılır ve kullanıcının tüm oturumları kapatılır.

**Veri izolasyonu.** Kullanıcı id'si yalnızca JWT'den okunur. Tüm sorgular `UserId` ile filtrelenir. Başkasına ait bir kayıt, var olmayan bir kayıtla aynı yanıtı verir (`404`); bu sayede id tahmin edilerek kayıt bulunamaz.

**Streak.** Bugün henüz tamamlanmadıysa seri dünden geriye doğru sayılır, yani gün bitmeden seri sıfırlanmaz. Hedefe ulaşılmadan geçen tam bir gün seriyi bozar. Her gün, o günün kendi hedefiyle değerlendirilir.

**Sağlık önerisi.** Sabit bir "X adım = Y ml" kuralı yoktur. Kullanıcının o günkü adım/egzersiz değeri, kendi son 14 günlük ortalamasıyla karşılaştırılır (en az 3 gün veri gerekir). Belirgin artış varsa yalnızca *hedefini gözden geçirmesi* önerilir; hedef asla otomatik değişmez ve yanıtta tıbbi tavsiye olmadığına dair bir uyarı bulunur. Yalnızca adım, egzersiz dakikası ve opsiyonel aktif kalori saklanır.

**Hatırlatmalar.** Backend yalnızca ayarları saklar. Push sunucusu, BackgroundService veya Hangfire yoktur. Yanıttaki `dailyReminderTimes` (örn. `["09:00","11:00",...]`) mobil tarafın yerel bildirimleri planlamasını kolaylaştırır. Gece yarısını geçen aralıklar (22:00–02:00) desteklenir.

---

## React Native entegrasyon notları

**Token yönetimi**
- `accessToken` (15 dk) bellekte, `refreshToken` (30 gün) güvenli depoda tutulmalıdır (`expo-secure-store` / Keychain / Keystore).
- `401` alındığında **tek bir** refresh isteği yapın, bekleyen diğer istekleri kuyruğa alın ve yeni token ile tekrar deneyin. Aynı refresh token'la paralel iki yenileme isteği göndermek, ikincisinin reddedilmesine yol açar.
- Refresh `401 invalidToken` dönerse kullanıcıyı login ekranına yönlendirin.

**Su ekleme ve "İçtim" bildirimi**
- Her kullanıcı eylemi için **bir kez** `clientEventId = uuid()` üretin ve bu id'yi, işlem sunucuya ulaşana kadar tüm denemelerde aynen kullanın.
- Ağ yoksa olayı yerel kuyruğa yazın (`clientEventId`, `amountMl`, `consumedAt` = gerçek içme anı, `timeZone`). İnternet geldiğinde `POST /api/hydration/intakes/sync` ile gönderin. `Created` ve `AlreadyProcessed` olanları kuyruktan silin; `Failed` olanları kullanıcıya gösterin veya atın.
- Bildirim butonu aynı olayı iki kez tetiklese bile sunucuda tek kayıt oluşur.
- Yanıttaki `progress` alanı (`consumedMl`, `remainingMl`, `progressPercentage`, `goalCompleted`) ile animasyonu doğrudan güncelleyebilirsiniz.

**Saat dilimi**
- Kayıt sırasında `Intl.DateTimeFormat().resolvedOptions().timeZone` (örn. `"Europe/Istanbul"`) gönderin.
- Cihaz saat dilimi profildekinden farklıysa (seyahat) bunu `PUT /api/users/me` ile güncelleyebilir veya her su kaydında `timeZone` alanı olarak gönderebilirsiniz.
- `consumedAt` için `new Date().toISOString()` kullanın.

**Hatırlatmalar**
- Uygulama açıldığında `GET /api/reminders/settings` çağırın ve `dailyReminderTimes` listesindeki her saat için `expo-notifications` ile günlük tekrar eden yerel bildirim planlayın. Bildirime "İçtim" aksiyonu ekleyin; aksiyon `defaultGlassMl` miktarını yeni bir `clientEventId` ile eklemelidir.
- Ayar değiştiğinde önce eski bildirimleri iptal edin, sonra yeniden planlayın. Bildirim izinlerini mobil tarafta isteyin.

**Sağlık verisi**
- HealthKit / Health Connect izinleri alındıysa, son 7 günün günlük özetini `POST /api/health/activity` ile gönderin (`source`: `AppleHealth` / `HealthConnect`). Aynı günü tekrar göndermek kaydı günceller.
- `GET /api/health/recommendation` sonucunda `shouldReviewGoal` true ise `message` ve `disclaimer` metnini gösterin; hedef değişikliğini kullanıcıya bırakın.

**Grafikler**
- `/api/statistics/weekly` ve `/monthly` yanıtlarındaki `days` dizisi her gün için bir eleman içerir (su içilmeyen günler `0`), grafiğe doğrudan bağlanabilir.

**Geliştirme ortamında bağlantı**
- iOS simülatörü: `http://localhost:5290`
- Android emülatörü: `http://10.0.2.2:5290`
- Gerçek cihaz: bilgisayarınızın yerel IP'si (örn. `http://192.168.1.20:5290`). Gerekirse `ASPNETCORE_URLS=http://0.0.0.0:5290` ile çalıştırın.
- Native uygulamalar CORS'tan etkilenmez; `Cors:AllowedOrigins` yalnızca Expo Web gibi tarayıcı istemcileri içindir.
