# Trafo Merkezi Dijital İkizi Test Adımları

Bu doküman, Unity 6 + MQTT tabanlı trafo merkezi dijital ikiz projesinin demo ve doğrulama testlerini adım adım açıklar. Amaç yalnızca Python scriptlerinin MQTT mesajı yayınladığını görmek değil, Unity tarafında SCADA/HMI, 3B dijital ikiz, alarm paneli, duman, alarm ışığı, alarm sesi ve kamera focus davranışlarının doğru çalıştığını doğrulamaktır.

## 1. Test Kapsamı

Test edilecek ana başlıklar:

- MQTT broker bağlantısı
- Unity Play Mode başlangıç durumu
- SCADA/HMI ekranının normal operasyon görünümü
- Yetkisiz kesici açma/kapama saldırısı
- Cooling false data injection saldırısı
- Trafo yağ seviyesi / yağ sıcaklığı kritik alarmı
- Bara gerilim dengesizliği / voltage sag senaryosu
- Alarm ışığı, alarm sesi, duman ve kamera focus davranışı
- Restore / normale dönüş davranışı

## 2. Ön Koşullar

Gerekli yazılımlar:

- Unity 6 Editor
- Python 3.11 veya uyumlu Python sürümü
- Mosquitto MQTT broker
- Python `paho-mqtt` paketi

Python paketi kontrolü:

```powershell
pip install paho-mqtt
```

Windows PowerShell içinde Mosquitto klasöründeysen komutları başında `.\` ile çalıştırmak gerekir:

```powershell
cd "C:\Program Files\Mosquitto"
.\mosquitto.exe -v
```

Ayrı bir PowerShell penceresinde topic dinleme testi:

```powershell
cd "C:\Program Files\Mosquitto"
.\mosquitto_sub.exe -h localhost -t substation/# -v
```

PowerShell `mosquitto_sub` komutunu doğrudan tanımazsa bu hata normaldir. Windows, bulunduğun klasördeki exe dosyasını otomatik çalıştırmaz; `.\mosquitto_sub.exe` kullanılmalıdır.

## 3. Genel Başlatma Testi

1. Mosquitto brokerı başlat:

```powershell
cd "C:\Program Files\Mosquitto"
.\mosquitto.exe -v
```

2. Unity projesini aç:

```text
TrafoDigitalTwin
```

3. `Assets/Scenes/SampleScene.unity` sahnesinin açık olduğundan emin ol.

4. Unity Play Mode başlat.

5. Unity Console içinde MQTT bağlantı loglarını kontrol et:

```text
[CoolingFalseDataReceiver] Connected to MQTT broker 127.0.0.1:1883, subscribed to substation/#
[BreakerMQTTReceiver] Connected to MQTT broker localhost:1883, subscribed to substation/breaker/control
```

6. SCADA/HMI ekranında normal başlangıç durumunu kontrol et:

```text
System Mode: NORMAL OPERATION
Circuit Breaker: CLOSED
IED Trip Status: NO TRIP
Security: No attack
Alarm Panel: aktif kritik alarm yok
```

7. Eğer normal sensör simülasyon scripti projede mevcutsa ayrı terminalde çalıştır. Script adı projedeki güncel dosya adına göre değişebilir; amaç Unity tarafının normal MQTT telemetrisini almaya devam ettiğini doğrulamaktır.

## 4. MQTT Yayın Alma Testi

Bu test, Python veya Unity tarafına geçmeden brokerın mesaj taşıdığını doğrular.

1. Bir terminalde subscriber aç:

```powershell
cd "C:\Program Files\Mosquitto"
.\mosquitto_sub.exe -h localhost -t substation/# -v
```

2. İkinci terminalde test mesajı yayınla:

```powershell
cd "C:\Program Files\Mosquitto"
.\mosquitto_pub.exe -h localhost -t substation/test -m "hello"
```

3. Subscriber terminalinde şu satır görülmelidir:

```text
substation/test hello
```

Bu test başarısızsa Unity tarafını test etmeden önce broker kurulumu veya firewall kontrol edilmelidir.

## 5. Senaryo 1: Yetkisiz Kesici Açma/Kapama

Amaç: MQTT üzerinden kesiciye yetkisiz `OPEN` veya `CLOSE` komutu gönderildiğinde dijital ikizde kesici hareketi, SCADA alarmı ve kamera focus davranışını doğrulamak.

Komut:

```powershell
cd "C:\Users\Amil\Desktop\BitirmeÇalışması\proje\TrafoDigitalTwin"
python Assets\_Scripts\AttackScripts\breaker_attack.py
```

Menü:

```text
1 - Unauthorized OPEN
2 - Unauthorized CLOSE
3 - Normal Operator OPEN
4 - Normal Operator CLOSE
```

Test adımları:

1. Unity Play Mode açıkken `1` seç.
2. Python terminalinde şu payload yayınlanmalıdır:

```json
{
  "breakerId": "BRK-01",
  "command": "OPEN",
  "source": "unauthorized"
}
```

3. Unity tarafında beklenen sonuçlar:

```text
Breaker Status: OPEN
Command Source: unauthorized
Alarm: CRITICAL ALERT: Unauthorized Breaker Operation Detected
Terminal log: BREAKER OPEN COMMAND RECEIVED
Terminal log: SOURCE: UNAUTHORIZED
Terminal log: CRITICAL: UNAUTHORIZED SWITCHING ATTACK DETECTED
```

4. Dijital ikizde beklenen fiziksel sonuçlar:

- `circuit_breaker` modeline kamera focus yapar.
- `SigortaSalter` açık pozisyona geçer.
- Enerji hattı veya kesici göstergesi kırmızı/pasif görünür.
- Terminal penceresi kapanır ve kullanıcı 3 saniyelik focus sonrasında freecam kontrolüne döner.

5. Aynı script içinde `2` seç:

```text
Unauthorized CLOSE
```

6. Beklenen sonuç:

- Kesici kapanır.
- `source = unauthorized` olduğu için olay yine saldırı olarak loglanır.
- SCADA alarm panelinde kritik alarm davranışı korunur.

7. Normal operatör davranışı için `3` veya `4` seç:

- Komut işlenir.
- Kesici açılır/kapanır.
- `source = operator` olduğu için kritik siber saldırı alarmı üretilmez.

Başarılı kabul kriteri:

- Yetkisiz komutlarda IDS alarmı oluşur.
- Operatör komutlarında normal işlem logu oluşur.
- Kesici görsel hareketi ve kamera focus gözlemlenir.

## 6. Senaryo 2: Cooling False Data Injection

Amaç: Gerçek trafo sıcaklığı yüksekken SCADA’ya sahte normal sıcaklık gösterilmesini, soğutmanın kapatılmasını ve fiziksel alarm zincirini doğrulamak.

Komut:

```powershell
python Assets\_Scripts\AttackScripts\cooling_false_data_attack.py
```

Menü:

```text
1 - Start Cooling False Data Attack
2 - Stop Attack / Restore Normal
q - Quit
```

Test adımları:

1. `1` seç.
2. MQTT mesajlarının yayınlandığını kontrol et:

```text
substation/attack/type = cooling_false_data
substation/cooling/control = OFF
substation/sensor/temperature/fake = 42
substation/transformer/temperature/real = 95
substation/effect/smoke = ON
substation/alarm/suppression = ON
```

3. Unity ve SCADA tarafında beklenen sonuçlar:

```text
Cooling system: OFF
Fake temperature: 42 C
Real temperature: 95 C
Data Integrity Attack Detected
Critical transformer temperature alarm active
```

4. Dijital ikizde beklenen fiziksel sonuçlar:

- Trafo bölgesinde duman efekti oluşur.
- `Alarm-Light` kırmızı yanıp söner.
- Alarm sesi çalar.
- Kamera 5 saniye `TransformerSmoke` bölgesine focus yapar.
- 5 saniye sonunda kamera/freecam kullanıcıya geri döner.

5. `2` seç.
6. Beklenen restore sonucu:

```text
Cooling system restored
Real temperature: 45 C
Fake temperature: 42 C
Smoke OFF
Alarm light OFF / eski durumuna döndü
Alarm sound stopped
```

Başarılı kabul kriteri:

- SCADA sahte ve gerçek sıcaklık farkını gösterir.
- Veri bütünlüğü saldırısı loglanır.
- Duman, ışık, ses ve kamera focus birlikte çalışır.
- Restore sonrası alarm etkileri kapanır.

## 7. Senaryo 3: Trafo Yağ Seviyesi / Yağ Sıcaklığı Kritik Alarmı

Amaç: Yağ sıcaklığı yükseldiğinde, yağ seviyesi düştüğünde ve Buchholz rölesi uyarı verdiğinde SCADA ve dijital ikiz davranışlarını doğrulamak.

Komut:

```powershell
python Assets\_Scripts\AttackScripts\oil_critical_alarm_attack.py
```

Menü:

```text
1 - Start Oil Temperature / Buchholz Alarm
2 - Stop Scenario / Restore Normal
q - Quit
```

Test adımları:

1. `1` seç.
2. MQTT mesajlarını doğrula:

```text
substation/attack/type = oil_critical_alarm
substation/transformer/oil_temperature = 105
substation/transformer/oil_level = 22
substation/protection/buchholz = WARNING
substation/transformer/oil_alarm = ON
```

3. Unity ve SCADA tarafında beklenen loglar:

```text
OIL TEMP HIGH
BUCHHOLZ RELAY WARNING
Oil temperature: 105 C
Oil level: 22%
Transformer Oil Critical Alarm Attack Detected
```

4. Dijital ikizde beklenen fiziksel sonuçlar:

- Trafo kırmızı alarm materyali/emission etkisine geçer.
- Duman efekti görünür.
- `Alarm-Light` kırmızı yanıp söner.
- Alarm sesi çalar.
- Kamera 5 saniye trafo/duman bölgesine focus yapar.

5. `2` seç.
6. Beklenen restore mesajları:

```text
substation/transformer/oil_temperature = 55
substation/transformer/oil_level = 78
substation/protection/buchholz = CLEAR
substation/transformer/oil_alarm = OFF
substation/effect/smoke = OFF
substation/attack/type = none
```

7. SCADA log:

```text
Oil critical alarm cleared
Oil temperature: 55 C
Oil level: 78%
```

Başarılı kabul kriteri:

- Yağ sıcaklığı ve seviyesi SCADA loglarına doğru yansır.
- Buchholz warning alarmı görülür.
- Duman/ışık/ses/focus aktif olur.
- Restore sonrası trafo normal görünüme döner.

## 8. Senaryo 4: Bara Gerilim Dengesizliği / Voltage Sag

Amaç: A/B/C faz gerilimleri bozulduğunda SCADA’da faz dengesizliği, düşük gerilim veya aşırı gerilim alarmının doğru oluştuğunu doğrulamak.

Komut:

```powershell
python Assets\_Scripts\AttackScripts\voltage_sag_attack.py
```

Menü:

```text
1 - Start Voltage Sag + Phase Imbalance
2 - Start Over Voltage + Phase Imbalance
3 - Stop Scenario / Restore Normal
q - Quit
```

### 8.1. Voltage Sag Testi

1. `1` seç.
2. MQTT mesajlarını doğrula:

```text
substation/attack/type = voltage_sag
substation/busbar/voltage/a = 24.8
substation/busbar/voltage/b = 31.6
substation/busbar/voltage/c = 35.2
substation/busbar/voltage_alarm = SAG
```

3. Unity console ve SCADA terminalde beklenen loglar:

```text
BUSBAR VOLTAGE SAG / IMBALANCE DETECTED
Voltage A: 24.8 kV
Voltage B: 31.6 kV
Voltage C: 35.2 kV
UNDER VOLTAGE ALARM
IED VOLTAGE PROTECTION WARNING
```

4. SCADA/HMI beklenen durum:

```text
System Mode: CYBER SECURITY EVENT
Busbar: FAULT
Alarm Panel: BUSBAR VOLTAGE SAG / IMBALANCE
Alarm Panel: UNDER VOLTAGE ALARM
```

5. Dijital ikizde beklenen fiziksel sonuçlar:

- Alarm ışığı yanıp söner.
- Alarm sesi çalar.
- Duman efekti görünür.
- Kamera 5 saniye focus yapar.
- 5 saniye sonunda freecam kontrolü geri gelir.

### 8.2. Over Voltage Testi

1. Aynı script içinde `2` seç.
2. MQTT mesajlarını doğrula:

```text
substation/attack/type = voltage_over
substation/busbar/voltage/a = 39.8
substation/busbar/voltage/b = 35.4
substation/busbar/voltage/c = 34.8
substation/busbar/voltage_alarm = CRITICAL
```

3. Beklenen log:

```text
OVER VOLTAGE ALARM
IED VOLTAGE PROTECTION WARNING
```

4. `3` seç ve sistemi normale döndür.

Restore mesajları:

```text
substation/busbar/voltage/a = 34.5
substation/busbar/voltage/b = 34.5
substation/busbar/voltage/c = 34.5
substation/busbar/voltage_alarm = OFF
substation/effect/smoke = OFF
substation/attack/type = none
```

Restore sonrası beklenen log:

```text
Busbar voltage alarm cleared
Voltage A/B/C: 34.5/34.5/34.5 kV
```

Başarılı kabul kriteri:

- Voltage sag durumunda `UNDER VOLTAGE ALARM` görülür.
- Over voltage durumunda `OVER VOLTAGE ALARM` görülür.
- Busbar SCADA durum göstergesi alarm durumuna geçer.
- Restore sonrası alarm, duman, ses ve ışık kapanır.

## 9. Alarm Işığı ve Ses Testi

Bu test tüm duman/alarm tabanlı senaryolarda ortak davranışı doğrular.

1. Unity sahnesinde `Alarm-Light` nesnesinin var olduğunu kontrol et.
2. Başlangıçta inactive veya düşük yoğunlukta olabilir; bu normaldir.
3. Cooling FDI, oil critical veya voltage sag senaryolarından birini başlat.
4. Beklenen durum:

```text
Alarm-Light enabled = true
Light color = red
Light intensity = blinking between low/high values
AudioSource isPlaying = true
```

5. Restore komutunu çalıştır.
6. Beklenen durum:

```text
Alarm-Light eski active/enabled/color/intensity durumuna döner
AudioSource isPlaying = false
```

## 10. Kamera Focus Testi

Kesici senaryosu:

- Kamera `circuit_breaker` modeline yaklaşır.
- Terminal kapanır.
- Yaklaşık 3 saniye sonra kamera/freecam kullanıcıya geri döner.

Duman/trafo/voltage senaryoları:

- Kamera `TransformerSmoke` bölgesine yaklaşır.
- Duman, ışık ve alarm etkileri görünür olmalıdır.
- Yaklaşık 5 saniye sonra kamera/freecam kullanıcıya geri döner.

Başarılı kabul kriteri:

- Kamera duvarın içinde kalmaz.
- Focus sırasında hedef bileşen ekranda görünür.
- Focus süresi sonunda kullanıcı hareket kontrolünü geri alır.
- Restore komutu kamera focusu yeniden başlatmaz.

## 11. Genel Hata Giderme

### 11.1. Python Mesaj Yayınlıyor Ama Unity Değişmiyor

Kontrol et:

```powershell
cd "C:\Program Files\Mosquitto"
.\mosquitto_sub.exe -h localhost -t substation/# -v
```

Python scripti tekrar çalıştır. Subscriber mesaj görüyorsa broker çalışıyordur. Unity değişmiyorsa Unity Console’da bağlantı loglarını ve hata mesajlarını kontrol et.

### 11.2. Mosquitto Komutu Tanınmıyor

PowerShell içinde Mosquitto klasöründeysen:

```powershell
.\mosquitto.exe -v
.\mosquitto_sub.exe -h localhost -t substation/# -v
```

Başında `.\` olmadan PowerShell aynı klasördeki exe dosyasını çalıştırmaz.

### 11.3. Duman Görünmüyor

Kontrol et:

- Unity Play Mode açık mı?
- Saldırı scriptinde `ON` komutu gönderildi mi?
- `TransformerSmoke` sahnede var mı?
- Kamera focus sonrası duman bölgesini gösteriyor mu?
- Restore komutu daha önce çalıştırılıp dumanı kapatmış olabilir mi?

### 11.4. Alarm Sesi Duyulmuyor

Kontrol et:

- Unity Game view aktif mi?
- Bilgisayar ses seviyesi açık mı?
- Unity AudioListener aktif mi?
- Saldırı aktifken `FDI_SmokeEffectController` runtime objesinde AudioSource oluşuyor mu?

### 11.5. Kamera Focus Yanlış Yere Gidiyor

Kontrol et:

- Kesici için `circuit_breaker` nesnesi sahnede doğru yerde mi?
- Kesici altındaki `SigortaSalter` modeli doğru child olarak duruyor mu?
- Duman senaryoları için `TransformerSmoke` referans noktası doğru yerde mi?
- Play Mode öncesi sahne kaydedildi mi?

## 12. Sunum İçin Önerilen Demo Sırası

Sunumda en temiz akış:

1. Normal SCADA/HMI ekranını göster.
2. Kesici saldırısını çalıştır:

```powershell
python Assets\_Scripts\AttackScripts\breaker_attack.py
```

3. `1 - Unauthorized OPEN` seç ve kesici animasyonunu göster.
4. `2 - Unauthorized CLOSE` seç ve saldırı logunun devam ettiğini göster.
5. Cooling FDI saldırısını çalıştır:

```powershell
python Assets\_Scripts\AttackScripts\cooling_false_data_attack.py
```

6. Duman, ışık, ses ve kamera focus davranışını göster.
7. Oil critical alarm senaryosunu çalıştır:

```powershell
python Assets\_Scripts\AttackScripts\oil_critical_alarm_attack.py
```

8. `OIL TEMP HIGH` ve `BUCHHOLZ RELAY WARNING` loglarını göster.
9. Voltage sag senaryosunu çalıştır:

```powershell
python Assets\_Scripts\AttackScripts\voltage_sag_attack.py
```

10. `UNDER VOLTAGE ALARM`, Busbar `FAULT` ve alarm panelini göster.
11. Her senaryo sonunda restore komutu çalıştır ve sistemin normale döndüğünü göster.

Bu sıra, projenin üç ana değerini net gösterir: MQTT haberleşmesi, SCADA alarm mantığı ve dijital ikizde fiziksel karşılık.

