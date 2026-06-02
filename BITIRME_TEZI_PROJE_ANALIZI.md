# Trafo Merkezi Dijital İkizi ve MQTT Tabanlı Siber Saldırı Senaryoları

## 1. Proje Tanımı

Bu proje, Unity 6 oyun motoru üzerinde geliştirilen ve MQTT haberleşmesiyle beslenen bir trafo merkezi dijital ikizi simülasyonudur. Sistemin temel amacı, bir trafo merkezindeki SCADA/HMI, koruma rölesi, kesici, transformatör, sensör ve alarm davranışlarını görsel bir 3B ortamda temsil etmek ve siber saldırı senaryolarının fiziksel karşılıklarını gözlemlenebilir hale getirmektir.

Proje yalnızca statik bir 3B model gösterimi değildir. Python tarafında MQTT mesajları üretilir, Unity tarafında bu mesajlar alınır, SCADA ekranına aktarılır ve sahnedeki fiziksel karşılıklar görsel olarak değiştirilir. Böylece sensör verisi, kontrol komutu, alarm üretimi, kamera odaklanması, duman efekti, alarm ışığı ve alarm sesi aynı demo akışı içinde birleştirilir.

Sistemin ana hedefi şu soruya cevap vermektir:

> Bir trafo merkezinde siber saldırı sonucu oluşan veri manipülasyonu veya yetkisiz kontrol komutu, dijital ikiz ortamında nasıl görünür, nasıl izlenir ve operatöre nasıl alarm olarak sunulur?

Bu kapsamda proje, hem elektriksel altyapı bileşenlerini hem de siber güvenlik olaylarını bir araya getirir. MQTT mesajları saldırı veya normal operasyonu temsil eder; Unity sahnesi ise bu olayların fiziksel ve operasyonel sonucunu görselleştirir.

## 2. Kullanılan Teknolojiler

Projede kullanılan temel teknolojiler şunlardır:

- **Unity 6**: 3B dijital ikiz ortamı, SCADA/HMI arayüzü, kamera kontrolü, animasyon ve görsel efektler için kullanılır.
- **C#**: Unity tarafındaki davranış scriptleri, MQTT receiver yapıları, SCADA kontrol mantığı, IDS kontrolleri, alarm yönetimi ve görsel efektler için kullanılır.
- **Python**: Sensör simülasyonu ve saldırı komutlarını MQTT üzerinden yayınlamak için kullanılır.
- **MQTT**: Python ile Unity arasında hafif ve gerçek zamanlı haberleşme protokolü olarak kullanılır.
- **Mosquitto Broker**: Localhost üzerinde çalışan MQTT brokerıdır.
- **paho-mqtt**: Python saldırı scriptlerinin MQTT mesajı yayınlaması için kullanılır.
- **TextMeshPro / Unity UI**: SCADA/HMI ekranındaki metin, alarm, durum ve terminal panelleri için kullanılır.
- **URP**: Unity sahnesinin render pipeline yapısıdır.

## 3. Sistem Mimarisi

Sistem genel olarak üç katmandan oluşur:

1. **Veri ve Saldırı Üretim Katmanı**
   - Python scriptleri bu katmanda çalışır.
   - Normal sensör verileri veya saldırı mesajları MQTT topic’lerine yayınlanır.
   - Örnek scriptler:
     - `Assets/_Scripts/veri.py`
     - `Assets/_Scripts/AttackScripts/breaker_attack.py`
     - `Assets/_Scripts/AttackScripts/cooling_false_data_attack.py`
     - `Assets/_Scripts/AttackScripts/oil_critical_alarm_attack.py`
     - `Assets/_Scripts/AttackScripts/voltage_sag_attack.py`

2. **MQTT Haberleşme Katmanı**
   - Mosquitto broker `localhost:1883` üzerinde çalışır.
   - Python scriptleri mesajları broker’a yayınlar.
   - Unity C# receiver scriptleri ilgili topic’lere abone olur.
   - MQTT, SCADA sistemi ile saha cihazları arasındaki veri akışını temsil eden deneysel haberleşme altyapısıdır.

3. **Unity Dijital İkiz ve SCADA Katmanı**
   - Unity sahnesinde trafo merkezi 3B modeli, kesici modeli, SCADA/HMI paneli ve alarm sistemleri bulunur.
   - Gelen MQTT mesajları sahnedeki nesnelere ve SCADA arayüzüne uygulanır.
   - Saldırı durumunda duman, renk değişimi, kamera focus, alarm ışığı ve sesli alarm tetiklenir.

Genel veri akışı:

```text
Python Attack Script
        |
        v
MQTT Broker localhost:1883
        |
        v
Unity MQTT Receiver
        |
        v
SCADA/HMI + 3B Dijital İkiz + Alarm Sistemi
```

## 4. Unity Sahne Yapısı

Unity sahnesinde ana dijital ikiz ortamı `Assets/Scenes/SampleScene.unity` içinde yer alır. Sahne içinde trafo merkezi modeli, SCADA odası, kesici modeli, terminal paneli, duman referans noktası ve alarm ışığı gibi bileşenler bulunur.

Önemli sahne nesneleri:

- `Terminal_Canvas`
  - SCADA/HMI arayüzünün bulunduğu ana UI nesnesidir.
  - Üzerinde birçok controller script bulunur.
  - `CoolingFalseDataReceiver`, `BreakerController`, `BreakerMQTTReceiver`, `SCADAHMIController`, `AlarmPanelController`, `SCADATerminalController` gibi scriptler bu yapı ile ilişkilidir.

- `MQTT_Manager`
  - Geleneksel sensör MQTT verilerini alan scriptleri barındırır.

- `circuit_breaker`
  - Sahnedeki 3B kesici modelidir.
  - Kesici saldırısı sırasında kamera bu nesneye focus yapar.

- `SigortaSalter`
  - `circuit_breaker` altında bulunan şalter parçasıdır.
  - Açma/kapama durumunda hareket ettirilir.

- `TransformerSmoke`
  - Duman efektinin konum referansı olarak kullanılır.
  - `FDISmokeEffectController`, dumanı bu nesnenin pozisyonuna göre oluşturur.

- `Alarm-Light`
  - Saldırı sırasında yanıp sönen alarm ışığıdır.
  - Başlangıçta inactive olsa bile script tarafından bulunur, aktif edilir, kırmızı renkte yanıp söner ve saldırı bitince eski durumuna döner.

## 5. SCADA/HMI Arayüzü

SCADA/HMI arayüzü trafo merkezinin operasyonel durumunu gösteren paneldir. Arayüz şu görevleri yerine getirir:

- Trafo, bara, kesici, IED, CT sensörü, ağ anahtarı ve SCADA sunucusu gibi bileşenlerin durumlarını gösterir.
- IED koruma rölesinin akım, trip threshold, trip status, GOOSE stNum/sqNum ve güvenlik durumunu gösterir.
- Kesici durumunu `OPEN` / `CLOSED` olarak gösterir.
- Kesici kontrol komutunun kaynağını `operator` veya `unauthorized` olarak gösterir.
- Son komut zamanını gösterir.
- Alarm panelinde aktif alarm sayısını ve alarm mesajlarını gösterir.
- Terminal/console bölümünde olay loglarını listeler.

SCADA/HMI paneli `TerminalUIBuilder.cs` tarafından programatik olarak oluşturulur. Bu script, UI elemanlarını yaratır ve ilgili controller scriptlerine referansları bağlar.

SCADA tarafındaki önemli scriptler:

- `SCADAHMIController.cs`
  - SCADA bileşenlerinin genel durumunu yönetir.
  - IED, kesici, alarm paneli ve MQTT receiver bağlantılarını koordine eder.

- `SCADATerminalController.cs`
  - Terminal komutlarını işler.
  - Harici olay satırlarını terminale yazdırır.

- `AlarmPanelController.cs`
  - Alarm listesini yönetir.
  - Kritik, uyarı ve bilgi seviyelerinde alarm satırları üretir.

- `TerminalUIBuilder.cs`
  - SCADA/HMI arayüzünü sahnede otomatik oluşturur.

## 6. MQTT Topic Yapısı

Projedeki MQTT topic’leri senaryolara göre ayrılmıştır. Bu yapı, farklı saldırı türlerinin ayrı veri kanalları üzerinden simüle edilmesini sağlar.

### 6.1. Kesici Kontrol Topic’i

```text
substation/breaker/control
```

Bu topic üzerinden kesici açma/kapama komutları JSON formatında gönderilir.

Örnek yetkisiz açma komutu:

```json
{
  "breakerId": "BRK-01",
  "command": "OPEN",
  "source": "unauthorized",
  "timestamp": "2026-05-17T02:26:08"
}
```

Örnek normal operatör komutu:

```json
{
  "breakerId": "BRK-01",
  "command": "CLOSE",
  "source": "operator",
  "timestamp": "2026-05-17T02:31:00"
}
```

### 6.2. Cooling False Data Injection Topic’leri

```text
substation/attack/type
substation/cooling/control
substation/sensor/temperature/fake
substation/transformer/temperature/real
substation/effect/smoke
substation/alarm/suppression
```

Bu topic’ler trafo soğutma sistemi manipülasyonu ve sahte sıcaklık verisi saldırısı için kullanılır.

### 6.3. Trafo Yağı Kritik Alarm Topic’leri

```text
substation/transformer/oil_temperature
substation/transformer/oil_level
substation/protection/buchholz
substation/transformer/oil_alarm
```

Bu topic’ler yağ sıcaklığı, yağ seviyesi ve Buchholz rölesi alarm senaryosu için kullanılır.

### 6.4. Bara Gerilim Dengesizliği / Voltage Sag Topic’leri

```text
substation/attack/type
substation/busbar/voltage/a
substation/busbar/voltage/b
substation/busbar/voltage/c
substation/busbar/voltage_alarm
```

Bu topic’ler bara gerilim dengesizliği, düşük gerilim ve aşırı gerilim senaryosu için kullanılır. A, B ve C faz değerleri ayrı MQTT mesajlarıyla gönderilir. `substation/busbar/voltage_alarm` topic’i `SAG`, `CRITICAL`, `ON` veya `OFF` değerleriyle alarmın başlatılması ve temizlenmesi için kullanılır.

Örnek voltage sag mesaj dizisi:

```text
substation/attack/type = voltage_sag
substation/busbar/voltage/a = 24.8
substation/busbar/voltage/b = 31.6
substation/busbar/voltage/c = 35.2
substation/busbar/voltage_alarm = SAG
```

Örnek over voltage mesaj dizisi:

```text
substation/attack/type = voltage_over
substation/busbar/voltage/a = 39.8
substation/busbar/voltage/b = 35.4
substation/busbar/voltage/c = 34.8
substation/busbar/voltage_alarm = CRITICAL
```

## 7. MQTT Receiver Yapıları

Unity tarafında MQTT mesajlarını alan birden fazla receiver script bulunur.

### 7.1. SimpleMQTTReceiver.cs

`SimpleMQTTReceiver.cs`, temel trafo sensör verilerini almak ve eski sıcaklık saldırısı/telemetri davranışlarını yönetmek için kullanılır. Bu script sensör verilerini işler, sıcaklık değerlerini SCADA arayüzüne yansıtır ve bazı eski saldırı durumlarında uyarı üretir.

Bu scriptte eski kamera sallama davranışı varsayılan olarak kapatılmıştır:

```csharp
public bool enableFailureCameraShake = false;
```

Bunun nedeni, yeni saldırı senaryolarında kamera davranışının daha kontrollü ve kullanıcı deneyimine uygun şekilde `FDISmokeEffectController` tarafından yönetilmesidir.

### 7.2. BreakerMQTTReceiver.cs

`BreakerMQTTReceiver.cs`, yalnızca kesici kontrol topic’ini dinler:

```text
substation/breaker/control
```

Gelen JSON payload `BreakerController` scriptine iletilir. Receiver kendi içinde basit MQTT CONNECT, SUBSCRIBE ve PUBLISH packet parsing mantığı kullanır. Bu yapı, Unity içinde ekstra MQTT kütüphanesine ihtiyaç duymadan broker’a bağlanmayı sağlar.

### 7.3. CoolingFalseDataReceiver.cs

`CoolingFalseDataReceiver.cs`, `substation/#` topic alanına abone olur ve birden fazla senaryoyu işler:

- Cooling false data injection
- Smoke effect
- Alarm suppression
- Transformer real/fake temperature
- Transformer oil temperature
- Transformer oil level
- Buchholz relay warning
- Oil critical alarm
- Busbar voltage sag / over voltage / phase imbalance alarm

Bu script ayrıca MQTT reconnect davranışı da içerir. Broker Unity’den sonra başlatılırsa receiver belirli aralıklarla yeniden bağlanmayı dener:

```csharp
public bool autoReconnect = true;
public float reconnectIntervalSeconds = 2f;
```

Bu özellik demo açısından önemlidir. Aksi halde Unity Play Mode açıkken Mosquitto broker sonradan çalıştırılırsa Unity mesajları alamaz.

## 8. Görsel ve İşitsel Alarm Sistemi

Saldırıların fiziksel karşılığını daha görünür yapmak için `FDISmokeEffectController.cs` geliştirilmiştir. Bu script duman, kamera focus, alarm ışığı ve alarm sesi davranışlarını tek bir yerden yönetir.

### 8.1. Duman Efekti

Duman efekti sahnede hazır bir particle system’e bağımlı değildir. Script kendi içinde duman pufları ve particle katmanları oluşturur:

- `FDI_GeneratedRealisticSmoke`
- `Smoke_Core_01`
- `Smoke_Core_02`
- `Smoke_Mid_01`
- `Smoke_Mid_02`
- `Smoke_Light_01`
- `Smoke_Light_02`
- `Smoke_Light_03`
- `FDI_RisingSmokeParticles`
- `FDI_DarkSmokeParticles`

Dumanın referans noktası:

```text
TransformerSmoke
```

Script, bu nesnenin pozisyonunu bulur ve belirlenen offset ile dumanı trafo üzerine yerleştirir.

### 8.2. Kamera Focus Davranışı

Saldırı başladığında kamera 5 saniyeliğine ilgili fiziksel olaya odaklanır. Kullanıcıdan beklenen davranış şudur:

1. Saldırı mesajı gelir.
2. Kamera otomatik olarak trafo/duman bölgesine focus yapar.
3. Focus yaklaşık 5 saniye sürer.
4. Süre bitince kamera/freecam önceki kullanıcı durumuna döner.

Bu davranış demo açısından önemlidir. Çünkü saldırının fiziksel karşılığı sahnede operatöre doğrudan gösterilir.

### 8.3. Alarm-Light

Sahnede `Alarm-Light` isimli bir ışık nesnesi kullanılır. Saldırı başladığında:

- Işık inactive olsa bile bulunur.
- GameObject aktif edilir.
- Işık kırmızı renge geçer.
- Intensity değeri yanıp sönme efektiyle değiştirilir.

Saldırı bitince:

- Işık eski renk değerine döner.
- Eski intensity değerine döner.
- Başlangıçta inactive ise tekrar inactive yapılır.

### 8.4. Alarm Sesi

Alarm sesi için harici ses dosyası zorunlu değildir. `FDISmokeEffectController`, runtime sırasında küçük bir procedural beep sesi oluşturur:

```text
FDI_Small_Alarm_Beep
```

Bu ses `AudioSource` ile loop olarak çalınır. Saldırı bitince `AudioSource.Stop()` çağrılır ve ses durur.

## 9. Saldırı Senaryosu 1: Yetkisiz Kesici Açma/Kapama

Bu senaryo, bir saldırganın MQTT üzerinden kesiciye yetkisiz açma veya kapama komutu göndermesini simüle eder.

### 9.1. Amaç

Trafo merkezlerinde kesiciler enerji akışını kontrol eden kritik ekipmanlardır. Bir kesicinin yetkisiz şekilde açılması enerji kesintisine, yetkisiz şekilde kapanması ise bakım personeli veya ekipman için tehlikeli durumlara yol açabilir.

Bu senaryoda saldırgan, SCADA operatörü yerine MQTT topic’ine sahte komut yayınlar.

### 9.2. Python Script

Script:

```text
Assets/_Scripts/AttackScripts/breaker_attack.py
```

Menü seçenekleri:

```text
1 - Unauthorized OPEN
2 - Unauthorized CLOSE
3 - Normal Operator OPEN
4 - Normal Operator CLOSE
q - Quit
```

Script her komuttan sonra kapanmaz, tekrar ana menüye döner. Bu sayede demo sırasında arka arkaya farklı komutlar gönderilebilir.

### 9.3. MQTT Payload

Örnek unauthorized OPEN:

```json
{
  "breakerId": "BRK-01",
  "command": "OPEN",
  "source": "unauthorized",
  "timestamp": "2026-06-02T14:30:00"
}
```

### 9.4. Unity Davranışı

Gelen mesaj `BreakerMQTTReceiver.cs` tarafından alınır ve `BreakerController.cs` içine iletilir.

`BreakerController` şu işlemleri yapar:

- `breakerId` kontrol edilir.
- `command` değeri `OPEN` veya `CLOSE` olarak normalize edilir.
- `source` değeri kaydedilir.
- SCADA alanları güncellenir:
  - Breaker Status
  - Command Source
  - Last Command Time
- Kesici görsel durumu değiştirilir.
- Enerji hattı rengi değiştirilir.
- Kamera kesiciye focus yapar.
- Terminale log satırları eklenir.

### 9.5. IDS Mantığı

Eğer `source == "unauthorized"` ise sistem bunu saldırı olarak algılar.

Üretilen kritik alarm:

```text
CRITICAL ALERT: Unauthorized Breaker Operation Detected
```

Terminal logları:

```text
[TIME] BREAKER OPEN COMMAND RECEIVED
[TIME] SOURCE: UNAUTHORIZED
[TIME] CRITICAL: UNAUTHORIZED SWITCHING ATTACK DETECTED
```

Ek olarak `maintenanceMode == true` iken `CLOSE` komutu gelirse daha kritik bir durum üretilir:

```text
DANGER: Breaker Closed During Maintenance Mode
```

### 9.6. Fiziksel Karşılık

Kesici açıldığında:

- Kesici görseli açık duruma geçer.
- Enerji hattı pasif/kırmızı hale gelir.
- Kamera kesici modeline yaklaşır.
- SCADA alarm paneli kritik alarm gösterir.

Kesici kapandığında:

- Kesici görseli kapalı duruma geçer.
- Enerji hattı aktif/yeşil hale gelir.
- Yetkisiz kaynak varsa alarm devam eder.

## 10. Saldırı Senaryosu 2: Cooling False Data Injection

Bu senaryo, transformatörün gerçek sıcaklığının yükseldiği fakat SCADA’ya sahte normal sıcaklık gösterildiği bir veri bütünlüğü saldırısını temsil eder.

### 10.1. Amaç

Trafo merkezlerinde sıcaklık izleme kritik bir güvenlik fonksiyonudur. Sıcaklık yükseldiğinde soğutma sistemlerinin çalışması ve alarm üretilmesi gerekir. Saldırgan, gerçek sıcaklığı gizleyerek operatörün yanlış karar vermesine neden olabilir.

### 10.2. Python Script

Script:

```text
Assets/_Scripts/AttackScripts/cooling_false_data_attack.py
```

Menü:

```text
1 - Start Cooling False Data Attack
2 - Stop Attack / Restore Normal
q - Quit
```

### 10.3. Başlatma Mesajları

```text
substation/attack/type = cooling_false_data
substation/cooling/control = OFF
substation/sensor/temperature/fake = 42
substation/transformer/temperature/real = 95
substation/effect/smoke = ON
substation/alarm/suppression = ON
```

Bu mesajlar şu anlamlara gelir:

- Soğutma sistemi kapatılır.
- SCADA’ya sahte sıcaklık 42 C gösterilir.
- Gerçek trafo sıcaklığı 95 C olarak ayarlanır.
- Duman efekti açılır.
- Alarm suppression aktif hale getirilir.

### 10.4. Unity Davranışı

`CoolingFalseDataReceiver.cs` bu topic’leri alır ve şu sonuçları üretir:

- `coolingOn = false`
- Gerçek sıcaklık 95 C olarak loglanır.
- SCADA’da sahte sıcaklık gösterilir.
- Trafo materyali aşırı ısınma rengine geçer.
- Duman efekti başlar.
- Alarm ışığı ve alarm sesi başlar.
- Kamera trafo/duman bölgesine 5 saniye focus yapar.
- Terminale veri bütünlüğü saldırısı logu eklenir.

### 10.5. IDS Mantığı

Gerçek sıcaklık kritik eşik değerini aşarsa sistem veri bütünlüğü saldırısı algılar.

Örnek log:

```text
Data Integrity Attack Detected
Critical transformer temperature alarm active
Real transformer temperature: 95 C
```

Alarm suppression aktifse sistem bunu ayrıca belirtir:

```text
Alarm Suppression Active
False Data Injection Active
```

### 10.6. Restore Akışı

Scriptte `2` seçildiğinde şu mesajlar gönderilir:

```text
substation/cooling/control = ON
substation/sensor/temperature/fake = 42
substation/transformer/temperature/real = 45
substation/effect/smoke = OFF
substation/alarm/suppression = OFF
substation/attack/type = none
```

Sonuç:

- Soğutma sistemi normale döner.
- Gerçek sıcaklık 45 C olur.
- Duman kapanır.
- Alarm ışığı ve ses durur.
- SCADA normal duruma döner.

## 11. Saldırı Senaryosu 3: Trafo Yağ Seviyesi / Yağ Sıcaklığı Kritik Alarmı

Bu senaryo, transformatör yağ sıcaklığının kritik seviyeye çıkmasını, yağ seviyesinin düşmesini ve Buchholz rölesinin uyarı üretmesini simüle eder.

### 11.1. Elektriksel ve Fiziksel Arka Plan

Güç transformatörlerinde yağ, hem yalıtım hem de soğutma görevi görür. Yağ sıcaklığının yükselmesi veya yağ seviyesinin düşmesi transformatör için ciddi bir arıza belirtisidir. Buchholz rölesi, yağlı transformatörlerde gaz birikimi veya iç arıza belirtilerini algılamak için kullanılan koruma elemanıdır.

Bu nedenle bu senaryo dijital ikizde güçlü bir fiziksel karşılığa sahiptir:

- Yağ sıcaklığı yükselir.
- Yağ seviyesi düşer.
- Buchholz rölesi warning durumuna geçer.
- Trafo kırmızı alarm durumuna geçer.
- Duman, alarm ışığı ve ses devreye girer.

### 11.2. Python Script

Script:

```text
Assets/_Scripts/AttackScripts/oil_critical_alarm_attack.py
```

Menü:

```text
1 - Start Oil Temperature / Buchholz Alarm
2 - Stop Scenario / Restore Normal
q - Quit
```

Script menülü çalışır ve her komuttan sonra kapanmaz. Demo sırasında saldırı başlatılıp normale döndürülerek tekrar test edilebilir.

### 11.3. Başlatma Mesajları

```text
substation/attack/type = oil_critical_alarm
substation/transformer/oil_temperature = 105
substation/transformer/oil_level = 22
substation/protection/buchholz = WARNING
substation/transformer/oil_alarm = ON
```

Bu değerler demo için kritik eşiği aşan durumları temsil eder:

- Yağ sıcaklığı: 105 C
- Yağ seviyesi: 22%
- Buchholz rölesi: WARNING
- Yağ alarmı: ON

### 11.4. Unity Davranışı

`CoolingFalseDataReceiver.cs`, bu topic’leri alır ve yağ kritik alarmını başlatır.

Sistem şu çıktıları üretir:

```text
OIL TEMP HIGH
BUCHHOLZ RELAY WARNING
Oil temperature: 105 C
Oil level: 22%
Transformer Oil Critical Alarm Attack Detected
```

SCADA alarm paneline eklenen alarm satırları:

```text
OIL TEMP HIGH
BUCHHOLZ RELAY WARNING
```

Sahne davranışı:

- Trafo materyali kırmızı/emission alarm rengine geçer.
- Duman efekti başlar.
- `Alarm-Light` kırmızı yanıp söner.
- Alarm sesi loop olarak çalar.
- Kamera 5 saniyeliğine trafo/duman bölgesine focus yapar.

### 11.5. Restore Mesajları

```text
substation/transformer/oil_temperature = 55
substation/transformer/oil_level = 78
substation/protection/buchholz = CLEAR
substation/transformer/oil_alarm = OFF
substation/effect/smoke = OFF
substation/attack/type = none
```

Restore sonrası:

- Yağ sıcaklığı 55 C olur.
- Yağ seviyesi 78% olur.
- Buchholz rölesi temizlenir.
- Duman kapanır.
- Alarm ışığı kapanır.
- Alarm sesi durur.
- Trafo materyali normal rengine döner.
- SCADA log:

```text
Oil critical alarm cleared
Oil temperature: 55 C
Oil level: 78%
```

## 12. Saldırı Senaryosu 4: Bara Gerilim Dengesizliği / Voltage Sag

Bu senaryo, trafo merkezi barasında A, B ve C faz gerilimlerinin bozulmasını simüle eder. Saldırı veya arıza sonucunda bir fazda düşük gerilim, başka bir fazda normal veya yüksek gerilim görülebilir. Bu durum koruma rölesi açısından `UNDER VOLTAGE`, `OVER VOLTAGE` veya faz dengesizliği alarmı üretir.

### 12.1. Elektriksel ve Operasyonel Arka Plan

Bara sistemi, trafo merkezindeki enerji dağıtımının ortak bağlantı noktasıdır. Bara gerilimi dengesiz hale geldiğinde bağlı yüklerde gerilim kalitesi bozulur, ekipmanlarda ısınma, koruma açması veya hassas cihazlarda hatalı çalışma görülebilir.

Bu nedenle voltage sag senaryosu dijital ikiz için güçlü bir siber-fiziksel karşılığa sahiptir:

- Faz gerilimleri normal nominal değerden sapar.
- SCADA’da bara durumu `FAULT` veya `SUSPECT` olarak görünür.
- Koruma rölesi `UNDER VOLTAGE` veya `OVER VOLTAGE` alarmı üretir.
- Alarm paneli kritik alarm gösterir.
- Alarm ışığı ve alarm sesi çalışır.
- Kamera 5 saniye olay bölgesine focus yapar.

### 12.2. Python Script

Bu senaryo için kullanılan script:

```text
Assets/_Scripts/AttackScripts/voltage_sag_attack.py
```

Script menülü çalışır:

```text
1 - Start Voltage Sag + Phase Imbalance
2 - Start Over Voltage + Phase Imbalance
3 - Stop Scenario / Restore Normal
q - Quit
```

Script her komuttan sonra kapanmaz; kullanıcı aynı terminal oturumunda saldırıyı başlatabilir, farklı varyasyonu deneyebilir ve sistemi normale döndürebilir.

### 12.3. Voltage Sag Başlatma Mesajları

`1 - Start Voltage Sag + Phase Imbalance` seçildiğinde yayınlanan mesajlar:

```text
substation/attack/type = voltage_sag
substation/busbar/voltage/a = 24.8
substation/busbar/voltage/b = 31.6
substation/busbar/voltage/c = 35.2
substation/busbar/voltage_alarm = SAG
```

Bu değerler nominal 34.5 kV bara gerilimine göre A fazında ciddi düşüş, B fazında orta seviye sapma ve C fazında normale yakın değer temsil eder. Fazlar arasındaki fark büyük olduğu için sistem hem düşük gerilim hem de faz dengesizliği olarak değerlendirilir.

### 12.4. Over Voltage Başlatma Mesajları

`2 - Start Over Voltage + Phase Imbalance` seçildiğinde yayınlanan mesajlar:

```text
substation/attack/type = voltage_over
substation/busbar/voltage/a = 39.8
substation/busbar/voltage/b = 35.4
substation/busbar/voltage/c = 34.8
substation/busbar/voltage_alarm = CRITICAL
```

Bu varyasyonda A fazı aşırı gerilim bölgesine çıkar. Fazlar arası fark nedeniyle dengesizlik alarmı da anlamlıdır. Böylece aynı senaryo hem düşük gerilim hem de aşırı gerilim davranışını gösterebilir.

### 12.5. Unity Davranışı

`CoolingFalseDataReceiver.cs`, `substation/#` aboneliği üzerinden gerilim topic’lerini alır. Script içinde nominal faz gerilimi ve alarm eşikleri tanımlıdır:

```text
nominalPhaseVoltage = 34.5
voltageSagThreshold = 30
voltageOverThreshold = 38
voltageImbalanceThreshold = 3
```

Unity tarafında çalışan temel IDS mantığı:

```text
if any phase voltage < 30 kV:
    UNDER VOLTAGE ALARM

if any phase voltage > 38 kV:
    OVER VOLTAGE ALARM

if max(voltageA, voltageB, voltageC) - min(voltageA, voltageB, voltageC) >= 3 kV:
    Phase Imbalance Alarm
```

Alarm tetiklendiğinde SCADA terminalinde şu loglar üretilir:

```text
BUSBAR VOLTAGE SAG / IMBALANCE DETECTED
Voltage A: 24.8 kV
Voltage B: 31.6 kV
Voltage C: 35.2 kV
UNDER VOLTAGE ALARM
IED VOLTAGE PROTECTION WARNING
```

Over voltage varyasyonunda `UNDER VOLTAGE ALARM` yerine `OVER VOLTAGE ALARM` yazılır.

### 12.6. SCADA ve Dijital İkiz Çıktısı

Senaryo aktif olduğunda:

- SCADA sistem modu `CYBER SECURITY EVENT` durumuna geçer.
- Busbar bileşeni `FAULT` veya `SUSPECT` olarak gösterilir.
- Alarm paneline `BUSBAR VOLTAGE SAG / IMBALANCE` alarmı eklenir.
- Koruma rölesi uyarısı `IED VOLTAGE PROTECTION WARNING` olarak loglanır.
- Alarm ışığı kırmızı yanıp söner.
- Alarm sesi çalmaya başlar.
- Duman efekti olay görünürlüğü için aktif edilir.
- Kamera 5 saniye boyunca olay bölgesine focus yapar ve süre bitince freecam kontrolü kullanıcıya geri döner.

Bu senaryoda dijital ikiz, yalnızca sayısal gerilim değerini değil, gerilim kalitesi bozulmasının operatör ekranındaki ve fiziksel sahnedeki sonucunu da gösterir.

### 12.7. Restore Mesajları

`3 - Stop Scenario / Restore Normal` seçildiğinde yayınlanan mesajlar:

```text
substation/busbar/voltage/a = 34.5
substation/busbar/voltage/b = 34.5
substation/busbar/voltage/c = 34.5
substation/busbar/voltage_alarm = OFF
substation/effect/smoke = OFF
substation/attack/type = none
```

Restore sonrasında:

- A, B ve C fazları 34.5 kV nominal değere döner.
- Voltage alarm state kapanır.
- Duman efekti kapanır.
- Alarm ışığı eski durumuna döner.
- Alarm sesi durur.
- SCADA log:

```text
Busbar voltage alarm cleared
Voltage A/B/C: 34.5/34.5/34.5 kV
```

## 13. Normal Operasyon ve Saldırı Karşılaştırması

| Durum | Normal Operasyon | Saldırı Durumu |
|---|---|---|
| Kesici | Operatör komutuyla açılır/kapanır | Yetkisiz MQTT komutuyla açılır/kapanır |
| Sıcaklık | Gerçek değer SCADA’ya yansır | Gerçek sıcaklık gizlenebilir |
| Soğutma | Fan/soğutma aktif çalışır | Soğutma OFF yapılır |
| Yağ sistemi | Yağ sıcaklığı ve seviye normaldir | Yağ sıcaklığı yüksek, seviye düşüktür |
| Buchholz | CLEAR/NORMAL | WARNING |
| Bara gerilimi | A/B/C fazları nominal ve dengelidir | Voltage sag, over voltage veya faz dengesizliği oluşur |
| Koruma rölesi | Gerilim alarmı yoktur | UNDER/OVER VOLTAGE uyarısı üretir |
| SCADA | Normal operation gösterir | Kritik alarm gösterir |
| Fiziksel efekt | Duman yok | Duman var |
| Alarm ışığı | Kapalı/eski durumunda | Kırmızı yanıp söner |
| Alarm sesi | Yok | Loop alarm sesi |
| Kamera | Kullanıcı kontrolünde | 5 saniye olay yerine focus |

## 14. IDS Yaklaşımı

Projede kullanılan IDS mantığı kural tabanlıdır. Gerçek bir makine öğrenmesi modeli veya gelişmiş ağ tabanlı IDS kullanılmamıştır. Bunun yerine belirli topic ve payload değerlerine göre saldırı tespiti yapılır.

### 14.1. Kesici IDS Kuralları

```text
if source == "unauthorized":
    Unauthorized Breaker Operation Detected
```

```text
if maintenanceMode == true and command == "CLOSE":
    Breaker Closed During Maintenance Mode
```

### 14.2. Cooling FDI IDS Kuralları

```text
if realTemperature > criticalTemperature:
    Data Integrity Attack Detected
```

```text
if alarmSuppression == ON:
    Alarm Suppression Active
```

### 14.3. Oil Critical Alarm IDS Kuralları

```text
if oilTemperature >= criticalOilTemperature:
    OIL TEMP HIGH
```

```text
if oilLevel <= criticalOilLevel:
    Oil level critical
```

```text
if buchholz == WARNING:
    BUCHHOLZ RELAY WARNING
```

### 14.4. Voltage Sag / Faz Dengesizliği IDS Kuralları

```text
if voltageA < voltageSagThreshold or voltageB < voltageSagThreshold or voltageC < voltageSagThreshold:
    UNDER VOLTAGE ALARM
```

```text
if voltageA > voltageOverThreshold or voltageB > voltageOverThreshold or voltageC > voltageOverThreshold:
    OVER VOLTAGE ALARM
```

```text
if max(voltageA, voltageB, voltageC) - min(voltageA, voltageB, voltageC) >= voltageImbalanceThreshold:
    BUSBAR VOLTAGE SAG / IMBALANCE DETECTED
```

Bu kurallar basit olmakla birlikte demo ve akademik anlatım için yeterlidir. Çünkü amaç, bir saldırı sinyalinin dijital ikizde fiziksel ve SCADA karşılığını göstermektir.

## 15. Kamera Yönetimi

Projede kamera davranışı saldırı senaryolarında önemli bir rol oynar. Normalde kullanıcı freecam ile sahnede dolaşabilir. Saldırı olduğunda ise sistem operatörün dikkatini ilgili fiziksel bileşene çekmek için otomatik focus yapar.

Kesici senaryosunda kamera `circuit_breaker` nesnesine odaklanır. Duman/trafo ve bara gerilim alarmı senaryolarında kamera `TransformerSmoke` referans konumuna odaklanır. Bu tercih, duman, alarm ışığı ve sesli alarm davranışının aynı olay bölgesinde görünmesini sağlar.

Kamera focus davranışının hedefleri:

- Operatör saldırının fiziksel etkisini doğrudan görür.
- Demo sırasında olay kaçırılmaz.
- Focus süresi sınırlıdır.
- Süre bitince kullanıcı kontrolüne geri dönülür.

Bu davranış özellikle bitirme projesi sunumunda önemlidir. Çünkü jüri veya izleyici, MQTT mesajı ile fiziksel sonuç arasındaki ilişkiyi doğrudan gözlemler.

## 16. Test Ortamı

Test için gerekli temel bileşenler:

- Unity 6 Editor
- Mosquitto MQTT broker
- Python 3.11 veya uyumlu Python sürümü
- paho-mqtt paketi

Python paketi:

```powershell
pip install paho-mqtt
```

Mosquitto Windows üzerinde `C:\Program Files\Mosquitto` altında kuruluysa PowerShell’de current directory’den çalıştırmak için:

```powershell
cd "C:\Program Files\Mosquitto"
.\mosquitto.exe
```

PowerShell current directory’deki exe’leri otomatik çalıştırmadığı için `.\mosquitto.exe` yazmak gerekir.

## 17. Demo Akışları

### 17.1. Genel Başlatma

1. Mosquitto broker çalıştırılır.
2. Unity projesi açılır.
3. `SampleScene` sahnesi açılır.
4. Unity Play Mode başlatılır.
5. Console’da MQTT bağlantısı kontrol edilir:

```text
[CoolingFalseDataReceiver] Connected to MQTT broker 127.0.0.1:1883, subscribed to substation/#
[BreakerMQTTReceiver] Connected to MQTT broker localhost:1883, subscribed to substation/breaker/control
```

### 17.2. Kesici Saldırısı Testi

Komut:

```powershell
python Assets\_Scripts\AttackScripts\breaker_attack.py
```

Test:

1. `1 - Unauthorized OPEN` seçilir.
2. Kesici açılır.
3. SCADA’da `OPEN` görünür.
4. Source `unauthorized` görünür.
5. Alarm paneli kritik alarm gösterir.
6. Kamera kesiciye focus yapar.
7. `2 - Unauthorized CLOSE` seçilirse kesici kapanır fakat olay yine saldırı olarak loglanır.
8. `3` veya `4` seçilirse normal operatör komutu olarak işlenir.

### 17.3. Cooling False Data Injection Testi

Komut:

```powershell
python Assets\_Scripts\AttackScripts\cooling_false_data_attack.py
```

Test:

1. `1` seçilir.
2. Soğutma sistemi OFF olur.
3. Gerçek sıcaklık 95 C olur.
4. SCADA sahte sıcaklık 42 C gösterebilir.
5. Duman efekti başlar.
6. Alarm ışığı yanıp söner.
7. Alarm sesi çalar.
8. Kamera trafo/duman bölgesine 5 saniye focus yapar.
9. `2` seçilirse sistem normale döner.

### 17.4. Trafo Yağ Kritik Alarm Testi

Komut:

```powershell
python Assets\_Scripts\AttackScripts\oil_critical_alarm_attack.py
```

Test:

1. `1` seçilir.
2. MQTT mesajları yayınlanır:

```text
substation/attack/type = oil_critical_alarm
substation/transformer/oil_temperature = 105
substation/transformer/oil_level = 22
substation/protection/buchholz = WARNING
substation/transformer/oil_alarm = ON
```

3. Unity console ve SCADA terminalde şu loglar görülür:

```text
OIL TEMP HIGH
BUCHHOLZ RELAY WARNING
Oil temperature: 105 C
Oil level: 22%
```

4. Trafo kırmızı alarm durumuna geçer.
5. Duman efekti oluşur.
6. `Alarm-Light` kırmızı yanıp söner.
7. Alarm sesi başlar.
8. Kamera 5 saniye focus yapar.
9. `2` seçilir.
10. Sistem normale döner:

```text
Oil critical alarm cleared
Oil temperature: 55 C
Oil level: 78%
```

### 17.5. Bara Gerilim Dengesizliği / Voltage Sag Testi

Komut:

```powershell
python Assets\_Scripts\AttackScripts\voltage_sag_attack.py
```

Voltage sag testi:

1. `1 - Start Voltage Sag + Phase Imbalance` seçilir.
2. MQTT mesajları yayınlanır:

```text
substation/attack/type = voltage_sag
substation/busbar/voltage/a = 24.8
substation/busbar/voltage/b = 31.6
substation/busbar/voltage/c = 35.2
substation/busbar/voltage_alarm = SAG
```

3. Unity console ve SCADA terminalde şu loglar görülür:

```text
BUSBAR VOLTAGE SAG / IMBALANCE DETECTED
Voltage A: 24.8 kV
Voltage B: 31.6 kV
Voltage C: 35.2 kV
UNDER VOLTAGE ALARM
IED VOLTAGE PROTECTION WARNING
```

4. SCADA’da Busbar durumu `FAULT` olarak görünür.
5. Alarm panelinde `BUSBAR VOLTAGE SAG / IMBALANCE` alarmı görünür.
6. Alarm ışığı kırmızı yanıp söner.
7. Alarm sesi başlar.
8. Kamera 5 saniye focus yapar ve sonra freecam kontrolü geri gelir.
9. `3 - Stop Scenario / Restore Normal` seçilir.
10. Sistem normale döner:

```text
Busbar voltage alarm cleared
Voltage A/B/C: 34.5/34.5/34.5 kV
```

Over voltage testi:

1. Aynı script içinde `2 - Start Over Voltage + Phase Imbalance` seçilir.
2. A fazı 39.8 kV değerine çıkar.
3. SCADA terminalde `OVER VOLTAGE ALARM` görülür.
4. `3` seçilerek sistem normale döndürülür.

## 18. Projenin Akademik Katkısı

Bu proje, enerji sistemlerinde siber güvenlik olaylarının yalnızca ağ seviyesinde değil, fiziksel operasyon seviyesinde de değerlendirilmesi gerektiğini gösterir. Klasik siber güvenlik analizlerinde saldırı genellikle paket, port, protokol veya log düzeyinde incelenir. Bu projede ise saldırının fiziksel sonucu dijital ikiz üzerinde gösterilir.

Akademik katkılar:

- MQTT tabanlı saldırı mesajlarının dijital ikiz üzerindeki fiziksel karşılıkları gösterilmiştir.
- SCADA/HMI arayüzü ile siber saldırı farkındalığı görselleştirilmiştir.
- Kesici, soğutma sistemi, trafo sıcaklığı, yağ seviyesi, Buchholz rölesi ve bara gerilimi gibi elektriksel bileşenler saldırı senaryolarına bağlanmıştır.
- Basit kural tabanlı IDS mantığı uygulanmıştır.
- Alarm ışığı, alarm sesi, duman ve kamera focus ile olay farkındalığı artırılmıştır.

## 19. Güçlü Yönler

Projenin güçlü yönleri:

- Gerçek zamanlı MQTT haberleşmesi kullanır.
- Unity dijital ikizi ile fiziksel karşılık sunar.
- SCADA/HMI ekranı demo için anlaşılırdır.
- Birden fazla saldırı senaryosu desteklenir.
- Python saldırı scriptleri menülüdür ve tekrar tekrar kullanılabilir.
- Alarm paneli, terminal logları, duman, ses ve ışık aynı olay zincirine bağlıdır.
- Kesici gibi mekanik bileşenlerde animasyon/fallback hareket desteği vardır.
- MQTT broker sonradan açılsa bile bazı receiver yapıları reconnect yapabilir.

## 20. Sınırlılıklar

Projenin mevcut sınırlılıkları:

- MQTT haberleşmesi local broker üzerinden yapılmaktadır.
- TLS, kullanıcı adı/şifre veya sertifika tabanlı kimlik doğrulama kullanılmamaktadır.
- IDS mantığı kural tabanlıdır; gelişmiş davranış analizi veya makine öğrenmesi yoktur.
- IEC 61850 GOOSE gerçek protokol paketi olarak uygulanmamıştır; GOOSE davranışı simülasyon mantığıyla temsil edilmiştir.
- Elektriksel güç akışı fizik motoru veya gerçek yük akışı hesabı ile modellenmemiştir.
- Sensör değerleri demo amaçlı sabit veya script tabanlıdır.
- Alarm önceliklendirme ve olay korelasyonu sınırlıdır.

Bu sınırlılıklar bitirme projesi kapsamında kabul edilebilir; çünkü hedef, gerçek trafo merkezi koruma sisteminin birebir endüstriyel uygulaması değil, dijital ikiz üzerinde siber-fiziksel saldırı farkındalığı oluşturmaktır.

## 21. Gelecek Geliştirmeler

Projeye ileride şu geliştirmeler eklenebilir:

- MQTT TLS desteği
- Broker authentication
- Role-based access control
- Gerçek IEC 61850 MMS/GOOSE paket simülasyonu
- Daha detaylı elektriksel güç akışı modeli
- Trend grafiklerinin SCADA paneline eklenmesi
- Alarm geçmişi ve olay kayıt sistemi
- Makine öğrenmesi tabanlı anomali tespiti
- Dijital ikiz üzerinde bakım modu, operatör onayı ve olay müdahale akışı
- Web tabanlı uzaktan izleme paneli
- Birden fazla trafo, bara ve kesici için genişletilmiş model

## 22. Sonuç

Bu proje, Unity 6 ve MQTT kullanılarak geliştirilen bir trafo merkezi dijital ikizi üzerinde siber saldırı senaryolarının görselleştirilmesini sağlar. Python scriptleri ile üretilen MQTT mesajları, Unity tarafında SCADA/HMI, 3B model, alarm paneli, duman, ses, ışık ve kamera focus davranışlarına dönüştürülür.

Projede dört ana saldırı senaryosu uygulanmıştır:

1. Yetkisiz kesici açma/kapama saldırısı
2. Cooling false data injection saldırısı
3. Trafo yağ seviyesi / yağ sıcaklığı kritik alarmı
4. Bara gerilim dengesizliği / voltage sag saldırısı

Bu senaryoların her biri dijital ikizde fiziksel bir karşılığa sahiptir. Kesici saldırısında kesici hareket eder ve enerji hattı değişir. Cooling FDI saldırısında gerçek sıcaklık yükselir, duman ve alarm oluşur. Yağ kritik alarm senaryosunda yağ sıcaklığı ve seviyesi üzerinden Buchholz rölesi uyarısı üretilir, trafo alarm durumuna geçer. Voltage sag senaryosunda ise A/B/C faz gerilimleri bozulur, bara durumu SCADA’da arıza olarak görünür ve koruma rölesi düşük/aşırı gerilim uyarısı üretir.

Sonuç olarak proje, enerji sistemleri siber güvenliği alanında dijital ikiz yaklaşımının eğitim, demonstrasyon ve farkındalık oluşturma açısından güçlü bir yöntem olduğunu göstermektedir. SCADA ekranı, MQTT haberleşmesi ve 3B sahne davranışları birlikte kullanıldığında, siber saldırıların yalnızca yazılımsal değil aynı zamanda fiziksel sonuçları da anlaşılır hale gelir.

## 23. Dosya ve Script Özeti

| Dosya | Görev |
|---|---|
| `Assets/_Scripts/SimpleMQTTReceiver.cs` | Temel MQTT sensör verileri ve eski telemetri saldırı davranışları |
| `Assets/_Scripts/BreakerMQTTReceiver.cs` | `substation/breaker/control` topic’ini dinler |
| `Assets/_Scripts/BreakerController.cs` | Kesici komutlarını işler, IDS ve görsel hareketi yönetir |
| `Assets/_Scripts/CoolingFalseDataReceiver.cs` | Cooling FDI, smoke, oil alarm, voltage sag ve ilgili MQTT topic’lerini işler |
| `Assets/_Scripts/FDISmokeEffectController.cs` | Duman, alarm ışığı, alarm sesi ve kamera focus davranışını yönetir |
| `Assets/_Scripts/SCADAHMIController.cs` | SCADA/HMI genel durum yönetimi |
| `Assets/_Scripts/AlarmPanelController.cs` | Alarm paneli mesajlarını yönetir |
| `Assets/_Scripts/SCADATerminalController.cs` | Terminal log ve komut arayüzü |
| `Assets/_Scripts/TerminalUIBuilder.cs` | SCADA arayüzünü programatik olarak oluşturur |
| `Assets/_Scripts/AttackScripts/breaker_attack.py` | Yetkisiz/normal kesici komutları yayınlar |
| `Assets/_Scripts/AttackScripts/cooling_false_data_attack.py` | Cooling FDI saldırısını başlatır/durdurur |
| `Assets/_Scripts/AttackScripts/oil_critical_alarm_attack.py` | Yağ sıcaklığı/seviyesi ve Buchholz alarm senaryosunu başlatır/durdurur |
| `Assets/_Scripts/AttackScripts/voltage_sag_attack.py` | Bara gerilim dengesizliği, voltage sag ve over voltage senaryolarını başlatır/durdurur |

## 24. Tez İçin Önerilen Bölüm Başlıkları

Bu proje tez metnine dönüştürülürken şu bölüm yapısı kullanılabilir:

1. Giriş
2. Enerji Sistemlerinde Dijital İkiz Kavramı
3. SCADA ve Trafo Merkezi Otomasyonu
4. MQTT Haberleşme Protokolü
5. Siber-Fiziksel Saldırı Senaryoları
6. Proje Mimarisi
7. Unity Tabanlı Dijital İkiz Tasarımı
8. MQTT Tabanlı Veri ve Saldırı Entegrasyonu
9. SCADA/HMI Arayüz Tasarımı
10. Yetkisiz Kesici Açma/Kapama Saldırısı
11. False Data Injection ve Soğutma Manipülasyonu
12. Trafo Yağ Kritik Alarm Senaryosu
13. Bara Gerilim Dengesizliği ve Voltage Sag Senaryosu
14. Testler ve Bulgular
15. Sonuç ve Gelecek Çalışmalar
