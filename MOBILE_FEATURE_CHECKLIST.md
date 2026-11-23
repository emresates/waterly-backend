# Waterly — Mobil Özellik Kontrol Listesi (Başarımlar)

Backend'de hazır olan ama kazanılabilmesi için **mobil uygulamada** iş gerektiren başarımlar. Bir madde mobilde yayınlanana kadar ilgili başarım kazanılamaz; `ACHIEVEMENT_MATRIX.md` içinde bu başarımlar `MOBILE_EVENT_PENDING` veya `MOBILE_UI_PENDING` olarak işaretlidir.

Bütün istekler `Authorization: Bearer <accessToken>` ister. Yanıtlar standart `ApiResponse` formatındadır (`success`, `message`, `statusCode`, `errorCode`, `data`). Request ve response ayrıntıları için `README.md` → "Mobil entegrasyon sözleşmesi".

## Ortak event kuralı

```http
POST /api/achievements/events
{ "clientEventId": "<uuid>", "eventType": "<TYPE>", "eventValue": "<opsiyonel>" }
```

- Her kullanıcı etkileşimi için **yeni** bir `clientEventId` (UUID v4) üretin. Ağ hatasında **aynı** id ile tekrar deneyin.
- `status` değeri `Recorded`, `AlreadyProcessed` veya `AlreadyCounted` olabilir; üçü de başarılıdır, kullanıcıya hata göstermeyin.
- Limit: kullanıcı başına dakikada 30 event. `429 tooManyRequests` alınırsa event'i sessizce atabilirsiniz; kritik veri değildir.
- Çevrimdışıyken event'leri kuyruğa almak gerekmez. Ekran tekrar açıldığında yeniden gönderilir. Sunucu günü kendi saatine göre belirler.
- `data.gamification.newlyUnlocked` doluysa başarım animasyonunu gösterin.

---

## 1. Uygulama açılışı / ön plana gelme

| | |
|---|---|
| Ekran | Uygulama başlangıcı, arka plandan dönüş |
| Etkileşim | Uygulama açıldığında veya 30 dk'dan uzun süre sonra ön plana geldiğinde |
| Event | `APP_OPENED` |
| Başarımlar | `ACTIVE_DAYS_7`, `SECRET_RETURN_14_DAYS`, `SECRET_COMEBACK_3_DAYS` |
| API | `POST /api/achievements/events` → ardından `GET /api/achievements/me/unseen` |

Token yenileme (`POST /api/auth/refresh`) de günü aktif sayar; `APP_OPENED` ise refresh gerekmeyen açılışları da yakalar.

## 2. Onboarding

| | |
|---|---|
| Ekran | Onboarding akışının son adımı ("Başla" butonu) |
| Event | `ONBOARDING_COMPLETED` (bir kez sayılır) |
| Başarımlar | `ONBOARDING_COMPLETED` |
| API | `POST /api/achievements/events` |

## 3. İstatistik, geçmiş ve rapor ekranları

| Ekran | Event | Başarımlar | Not |
|---|---|---|---|
| İstatistik ekranı | `STATISTICS_OPENED` | `STATS_FIRST_VIEW` | Günde 1 sayılır |
| Geçmiş listesi | `HISTORY_OPENED` | `HISTORY_FIRST_VIEW` | Günde 1 |
| Haftalık rapor | `WEEKLY_REPORT_OPENED` | `WEEKLY_REPORT_WEEKS_4`, `WEEKLY_REPORT_WEEKS_5` | ISO takvim haftası başına 1 |
| Aylık rapor | `MONTHLY_REPORT_OPENED` | `MONTHLY_REPORT_FIRST` | Günde 1 |
| Sağlık önerisi | `HEALTH_RECOMMENDATION_VIEWED` | `HEALTH_RECOMMENDATION_VIEW` | Günde 1 |

Event'i ekranın görünür olduğu anda (ör. `useFocusEffect`) gönderin. GET isteğine eklemeyin; backend GET'lerde yan etki üretmez.

## 4. Takvim ve gün detayı (yeni UI olabilir)

| | |
|---|---|
| Ekran | Takvim görünümü → geçmiş bir güne dokunup detay açma |
| Event | `CALENDAR_DAY_VIEWED`, `eventValue` = açılan gün `"yyyy-MM-dd"` (opsiyonel; gönderilirse bugünden önce olmalı) |
| Başarımlar | `CALENDAR_DAY_VIEW` |
| API | Gün verisi için `GET /api/statistics/daily?date=yyyy-MM-dd` veya `GET /api/hydration/intakes?from=...&to=...`, sonra event |

## 5. Bildirimdeki "İçtim" aksiyonu (yeni UI olabilir)

| | |
|---|---|
| Ekran | Yerel hatırlatma bildirimi (expo-notifications action button) |
| Etkileşim | Kullanıcı bildirimdeki "İçtim" butonuna basar |
| Event | Yok; su kaydında `"source": "ReminderNotification"` gönderilir |
| Başarımlar | `REMINDER_ACTION_FIRST`, `REMINDER_ACTION_DAYS_10`, `REMINDER_ACTION_DAYS_30` |
| API | `POST /api/hydration/intakes` `{ amountMl: defaultGlassMl, clientEventId, consumedAt, timeZone, source: "ReminderNotification" }`. Çevrimdışıysa aynı öğe `POST /api/hydration/intakes/sync` ile gönderilir. |

Aksiyon bir kez basıldığında **bir** `clientEventId` üretin ve tüm tekrarlarda aynısını kullanın.

## 6. Sağlık bağlantısı

| | |
|---|---|
| Ekran | Ayarlar → Apple Health / Health Connect izni |
| Etkileşim | İzin verildikten sonra son 7 günün özetini yükleme |
| Başarımlar | `HEALTH_CONNECTED` (adım veya egzersiz > 0 olan ilk gerçek veri) |
| API | `POST /api/health/activity` `{ source: "AppleHealth" \| "HealthConnect", days: [...] }` |

## 7. Karanlık mod (yeni UI)

| | |
|---|---|
| Ekran | Ayarlar → Görünüm: Sistem / Açık / Koyu |
| Etkileşim | Kullanıcı "Koyu"yu seçer |
| Event | Gerekmez; tercih kaydı sunucuda sayılır. (`DARK_THEME_ENABLED` event'i de kabul edilir.) |
| Başarımlar | `SECRET_DARK_MODE` (bir kez) |
| API | `GET /api/users/me/preferences` → değişikliği uygula → `PUT /api/users/me/preferences` (tam nesne) |

Uygulama açılışında `GET /api/users/me/preferences` ile tercihleri geri yükleyin; telefon değişse de tema ve görünüm hesapla birlikte gelir.

## 8. Şişe tema seçici (yeni UI)

| | |
|---|---|
| Ekran | Profil / Görünüm → "Şişe Temaları" ızgarası |
| Etkileşim | Kilitli temalar kilit ikonu ve koşulla (`requiredLevel` / `requiredAchievementCode`) gösterilir; açık temalar seçilebilir |
| Event | Gerekmez; seçim (`PUT`) sunucuda tema başına bir kez sayılır |
| Başarımlar | `SECRET_THEMES_3` (3 farklı tema seçmek) |
| API | `GET /api/cosmetics?type=BottleTheme` → `PUT /api/users/me/preferences` `{ ..., bottleTheme: "BOTTLE_OCEAN" }` |

- Başlangıç temaları herkese açıktır: `BOTTLE_CLASSIC` (varsayılan), `BOTTLE_OCEAN`, `BOTTLE_MINT`.
- Seviye temaları: `BOTTLE_SKY_BLUE` (5), `BOTTLE_FROST` (20), `BOTTLE_GOLD` (50), `BOTTLE_MYTHIC` (100).
- Kilitli tema seçilirse `403 rewardLocked` döner. UI kilitli temayı zaten seçilemez göstermeli.
- `assetKey` (ör. `bottle_ocean`) uygulamaya gömülü görsel/Lottie dosyasının adıdır.
- Aynı ekranda çerçeve (`profileFrame`), su animasyonu (`waterAnimation`), rozet efekti (`badgeEffect`) ve maskot aksesuarı (`mascotAccessory`) için de seçim yapılabilir.

## 9. Şişe easter egg'i (yeni UI)

| | |
|---|---|
| Ekran | Ana ekrandaki animasyonlu su şişesi |
| Etkileşim | 5 saniye içinde 10 dokunuş → özel animasyon oynatılır |
| Event | `BOTTLE_EASTER_EGG_DISCOVERED` (animasyon ilk kez oynatıldığında; sonraki gönderimler `AlreadyCounted` döner) |
| Başarımlar | `SECRET_BOTTLE_TAPS_10` (bir kez) |
| API | `POST /api/achievements/events` |

Dokunuş sayımı tamamen istemcidedir. Sunucu yalnızca keşfi bir kez kaydeder.

## 10. Başarım ve ödül ekranları (gösterim)

| Ekran | API |
|---|---|
| Rozet koleksiyonu | `GET /api/achievements?category=&rarity=&unlocked=&page=&pageSize=`; `code: null` = kilitli gizli başarım, placeholder gösterin |
| Profil başlığı (seviye çubuğu) | `GET /api/achievements/me/progress`, `GET /api/achievements/me/summary` |
| Kazanım animasyonu | Su / event / tercih yanıtlarındaki `gamification`, ayrıca `GET /api/achievements/me/unseen` → gösterildikten sonra `POST /api/achievements/me/seen` |
| Ödüllerim | `GET /api/achievements/me/rewards` veya `GET /api/cosmetics` |

## Mobil gerektirmeyen başarımlar

Diğer başarımlar (su, seri, hedef, hacim, bardak, alışkanlık saatleri, hatırlatma ayarları, saat dilimi, meta) mevcut ekranların kullandığı API'lerden sunucuda otomatik hesaplanır. Ek mobil iş gerekmez; yalnızca yanıtlardaki `gamification` alanının gösterilmesi önerilir.
