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

Hata kodları: `userNotFound`, `emailAlreadyExists`, `invalidCredentials`, `invalidToken`, `unauthorized`, `forbidden`, `intakeNotFound`, `invalidWaterAmount`, `invalidConsumedAt`, `invalidTimeZone`, `invalidDateRange`, `invalidReminderSettings`, `goalNotFound`, `validationFailed`, `tooManyRequests`, `internalServerError`, `invalidAchievementEvent`. Başarım sistemi için ayrıca `achievementNotFound`, `achievementAlreadyUnlocked`, `rewardNotFound`, `invalidReward`, `achievementEventAlreadyProcessed` sabitleri tanımlıdır; tekrar gönderimler hata değil, başarılı yanıt olarak döner.

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
| GET | `/api/achievements` | Tüm başarımlar + ilerleme. `?category&rarity&unlocked&page&pageSize` |
| GET | `/api/achievements/me` | Kazanılan başarımlar |
| GET | `/api/achievements/me/summary` | Sayılar, XP, seviye, nadirlik dağılımı |
| GET | `/api/achievements/me/progress` | Seviye çubuğu için XP / seviye bilgisi |
| GET | `/api/achievements/me/recent` | Son kazanılanlar `?count=10` |
| GET | `/api/achievements/me/unseen` | Animasyonu henüz gösterilmemiş kazanımlar |
| POST | `/api/achievements/me/seen` | `{ achievementIds: [..] }` veya `{ all: true }` |
| POST | `/api/achievements/me/evaluate` | Tüm başarımları kayıtlı veriden yeniden hesapla (idempotent) |
| GET | `/api/achievements/me/rewards` | Kazanılan kozmetik ödüller |
| POST | `/api/achievements/events` | Mobil etkileşim olayı (rate limit'li, idempotent) |
| GET / PUT | `/api/users/me/preferences` | Görünüm tercihleri: karanlık mod, aktif şişe teması ve kozmetikler |
| GET | `/api/cosmetics` | Bütün kozmetik ödüller + kilit / aktif durumu. `?type=BottleTheme` |

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

## Başarım Sistemi (Achievement, XP, Level, Reward)

Kullanıcı su içtikçe ve uygulamayı kullandıkça başarımlar, XP, seviye ve kozmetik ödüller kazanır. Her şey PostgreSQL'de saklanır; telefon değişse de kaybolmaz. Kod: `Services/AchievementService.cs`, `Helpers/AchievementMetricsCalculator.cs`, `Helpers/LevelCalculator.cs`, `Data/AchievementCatalog.cs`, `Data/RewardCatalog.cs`.

### Kategoriler ve nadirlik

| Kategori | Adet | İçerik |
|---|---|---|
| `GettingStarted` | 10 | İlk oturum, ilk su, kayıt günü hedefleri, ilk hedef, ayarlar, 7 aktif gün |
| `Streak` | 14 | 2–365 günlük seri, ikinci şans, toplam 30/100/365 hedef günü |
| `WaterVolume` | 12 | Toplam 1 L – 10.000 L (günlük hedefle sınırlı) |
| `GlassCollection` | 10 | 1–5000 bardak eşdeğeri (250 ml) |
| `DailyHabits` | 10 | Sabah / öğle / akşam, dengeli gün, bildirimden su ekleme |
| `Discovery` | 10 | Ekran keşifleri, kişiselleştirme, sağlık bağlantısı |
| `Secret` | 10 | Kilitliyken adı, açıklaması, kodu ve ilerlemesi gizlenen başarımlar |

Toplam **76** başarım. Nadirlik: `Common`, `Rare`, `Epic`, `Legendary`, `Mythic`. Enum'lar JSON'da string döner (`"category": "Streak"`).

### Tablolar

| Tablo | Açıklama |
|---|---|
| `Achievements` | Katalog (seed). `Code` unique, sabit `Id` |
| `UserAchievements` | Kazanımlar. `(UserId, AchievementId)` unique, `SeenAt` animasyon durumu |
| `UserProgress` | Kullanıcı başına tek satır: `TotalXp`, `Level` |
| `Rewards` | Kozmetik ödül kataloğu (seed). `Code` unique |
| `UserRewards` | Açılan ödüller. `(UserId, RewardId)` unique |
| `UserAchievementEvents` | Mobil olaylar + sunucu oturum kayıtları. `(UserId, ClientEventId)` ve `(UserId, EventType, DedupeKey)` unique |
| `UserPreferences` | Kullanıcı başına tek satır: `ColorScheme` ve aktif kozmetikler (`Active*Id` → `Rewards`) |
| `UserProgress.LastActiveDate` | Son aktif yerel gün |
| `WaterIntakes.Source` | Yeni sütun: `Manual` (varsayılan) / `ReminderNotification` |

Kullanıcı silindiğinde tüm kullanıcıya ait başarım verileri cascade ile silinir; katalog tabloları etkilenmez.

### Seed mekanizması

Başarımlar ve ödüller `Data/AchievementCatalog.cs` ve `Data/RewardCatalog.cs` içinde C# olarak tanımlıdır ve `HasData` ile `AddAchievementSystem` migration'ına sabit id'lerle yazılmıştır. Migration tekrar çalıştırıldığında yeni satır oluşmaz. Kurallar:

- `Id` ve `Code` kalıcıdır; değiştirilmez, tekrar kullanılmaz. Yeni başarım listenin sonuna yeni id ile eklenir ve `dotnet ef migrations add ...` ile yeni migration oluşturulur.
- Kaldırılacak başarım silinmez, `IsActive = false` yapılır; kazananlar kaybetmez.
- `Name` / `Description` Türkçe varsayılandır. Çeviri eklenirken `Code` anahtar olarak kullanılabilir (ör. `achievements.FIRST_DROP.name`) veya `(AchievementId, Culture)` anahtarlı ayrı bir çeviri tablosu eklenebilir.

### Hesaplama mantığı

`AchievementService.EvaluateAchievementsAsync`:

1. Kullanıcının `UserProgress` satırı `SELECT ... FOR UPDATE` ile kilitlenir. Aynı kullanıcının değerlendirmeleri sırayla çalışır, paralel isteklerde çift kazanım olmaz.
2. Bütün metrikler kayıtlı veriden **yeniden hesaplanır** (artırma yok). Su verisi PostgreSQL içinde güne göre gruplanır; tek tek su kayıtları belleğe alınmaz.
3. Eksik başarımlar eklenir. Döngü, yeni açılanlar meta başarımları (`SECRET_ACHIEVEMENTS_*`, `SECRET_FIRST`) tetiklemediği ana kadar sürer. Bir başarım yalnızca kilitliyken değerlendirildiği için kendini sayamaz; sonsuz döngü oluşmaz.
4. `TotalXp = SUM(XpReward)` (kazanılan başarımlar) olarak yeniden yazılır, seviye ve seviye ödülleri buna göre güncellenir. Tutarsızlık olsa bile bir sonraki değerlendirme düzeltir.
5. Kazanılan başarım ve ödüller geri alınmaz.

Ne zaman çalışır: kayıt, giriş, günün ilk token yenilemesi, su ekleme / senkronizasyon / düzenleme / silme, hedef değişikliği, hatırlatma ayarı, sağlık verisi, mobil olay ve `POST /api/achievements/me/evaluate`. Ana işlem (ör. su kaydı) başarılı olduktan sonra çalışır; başarım hesabı hata verirse loglanır, su kaydı geçerli kalır ve sonraki değerlendirme eksikleri tamamlar.

Önemli kurallar:

- **Günlük üst sınır:** Bir gün, başarım ilerlemesine en fazla o günün hedefi kadar katkı yapar. 5000 ml içilen gün hedef 2000 ise 2000 ml sayılır. Gerçek tüketim verisi değişmez.
- **Bardak eşdeğeri:** `floor(sınırlı toplam ml / 250)`. Kayıt sayısı kullanılmaz.
- **Geriye tarihli kayıt:** Su başarımlarında yalnızca, içildiği andan en fazla **72 saat** sonra kaydedilmiş su sayılır (`CreatedAt - ConsumedAt <= 72h`). Çevrimdışı senkronizasyon çalışır, tek seferde bir haftayı geriye doldurup seri kazanmak mümkün değildir.
- **Seri:** `StatisticsService` ile aynı kural; her gün o günün kendi hedefiyle değerlendirilir. Açma koşulu "en uzun seri"dir, ilk kez 30 gün olan kullanıcı 2/3/7/10/14/21'i de alır. Kilitli seri başarımlarında `currentValue` güncel seriyi gösterir ve bugün henüz tamamlanmadıysa sıfırlanmaz.
- **Alışkanlık saatleri** (kaydın kendi saat dilimine göre yerel saat, en az 100 ml): sabah başlangıcı = günün ilk kaydı 07:00–09:59; öğle 12:00–13:59; akşam 18:00–20:59; dengeli gün = 06:00–11:59, 12:00–17:59 ve 18:00–21:59 aralıklarının her birinde kayıt. 22:00 sonrası hiçbir başarıma katkı vermez.
- **Aktif gün:** Başarılı oturum (sunucu kaydı), `APP_OPENED` veya o gün *kaydedilmiş* su. Aynı gün 100 kez açmak 1 gündür. `SECRET_RETURN_14_DAYS` iki aktif gün arasında en az 14 tam takvim günü boşluk ister; `SECRET_COMEBACK_3_DAYS` dönüş günü dahil 3 aktif gün ister.
- **Tarih:** Olayların günü cihazdan alınmaz; sunucu saati + profil saat dilimiyle hesaplanır. Saat dilimi değişince eski su kayıtlarının `LocalDate` değeri değişmez.
- **Geçmiş kullanıcılar:** Sistem eklenmeden önce kayıt olmuş kullanıcıların başarımları ilk girişte (veya `evaluate` çağrısında) su / hedef / ayar / sağlık geçmişinden hesaplanır. Kaydı olmayan mobil etkileşimler (onboarding, ekran açma vb.) kazanılmış sayılmaz. İşlem idempotenttir.

### XP ve seviye

`Threshold(level) = 5 * (level - 1)^2` toplam XP eşiğidir: Level 1 = 0, Level 2 = 5, Level 10 = 405, Level 50 = 12005, Level 100 = 49005 XP. Seviye 100'de sabitlenir, XP artmaya devam eder. Bir işlemde birden fazla seviye atlanabilir. Katalogdaki toplam XP 74.965'tir.

```json
{ "level": 13, "totalXp": 800, "currentLevelXp": 720, "nextLevelXp": 845, "xpIntoLevel": 80,
  "xpToNextLevel": 45, "progressPercentage": 64.0, "maxLevel": 100, "isMaxLevel": false }
```

XP yalnızca sunucuda hesaplanır. İstemci XP, seviye veya başarım kodu gönderemez; her başarım tek seferliktir, bu yüzden sınırsız XP yolu yoktur. Hedef üzerindeki su ekstra XP vermez.

### Ödüller

| Kod | Tür | Koşul |
|---|---|---|
| `BOTTLE_SKY_BLUE` | BottleTheme | Level 5 |
| `FRAME_AQUA` | ProfileFrame | Level 10 |
| `ANIMATION_BUBBLES` | WaterAnimation | Level 15 |
| `BOTTLE_FROST` | BottleTheme | Level 20 |
| `FRAME_GLOW` | ProfileFrame | Level 30 |
| `WATER_SPARKLE` | WaterAnimation | Level 40 |
| `BOTTLE_GOLD` | BottleTheme | Level 50 |
| `FRAME_AURORA` | ProfileFrame | Level 75 |
| `BOTTLE_MYTHIC` | BottleTheme | Level 100 |
| `MASCOT_SUNGLASSES` | MascotAccessory | `STREAK_7` |
| `BADGE_SHINE` | BadgeEffect | `STREAK_30` |
| `MASCOT_CROWN` | MascotAccessory | `STREAK_100` |
| `BOTTLE_CLASSIC` | BottleTheme | Başlangıç (herkes, varsayılan tema) |
| `BOTTLE_OCEAN` | BottleTheme | Başlangıç (herkes) |
| `BOTTLE_MINT` | BottleTheme | Başlangıç (herkes) |

`AssetKey` (ör. `bottle_sky_blue`) mobil uygulamadaki görsel/animasyon dosyasının adıdır; backend dosya saklamaz.

**Kazanmak ve kullanmak ayrıdır.** Ödül, koşulu sağlanınca `UserRewards` tablosuna yazılır (kazanma). Kullanıcı açtığı ödülü `PUT /api/users/me/preferences` ile seçer; seçim `UserPreferences` tablosunda hesap bazında saklanır (kullanma). Kilitli bir ödül seçilemez (`403 rewardLocked`). Yanlış türde ödül `400 invalidReward`, bilinmeyen kod `404 rewardNotFound` döner. Telefon değişse de aktif tema ve görünüm `GET /api/users/me/preferences` ile geri gelir.

### Son aktif gün ve geri dönüş

`UserProgress.LastActiveDate` her değerlendirmede güncellenir ve `GET /api/achievements/me/summary` içinde `lastActiveDate` olarak döner. Aktif gün; başarılı oturum (kayıt, giriş, günün ilk token yenilemesi), `APP_OPENED` veya o gün kaydedilen su demektir. İki aktif gün arasında en az 14 tam takvim günü boşluk varsa ikinci gün "dönüş" sayılır (`SECRET_RETURN_14_DAYS`). Dönüş günü dahil 3 farklı aktif gün `SECRET_COMEBACK_3_DAYS` kazandırır; aradaki günleri kaçırmak sayımı bozmaz. Hiçbir başarım geri alınmaz.

### Uyumluluk matrisi ve mobil kontrol listesi

- [ACHIEVEMENT_MATRIX.md](ACHIEVEMENT_MATRIX.md): 76 başarımın her biri için gerekli özellik, tetikleyici, backend kaynağı, mobil event, endpoint ve uygulama durumu.
- [MOBILE_FEATURE_CHECKLIST.md](MOBILE_FEATURE_CHECKLIST.md): Mobilde yapılması gereken ekranlar, etkileşimler ve API kullanımı.

`AchievementFeatureTests.EveryAchievementInTheCatalog_CanActuallyBeUnlocked` testi, katalogdaki 76 başarımın tamamının gerçek servis akışlarıyla açılabildiğini (seviye 100 ve bütün ödüller dahil) doğrular.

### Mobil olay sözleşmesi

`POST /api/achievements/events` (kullanıcı başına dakikada 30 istek, `RateLimiting:AchievementEventsPermitLimitPerMinute`)

```json
{ "clientEventId": "3f1c9a52-7c1e-4a8e-9a43-0f3b2b7c9d11", "eventType": "WEEKLY_REPORT_OPENED" }
```

| eventType | eventValue | Ne kadar sayılır |
|---|---|---|
| `APP_OPENED` | – | Günde 1 (aktif gün) |
| `ONBOARDING_COMPLETED` | – | 1 kez |
| `STATISTICS_OPENED`, `HISTORY_OPENED`, `MONTHLY_REPORT_OPENED`, `HEALTH_RECOMMENDATION_VIEWED` | – | Günde 1 |
| `WEEKLY_REPORT_OPENED` | – | ISO haftası başına 1 |
| `CALENDAR_DAY_VIEWED` | opsiyonel, geçmiş gün `yyyy-MM-dd` | Günde 1 |
| `DARK_THEME_ENABLED`, `BOTTLE_EASTER_EGG_DISCOVERED` | – | 1 kez |
| `BOTTLE_THEME_USED` | zorunlu: kullanıcının **sahip olduğu** bir şişe temasının `assetKey` değeri (ör. `bottle_ocean`) | Tema başına 1 |

Not: Tema ve karanlık mod için event göndermek zorunlu değildir. `PUT /api/users/me/preferences` ile yapılan seçim sunucuda aynı kurallarla (tema başına 1, karanlık mod 1 kez) kaydedilir ve tercih edilen yoldur.

Yanıttaki `status`: `Recorded` (kaydedildi ve başarımlar değerlendirildi, `gamification` dolu), `AlreadyProcessed` (aynı `clientEventId` daha önce geldi), `AlreadyCounted` (aynı dönem için zaten sayıldı). Üçü de `200` döner. Tanımsız tip, sunucuya özel `SESSION_STARTED` veya izin verilmeyen `eventValue` `400 invalidAchievementEvent` döner. İstemci olayları güvenilir kabul edilmez; yalnızca düşük riskli Discovery / Secret başarımlarında kullanılır. Su başarımları her zaman sunucudaki kayıtlardan hesaplanır.

### Bildirimden su ekleme

`AddIntakeRequest` (ve sync içindeki öğeler) opsiyonel `source` alanı alır: `"Manual"` (gönderilmezse varsayılan) veya `"ReminderNotification"`. Eski istemciler değişiklik yapmadan çalışır. Idempotency davranışı aynıdır. `IntakeResponse` artık `source` alanını içerir; `IntakeResultResponse` ve `SyncIntakesResponse` yanıtlarında `gamification` alanı (yeni başarımlar, ödüller, seviye) bulunur.

### Mobil entegrasyon sözleşmesi

Bütün endpoint'ler `Authorization: Bearer <accessToken>` ister ve `ApiResponse<T>` döner. Kullanıcı kimliği yalnızca JWT'den alınır. Enum'lar string'dir.

#### Ortak DTO'lar

```jsonc
// AchievementResponse
{
  "id": 41, "code": "GLASSES_100", "name": "Bardak Ustası", "description": "100 bardak eşdeğeri su kaydet.",
  "category": "GlassCollection", "rarity": "Rare", "xpReward": 250, "iconKey": "glasses_100",
  "isSecret": false, "isUnlocked": false, "currentValue": 64, "targetValue": 100, "progressPercentage": 64.0,
  "unlockedAt": null, "seenAt": null
}
// Kilitli gizli başarım: code / currentValue / targetValue / progressPercentage = null,
// name = "Gizli Başarım", iconKey = "secret_locked".

// UserLevelResponse
{ "level": 13, "totalXp": 800, "currentLevelXp": 720, "nextLevelXp": 845, "xpIntoLevel": 80,
  "xpToNextLevel": 45, "progressPercentage": 64.0, "maxLevel": 100, "isMaxLevel": false }

// RewardResponse
{ "id": 1, "code": "BOTTLE_SKY_BLUE", "name": "Gökyüzü Mavisi Şişe", "description": "...", "rewardType": "BottleTheme",
  "assetKey": "bottle_sky_blue", "requiredLevel": 5, "requiredAchievementCode": null, "unlockedAt": "2026-11-23T10:00:00+00:00" }

// AchievementEvaluationResponse ("gamification" alanı)
{ "newlyUnlocked": [AchievementResponse], "newRewards": [RewardResponse], "progress": UserLevelResponse,
  "previousLevel": 4, "leveledUp": true }

// CosmeticItemResponse
{ "id": 14, "code": "BOTTLE_OCEAN", "name": "Okyanus Şişe", "description": "...", "rewardType": "BottleTheme",
  "assetKey": "bottle_ocean", "requiredLevel": 1, "requiredAchievementCode": null,
  "isStarter": true, "isUnlocked": true, "unlockedAt": "...", "isActive": false }
```

#### Endpoint'ler

| Metot | Yol | Request | Response `data` |
|---|---|---|---|
| GET | `/api/achievements` | Query: `category`, `rarity`, `unlocked`, `page` (1), `pageSize` (100, max 100) | `PagedResponse<AchievementResponse>` |
| GET | `/api/achievements/me` | – | `AchievementResponse[]` (kazanılanlar, en yeni önce) |
| GET | `/api/achievements/me/summary` | – | `{ totalAchievements, unlockedAchievements, lockedAchievements, completionPercentage, totalXp, level, levelProgressPercentage, commonUnlocked, rareUnlocked, epicUnlocked, legendaryUnlocked, mythicUnlocked, unseenAchievements, lastActiveDate }` |
| GET | `/api/achievements/me/progress` | – | `UserLevelResponse` |
| GET | `/api/achievements/me/recent` | Query: `count` (10, max 50) | `AchievementResponse[]` |
| GET | `/api/achievements/me/unseen` | – | `AchievementResponse[]` (en eski önce) |
| POST | `/api/achievements/me/seen` | `{ "achievementIds": [2, 3] }` veya `{ "all": true }` | `{ markedCount }` |
| POST | `/api/achievements/me/evaluate` | – | `AchievementEvaluationResponse` |
| GET | `/api/achievements/me/rewards` | – | `RewardResponse[]` |
| POST | `/api/achievements/events` | `{ clientEventId, eventType, eventValue? }` | `{ clientEventId, eventType, status, gamification? }` |
| GET | `/api/users/me/preferences` | – | `PreferencesResponse` |
| PUT | `/api/users/me/preferences` | aşağıda | `PreferencesResponse` + `gamification` |
| GET | `/api/cosmetics` | Query: `type` (opsiyonel `RewardType`) | `CosmeticItemResponse[]` |
| POST | `/api/hydration/intakes` | mevcut alanlar + opsiyonel `source` | `IntakeResultResponse` (+ `gamification`) |
| POST | `/api/hydration/intakes/sync` | `{ intakes: [AddIntakeRequest] }` | `SyncIntakesResponse` (+ `gamification`) |

```jsonc
// PUT /api/users/me/preferences  (tam nesne; null = seçim yok, bottleTheme zorunlu)
{ "colorScheme": "Dark", "bottleTheme": "BOTTLE_OCEAN", "profileFrame": null,
  "waterAnimation": "ANIMATION_BUBBLES", "badgeEffect": null, "mascotAccessory": "MASCOT_SUNGLASSES" }

// PreferencesResponse
{ "colorScheme": "Dark", "bottleTheme": CosmeticItemResponse, "profileFrame": null, "waterAnimation": CosmeticItemResponse,
  "badgeEffect": null, "mascotAccessory": CosmeticItemResponse, "updatedAt": "...", "gamification": { ... } }
```

`colorScheme`: `System` | `Light` | `Dark`. `rewardType`: `BottleTheme` | `ProfileFrame` | `WaterAnimation` | `BadgeEffect` | `MascotAccessory`.

#### Event isimleri

`APP_OPENED`, `ONBOARDING_COMPLETED`, `STATISTICS_OPENED`, `HISTORY_OPENED`, `WEEKLY_REPORT_OPENED`, `MONTHLY_REPORT_OPENED`, `CALENDAR_DAY_VIEWED`, `HEALTH_RECOMMENDATION_VIEWED`, `DARK_THEME_ENABLED`, `BOTTLE_THEME_USED`, `BOTTLE_EASTER_EGG_DISCOVERED`. Sayım kuralları yukarıdaki "Mobil olay sözleşmesi" tablosundadır.

#### Hata kodları (başarım / kozmetik)

| HTTP | errorCode | Ne zaman |
|---|---|---|
| 400 | `invalidAchievementEvent` | Tanımsız / sunucuya özel event tipi, geçersiz veya sahip olunmayan `eventValue` |
| 400 | `validationFailed` | Eksik `clientEventId`, boş `seen` isteği, eksik `bottleTheme` / `colorScheme` |
| 400 | `invalidReward` | Seçilen ödül o alanın türünde değil (ör. çerçeveyi şişe teması olarak seçmek) |
| 403 | `rewardLocked` | Henüz kazanılmamış kozmetik seçildi |
| 404 | `rewardNotFound` | Bilinmeyen ödül kodu |
| 429 | `tooManyRequests` | Event limiti aşıldı |

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

**Başarımlar**
- Uygulama açıldığında: `POST /api/achievements/events` (`APP_OPENED`), ardından `GET /api/achievements/me/unseen`. Gelen her başarım için animasyon oynatın, sonra `POST /api/achievements/me/seen` ile işaretleyin.
- Su ekleme / sync / düzenleme yanıtındaki `gamification.newlyUnlocked` ve `newRewards` ile anında animasyon gösterebilirsiniz; `leveledUp` true ise seviye atlama ekranı gösterin. Aynı başarım `unseen` listesinde de görünür; `seen` ile işaretlenene kadar çift göstermemek için kimlikleri (`id`) takip edin.
- Rozet ekranı: `GET /api/achievements?category=...` (`currentValue / targetValue / progressPercentage` hazır gelir). `code` değeri `null` olan öğeler kilitli gizli başarımlardır; kilitli placeholder gösterin.
- Profil: `GET /api/achievements/me/summary` ve `GET /api/achievements/me/progress` (seviye çubuğu için hesap gerekmez).
- Ekran olaylarını her ekran açılışında yeni bir `clientEventId` ile gönderebilirsiniz; aynı gün tekrarları sunucu sayar ama ilerlemeyi şişirmez. Ağ hatasında aynı `clientEventId` ile tekrar deneyin.
- Bildirimdeki "İçtim" aksiyonu su eklerken `"source": "ReminderNotification"` göndermelidir.
- Ödüller: `GET /api/achievements/me/rewards` → `assetKey` ile uygulama içindeki tema / çerçeve / animasyon dosyasını eşleyin.

**Geliştirme ortamında bağlantı**
- iOS simülatörü: `http://localhost:5290`
- Android emülatörü: `http://10.0.2.2:5290`
- Gerçek cihaz: bilgisayarınızın yerel IP'si (örn. `http://192.168.1.20:5290`). Gerekirse `ASPNETCORE_URLS=http://0.0.0.0:5290` ile çalıştırın.
- Native uygulamalar CORS'tan etkilenmez; `Cors:AllowedOrigins` yalnızca Expo Web gibi tarayıcı istemcileri içindir.
