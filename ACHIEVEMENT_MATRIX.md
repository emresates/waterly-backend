# Waterly — Başarım Uyumluluk Matrisi

76 başarımın her biri için: hangi özelliğe dayandığı, neyle tetiklendiği, backend'in hangi veriden hesapladığı, mobilin ne göndermesi gerektiği ve durumu.

`AchievementFeatureTests.EveryAchievementInTheCatalog_CanActuallyBeUnlocked` testi 76 başarımın tamamının gerçek servis akışlarıyla açılabildiğini doğrular. `CompatibilityMatrix_ListsEveryAchievementExactlyOnce` testi bu tablonun katalogla senkron kalmasını sağlar.

## Gruplar

| Grup | Anlamı |
|---|---|
| **1** | Mevcut backend verilerinden otomatik hesaplanır (su, hedef, hatırlatma, sağlık kayıtları) |
| **2** | Mevcut backend servislerine küçük değişiklik gerektirdi (oturum kaydı, `source` alanı, aktif gün takibi) |
| **3** | Mobil uygulamanın event göndermesini gerektirir (`POST /api/achievements/events`) |
| **4** | Yeni mobil UI özelliği gerektirir |
| **5** | Yeni veritabanı modeli veya API endpoint'i gerektirdi |

## Durum değerleri

| ImplementationStatus | Anlamı |
|---|---|
| **BACKEND_DONE** | Backend hazır ve test edildi. Mobilde mevcut API çağrılarından (kayıt/giriş, su ekleme, hedef, hatırlatma, sağlık senkronu) başka bir şey gerekmez. |
| **BACKEND_DONE · MOBILE_EVENT_PENDING** | Backend hazır. Mobil uygulamanın ilgili ekranda event göndermesi gerekir; gönderilene kadar başarım kazanılamaz. |
| **BACKEND_DONE · MOBILE_UI_PENDING** | Backend hazır. Mobilde henüz olmayabilecek bir UI özelliği gerekir (bkz. `MOBILE_FEATURE_CHECKLIST.md`). Mobil UI yayınlanana kadar **tamamlandı sayılmaz**. |

Mobil tarafın mevcut durumu bu repoda görülemediği için, mobil iş gerektiren her satır "PENDING" olarak işaretlidir.

## Matris

| AchievementCode | Grup | RequiredFeature | Trigger | BackendSource | MobileEvent | RequiredEndpoint | ImplementationStatus |
|---|---|---|---|---|---|---|---|
| `WELCOME` | 2 | Oturum açma | Kayıt / giriş / günün ilk token yenilemesi | `UserAchievementEvents` (SESSION_STARTED, sunucu kaydı) | – | `POST /api/auth/register`, `/login`, `/refresh` | `BACKEND_DONE` |
| `FIRST_DROP` | 1 | Su ekleme | İlk uygun su kaydı | `WaterIntakes` | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `FIRST_GLASS` | 1 | Su ekleme | Kayıt gününde ≥250 ml | `WaterIntakes` + `Users.CreatedAt` | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `FIRST_DAY_500` | 1 | Su ekleme | Kayıt gününde ≥500 ml | `WaterIntakes` + `Users.CreatedAt` | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `FIRST_HALF_GOAL` | 1 | Su ekleme | Bir günde hedefin %50'si | `WaterIntakes` + `HydrationGoals` | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `FIRST_GOAL` | 1 | Su ekleme | İlk tamamlanan gün | `WaterIntakes` + `HydrationGoals` | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GOAL_CUSTOMIZED` | 1 | Hedef ayarı ekranı | Hedefin gerçekten farklı bir değere değişmesi | `HydrationGoals` geçmişi | – | `PUT /api/goals/current` | `BACKEND_DONE` |
| `REMINDER_CONFIGURED` | 1 | Hatırlatma ayarı ekranı | Ayarların ilk kez kaydedilmesi | `ReminderSettings.UpdatedAt` | – | `PUT /api/reminders/settings` | `BACKEND_DONE` |
| `ONBOARDING_COMPLETED` | 3 | Onboarding akışı | Onboarding'in son adımı | `UserAchievementEvents` (once) | `ONBOARDING_COMPLETED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_EVENT_PENDING` |
| `ACTIVE_DAYS_7` | 2 | Aktif gün takibi | 7 farklı aktif gün | SESSION_STARTED + APP_OPENED + su kaydının kaydedildiği gün | `APP_OPENED` (önerilir) | `POST /api/achievements/events` | `BACKEND_DONE` |
| `STREAK_2` | 1 | Su ekleme | 2 gün üst üste hedef | `WaterIntakes` + `HydrationGoals` | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_3` | 1 | Su ekleme | 3 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_7` | 1 | Su ekleme | 7 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_10` | 1 | Su ekleme | 10 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_14` | 1 | Su ekleme | 14 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_21` | 1 | Su ekleme | 21 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_30` | 1 | Su ekleme | 30 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_50` | 1 | Su ekleme | 50 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_100` | 1 | Su ekleme | 100 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `STREAK_365` | 1 | Su ekleme | 365 gün seri | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `SECOND_CHANCE` | 1 | Su ekleme | Tamamlanan günlerden sonra ≥1 tam gün ara, sonra yeniden hedef | `WaterIntakes` + `HydrationGoals` | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GOAL_DAYS_30` | 1 | Su ekleme | Toplam 30 hedef günü | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GOAL_DAYS_100` | 1 | Su ekleme | Toplam 100 hedef günü | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GOAL_DAYS_365` | 1 | Su ekleme | Toplam 365 hedef günü | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_1000` | 1 | Su ekleme | Günlük hedefle sınırlı toplam ≥1 L | `WaterIntakes` (güne göre aggregate, hedefle sınırlı) | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_5000` | 1 | Su ekleme | ≥5 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_10000` | 1 | Su ekleme | ≥10 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_25000` | 1 | Su ekleme | ≥25 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_50000` | 1 | Su ekleme | ≥50 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_100000` | 1 | Su ekleme | ≥100 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_250000` | 1 | Su ekleme | ≥250 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_500000` | 1 | Su ekleme | ≥500 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_1000000` | 1 | Su ekleme | ≥1.000 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_2500000` | 1 | Su ekleme | ≥2.500 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_5000000` | 1 | Su ekleme | ≥5.000 L | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `TOTAL_ML_10000000` | 1 | Su ekleme | ≥10.000 L (en az ~1000 gün, hedef 10 L ile) | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_1` | 1 | Su ekleme | floor(sınırlı toplam / 250) ≥ 1 | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_5` | 1 | Su ekleme | ≥5 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_10` | 1 | Su ekleme | ≥10 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_25` | 1 | Su ekleme | ≥25 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_50` | 1 | Su ekleme | ≥50 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_100` | 1 | Su ekleme | ≥100 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_250` | 1 | Su ekleme | ≥250 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_500` | 1 | Su ekleme | ≥500 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_1000` | 1 | Su ekleme | ≥1000 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `GLASSES_5000` | 1 | Su ekleme | ≥5000 bardak | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `MORNING_FIRST` | 1 | Su ekleme | Günün ilk ≥100 ml kaydı 07:00–09:59 | `WaterIntakes` (yerel saat) | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `MORNING_DAYS_7` | 1 | Su ekleme | 7 böyle gün | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `NOON_DAYS_5` | 1 | Su ekleme | 5 gün 12:00–13:59 | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `EVENING_DAYS_7` | 1 | Su ekleme | 7 gün 18:00–20:59 | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `BALANCED_DAY_FIRST` | 1 | Su ekleme | Bir günde 06–12, 12–18, 18–22 aralıklarının hepsinde kayıt | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `BALANCED_DAYS_5` | 1 | Su ekleme | 5 dengeli gün | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `BALANCED_DAYS_14` | 1 | Su ekleme | 14 dengeli gün | aynı | – | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `REMINDER_ACTION_FIRST` | 2 | Bildirimdeki "İçtim" aksiyonu | `source = ReminderNotification` ile ilk kayıt | `WaterIntakes.Source` (yeni sütun) | – (`source` alanı) | `POST /api/hydration/intakes`, `/intakes/sync` | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `REMINDER_ACTION_DAYS_10` | 2 | Bildirimdeki "İçtim" aksiyonu | 10 farklı gün | aynı | – (`source` alanı) | aynı | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `REMINDER_ACTION_DAYS_30` | 2 | Bildirimdeki "İçtim" aksiyonu | 30 farklı gün | aynı | – (`source` alanı) | aynı | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `STATS_FIRST_VIEW` | 3 | İstatistik ekranı | Ekran açılışı | `UserAchievementEvents` (günde 1) | `STATISTICS_OPENED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_EVENT_PENDING` |
| `HISTORY_FIRST_VIEW` | 3 | Geçmiş ekranı | Ekran açılışı | `UserAchievementEvents` (günde 1) | `HISTORY_OPENED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_EVENT_PENDING` |
| `WEEKLY_REPORT_WEEKS_5` | 3 | Haftalık rapor ekranı | 5 farklı ISO haftasında açılış | `UserAchievementEvents` (ISO hafta başına 1) | `WEEKLY_REPORT_OPENED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_EVENT_PENDING` |
| `MONTHLY_REPORT_FIRST` | 3 | Aylık rapor ekranı | Ekran açılışı | `UserAchievementEvents` (günde 1) | `MONTHLY_REPORT_OPENED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_EVENT_PENDING` |
| `CALENDAR_DAY_VIEW` | 4 | Takvim ekranı + gün detayı | Geçmiş bir günün açılması | `UserAchievementEvents` (günde 1) | `CALENDAR_DAY_VIEWED` (+ opsiyonel tarih) | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `REMINDER_CUSTOMIZED` | 1 | Hatırlatma ayarı ekranı | Varsayılandan farklı ayar kaydı | `ReminderSettings` | – | `PUT /api/reminders/settings` | `BACKEND_DONE` |
| `GLASS_SIZE_CUSTOMIZED` | 1 | Hatırlatma ayarı ekranı | `defaultGlassMl ≠ 250` | `ReminderSettings.DefaultGlassMl` | – | `PUT /api/reminders/settings` | `BACKEND_DONE` |
| `HEALTH_CONNECTED` | 1 | Apple Health / Health Connect entegrasyonu | İlk gerçek (adım veya egzersiz > 0) veri | `DailyActivities` (Source ≠ Manual) | – | `POST /api/health/activity` | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `HEALTH_RECOMMENDATION_VIEW` | 3 | Sağlık önerisi ekranı | Ekran açılışı | `UserAchievementEvents` (günde 1) | `HEALTH_RECOMMENDATION_VIEWED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_EVENT_PENDING` |
| `WEEKLY_REPORT_WEEKS_4` | 3 | Haftalık rapor ekranı | 4 farklı ISO haftasında açılış | `UserAchievementEvents` (ISO hafta başına 1) | `WEEKLY_REPORT_OPENED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_EVENT_PENDING` |
| `SECRET_FIRST` | 1 | Meta | Kendisi dışında ilk gizli başarım | `UserAchievements` | – | – | `BACKEND_DONE` |
| `SECRET_DARK_MODE` | 5 | Karanlık mod tercihi (yeni `UserPreferences`) | `colorScheme = Dark` kaydı (veya event) | `UserPreferences` → `UserAchievementEvents` (once) | `DARK_THEME_ENABLED` (opsiyonel) | `PUT /api/users/me/preferences` | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `SECRET_THEMES_3` | 5 | Şişe tema seçici (yeni `UserPreferences`, `GET /api/cosmetics`) | 3 farklı sahip olunan temanın seçilmesi | `UserPreferences` → `UserAchievementEvents` (tema başına 1) | `BOTTLE_THEME_USED` (opsiyonel, yalnız sahip olunan tema) | `PUT /api/users/me/preferences`, `GET /api/cosmetics` | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `SECRET_BOTTLE_TAPS_10` | 4 | Şişe easter egg'i (5 sn'de 10 dokunuş + özel animasyon) | Animasyonun keşfi | `UserAchievementEvents` (once) | `BOTTLE_EASTER_EGG_DISCOVERED` | `POST /api/achievements/events` | `BACKEND_DONE · MOBILE_UI_PENDING` |
| `SECRET_RETURN_14_DAYS` | 2 | Aktif gün takibi (`UserProgress.LastActiveDate`) | ≥14 tam gün pasiflikten sonra aktif gün | SESSION_STARTED + APP_OPENED + su kaydı günü | `APP_OPENED` (önerilir) | `POST /api/achievements/events`, `/api/auth/refresh` | `BACKEND_DONE` |
| `SECRET_COMEBACK_3_DAYS` | 2 | Aktif gün takibi | Dönüş günü dahil 3 farklı aktif gün (arada gün kaçırmak serbest) | aynı | `APP_OPENED` (önerilir) | aynı | `BACKEND_DONE` |
| `SECRET_TIMEZONE_TRAVEL` | 1 | Su ekleme (cihaz saat dilimi) | 2 farklı IANA saat diliminde su kaydı | `WaterIntakes.TimeZone` | – (`timeZone` alanı) | `POST /api/hydration/intakes` | `BACKEND_DONE` |
| `SECRET_ACHIEVEMENTS_10` | 1 | Meta | Kendisi dışında 10 başarım | `UserAchievements` | – | – | `BACKEND_DONE` |
| `SECRET_ACHIEVEMENTS_25` | 1 | Meta | Kendisi dışında 25 başarım | `UserAchievements` | – | – | `BACKEND_DONE` |
| `SECRET_ACHIEVEMENTS_50` | 1 | Meta | Kendisi dışında 50 başarım | `UserAchievements` | – | – | `BACKEND_DONE` |

## Özet

| Grup | Adet |
|---|---|
| 1 — Otomatik | 58 |
| 2 — Küçük backend değişikliği | 7 |
| 3 — Mobil event | 7 |
| 4 — Yeni mobil UI | 2 |
| 5 — Yeni DB modeli / endpoint | 2 |
| **Toplam** | **76** |

| Durum | Adet |
|---|---|
| **BACKEND_DONE** | 61 |
| **BACKEND_DONE · MOBILE_EVENT_PENDING** | 7 |
| **BACKEND_DONE · MOBILE_UI_PENDING** | 8 |
