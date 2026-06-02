using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class SimpleMQTTReceiver : MonoBehaviour
{
    
    [Header("UI References")]
    public Text temperatureText;      // Sıcaklık göstergesi
    public Text voltageText;          // Gerilim göstergesi
    public Text loadText;             // Yük göstergesi
    public Text statusText;           // Durum göstergesi
    public GameObject alarmPanel;     // Alarm paneli
    public AlarmPanelController alarmPanelController;

    [Header("Transformer Thermal Digital Twin")]
    public Renderer transformerRenderer;
    public Renderer[] transformerRenderers;
    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.78f, 0.12f, 1f);
    public Color criticalColor = new Color(1f, 0.18f, 0.08f, 1f);
    public Color failureColor = new Color(0.22f, 0.02f, 0.02f, 1f);
    public float warningThreshold = 80f;
    public float criticalThreshold = 85f;
    public float smokeThreshold = 100f;
    public float failureThreshold = 110f;
    public ParticleSystem smokeParticle;
    public AudioSource sirenAudioSource;
    public Light alarmLight;
    public GameObject explosionEffect;
    public Camera cameraShake;
    public bool enableFailureCameraShake = false;
    public float cameraShakeAmount = 0.08f;
    
    [Header("MQTT Settings")]
    public string brokerAddress = "localhost";
    public int brokerPort = 1883;
    public string topic = "TM1/Trafo/sensor";
    
    // MQTT bağlantısı için
    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread mqttThread;
    private bool isConnected = false;
    private bool isRunning = true;
    
    // Son alinan veriler
    private float lastTemperature = 58f;
    private float lastDisplayedTemperature = 58f;
    private float lastRealTemperature = 58f;
    private float lastOilTemperature = 60f;
    private float lastVoltage = 154f;
    private float lastSecondaryVoltage = 34.5f;
    private float lastPrimaryCurrent = 250f;
    private float lastSecondaryCurrent = 1200f;
    private float lastLoad = 80f;
    private float lastMessageTime = 0;
    private int messageCounter = 0;
    
    private readonly Queue<string> pendingPayloads = new Queue<string>();
    private readonly object pendingPayloadLock = new object();
private string lastRawJson = "";
    
    // ===== SALDIRI YÖNETİMİ İÇİN YENİ DEĞİŞKENLER =====
    private bool isUnderAttack = false;
    private string attackType = "";
    private float attackValue = 0;
    private bool dataLossSimulated = false;
    private bool telemetryAttackActive = false;
    private string telemetryAttackType = "";
    private string telemetryStatus = "NORMAL";
    private string lastThermalStatus = "";
    private Vector3 originalCameraPosition;
    private bool hasCameraOriginalPosition = false;
    private bool cameraShakeActive = false;
    
    void Start()
    {
        // UI referanslarını bul (eğer atanmadıysa)
        if (temperatureText == null)
            temperatureText = GameObject.Find("TemperatureValue")?.GetComponent<Text>();
        if (voltageText == null)
            voltageText = GameObject.Find("VoltageValue")?.GetComponent<Text>();
        if (loadText == null)
            loadText = GameObject.Find("LoadValue")?.GetComponent<Text>();
        if (statusText == null)
            statusText = GameObject.Find("StatusValue")?.GetComponent<Text>();
        if (alarmPanelController == null)
            alarmPanelController = GetComponent<AlarmPanelController>() ?? FindFirstObjectByType<AlarmPanelController>();
        
        // MQTT bağlantısını başlat
        ConnectToMQTT();
        
        // Alarm panelini gizle
        if (alarmPanel != null)
            alarmPanel.SetActive(false);
        
        // Son mesaj zamanını başlat
        lastMessageTime = Time.time;
        ApplyThermalEffects();
        
        Debug.Log("SimpleMQTTReceiver Başlatıldı - Saldırı simülasyonu için hazır");
    }
    
    void ConnectToMQTT()
    {
        try
        {
            tcpClient = new TcpClient(brokerAddress, brokerPort);
            stream = tcpClient.GetStream();
            
            // MQTT CONNECT paketini gönder
            SendConnectPacket();
            
            // MQTT SUBSCRIBE paketini gönder
            SendSubscribePacket();
            
            // Dinleme thread'ini başlat
            mqttThread = new Thread(ListenForMessages);
            mqttThread.Start();
            
            isConnected = true;
            Debug.Log($"✅ MQTT Broker'a bağlandı: {brokerAddress}:{brokerPort}");
            
            if (statusText != null)
                statusText.text = "Bağlı";
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ MQTT bağlantı hatası: {e.Message}");
            if (statusText != null)
                statusText.text = "Bağlantı Yok";
        }
    }
    
void SendConnectPacket()
    {
        byte[] protocolName = Encoding.ASCII.GetBytes("MQTT");
        byte[] clientId = Encoding.ASCII.GetBytes("UnityTrafo");

        List<byte> body = new List<byte>();
        body.Add(0x00);
        body.Add((byte)protocolName.Length);
        body.AddRange(protocolName);
        body.Add(0x04);
        body.Add(0x02);
        body.Add(0x00);
        body.Add(0x3C);
        body.Add(0x00);
        body.Add((byte)clientId.Length);
        body.AddRange(clientId);

        List<byte> packet = new List<byte>();
        packet.Add(0x10);
        packet.AddRange(EncodeRemainingLength(body.Count));
        packet.AddRange(body);

        stream.Write(packet.ToArray(), 0, packet.Count);
    }
    
void SendSubscribePacket()
    {
        byte[] topicBytes = Encoding.UTF8.GetBytes(topic);

        List<byte> body = new List<byte>();
        body.Add(0x00);
        body.Add(0x01);
        body.Add((byte)(topicBytes.Length >> 8));
        body.Add((byte)(topicBytes.Length & 0xFF));
        body.AddRange(topicBytes);
        body.Add(0x00);

        List<byte> packet = new List<byte>();
        packet.Add(0x82);
        packet.AddRange(EncodeRemainingLength(body.Count));
        packet.AddRange(body);

        stream.Write(packet.ToArray(), 0, packet.Count);
        Debug.Log($"[MQTT] Abone olundu: {topic}");
    }

List<byte> EncodeRemainingLength(int length)
    {
        List<byte> encoded = new List<byte>();
        do
        {
            byte digit = (byte)(length % 128);
            length /= 128;
            if (length > 0)
                digit |= 128;
            encoded.Add(digit);
        }
        while (length > 0);
        return encoded;
    }

    int DecodeRemainingLength(byte[] data, int maxLength, out int bytesUsed)
    {
        int multiplier = 1;
        int value = 0;
        bytesUsed = 0;

        for (int i = 1; i < maxLength; i++)
        {
            byte encodedByte = data[i];
            value += (encodedByte & 127) * multiplier;
            bytesUsed++;
            if ((encodedByte & 128) == 0)
                break;
            multiplier *= 128;
        }

        return value;
    }

    
    void ListenForMessages()
    {
        byte[] buffer = new byte[4096];
        
        while (isRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ProcessMQTTMessage(buffer, bytesRead);
                    }
                }
                Thread.Sleep(10);
            }
            catch (Exception e)
            {
                Debug.LogError($"MQTT dinleme hatası: {e.Message}");
                break;
            }
        }
    }
    
void ProcessMQTTMessage(byte[] data, int length)
    {
        try
        {
            if (length < 2) return;

            if ((data[0] & 0xF0) == 0x30)
            {
                int remainingLengthBytes;
                DecodeRemainingLength(data, length, out remainingLengthBytes);
                int index = 1 + remainingLengthBytes;

                if (index + 2 > length) return;

                int topicLen = (data[index] << 8) | data[index + 1];
                index += 2 + topicLen;

                if (index >= length) return;

                string payload = Encoding.UTF8.GetString(data, index, length - index);
                EnqueuePayload(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Mesaj isleme hatasi: {e.Message}");
        }
    }

void EnqueuePayload(string payload)
    {
        lock (pendingPayloadLock)
        {
            pendingPayloads.Enqueue(payload);
        }
    }

    void Update()
    {
        while (true)
        {
            string payload = null;
            lock (pendingPayloadLock)
            {
                if (pendingPayloads.Count > 0)
                    payload = pendingPayloads.Dequeue();
            }

            if (payload == null)
                break;

            ParseSensorData(payload);
        }
    }

    
void ParseSensorData(string jsonData)
    {
        if (dataLossSimulated)
        {
            if (Time.frameCount % 60 == 0)
                Debug.Log("[DoS] Veri akisi kesildi - UI guncellenmiyor");
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<TrafoData>(jsonData);

            lastMessageTime = Time.time;
            lastRawJson = jsonData;
            messageCounter++;

            if (!isUnderAttack)
            {
                float displayedTemp = data.displayedTemperature > 0f ? data.displayedTemperature : data.sicaklik;
                float realTemp = data.realTemperature > 0f ? data.realTemperature : displayedTemp;

                lastTemperature = displayedTemp;
                lastDisplayedTemperature = displayedTemp;
                lastRealTemperature = realTemp;
                lastOilTemperature = data.yag_sicaklik;
                lastVoltage = data.gerilim_primer / 1000f;
                lastSecondaryVoltage = data.gerilim_sekonder / 1000f;
                lastPrimaryCurrent = data.akim_primer;
                lastSecondaryCurrent = data.akim_sekonder;
                lastLoad = data.yuk_seviyesi;
                telemetryAttackActive = data.attackActive;
                telemetryAttackType = string.IsNullOrEmpty(data.attackType) ? "" : data.attackType;
                telemetryStatus = string.IsNullOrEmpty(data.status) ? GetThermalStatus(realTemp) : data.status;
            }

            UpdateUI();
            ApplyThermalEffects();
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON parse hatasi: {e.Message}\nVeri: {jsonData}");
        }
    }
    
    void UpdateUI()
    {
        // Sıcaklık güncelle (saldırı altındaysa sahte değeri göster)
        float displayTemp = isUnderAttack && attackType == "temperature" ? attackValue : lastDisplayedTemperature;
        
        if (temperatureText != null)
        {
            temperatureText.text = $"{displayTemp:F1}°C";
            bool showThermalAttackWarning = IsTemperatureAttackScenarioActive();
            if (showThermalAttackWarning && lastRealTemperature > criticalThreshold)
            {
                temperatureText.color = Color.red;
            }
            else if (showThermalAttackWarning && lastRealTemperature > warningThreshold)
            {
                temperatureText.color = Color.yellow;
            }
            else
            {
                temperatureText.color = Color.white;
            }
        }
        
        // Gerilim güncelle (saldırı altındaysa sahte değeri göster)
        float displayVoltage = isUnderAttack && attackType == "voltage" ? attackValue : lastVoltage;
        
        if (voltageText != null)
        {
            voltageText.text = $"{displayVoltage:F1} kV";
            if (displayVoltage < 150 || displayVoltage > 165)
            {
                voltageText.color = Color.red;
            }
            else
            {
                voltageText.color = Color.white;
            }
        }
        
        // Yük güncelle (saldırı altındaysa sahte değeri göster)
        float displayLoad = isUnderAttack && attackType == "load" ? attackValue : lastLoad;
        
        if (loadText != null)
        {
            loadText.text = $"%{displayLoad:F0}";
            if (displayLoad > 100)
            {
                loadText.color = Color.red;
            }
            else if (displayLoad > 90)
            {
                loadText.color = Color.yellow;
            }
            else
            {
                loadText.color = Color.white;
            }
        }
        
        // Status text güncelle (saldırı durumu)
        if (statusText != null)
        {
            if (telemetryAttackActive)
            {
                statusText.text = $"FDI AKTIF: SCADA {lastDisplayedTemperature:F1} C | GERCEK {lastRealTemperature:F1} C";
                statusText.color = Color.red;
            }
            else if (isUnderAttack)
            {
                if (attackType == "dos")
                    statusText.text = "⚠️ DoS SALDIRISI ALTINDA!";
                else
                    statusText.text = $"⚠️ FDI SALDIRISI ALTINDA! ({attackType})";
                statusText.color = Color.red;
            }
            else
            {
                statusText.text = isConnected ? "✅ Normal Çalışıyor" : "❌ Bağlantı Yok";
                statusText.color = isConnected ? Color.green : Color.red;
            }
        }
    }

    void ApplyThermalEffects()
    {
        bool temperatureAttackActive = IsTemperatureAttackScenarioActive();
        string status = temperatureAttackActive ? GetThermalStatus(lastRealTemperature) : "NORMAL";
        bool smokeActive = temperatureAttackActive && lastRealTemperature > smokeThreshold;
        bool failureActive = temperatureAttackActive && lastRealTemperature > failureThreshold;

        ApplyTransformerColor(status);
        ApplySmoke(smokeActive);
        ApplyFailureEffects(failureActive);
        RaiseAlarmForStatus(status);
        lastThermalStatus = status;
    }

    string GetThermalStatus(float realTemp)
    {
        if (realTemp > failureThreshold)
            return "FAILURE";
        if (realTemp > smokeThreshold)
            return "SMOKE";
        if (realTemp > criticalThreshold)
            return "CRITICAL";
        if (realTemp > warningThreshold)
            return "WARNING";
        return "NORMAL";
    }

    void ApplyTransformerColor(string status)
    {
        Color targetColor = normalColor;
        if (status == "WARNING")
            targetColor = warningColor;
        else if (status == "CRITICAL" || status == "SMOKE")
            targetColor = criticalColor;
        else if (status == "FAILURE")
            targetColor = failureColor;

        if (transformerRenderer != null)
            ApplyMaterialColor(transformerRenderer, targetColor);

        if (transformerRenderers == null)
            return;

        foreach (Renderer targetRenderer in transformerRenderers)
        {
            if (targetRenderer != null)
                ApplyMaterialColor(targetRenderer, targetColor);
        }
    }

    void ApplyMaterialColor(Renderer targetRenderer, Color color)
    {
        Material material = targetRenderer.material;
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
        {
            Color emission = color * (color == normalColor ? 0f : 0.65f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
    }

    void ApplySmoke(bool active)
    {
        if (smokeParticle == null)
            return; // TODO: Inspector'dan trafo uzerindeki smoke particle atanabilir.

        if (active && !smokeParticle.isPlaying)
            smokeParticle.Play();
        else if (!active && smokeParticle.isPlaying)
            smokeParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void ApplyFailureEffects(bool active)
    {
        if (sirenAudioSource != null)
        {
            if (active && !sirenAudioSource.isPlaying)
                sirenAudioSource.Play();
            else if (!active && sirenAudioSource.isPlaying)
                sirenAudioSource.Stop();
        }

        if (alarmLight != null)
            alarmLight.enabled = active;

        if (explosionEffect != null && explosionEffect.activeSelf != active)
            explosionEffect.SetActive(active);

        if (active && enableFailureCameraShake)
            ApplyCameraShake();
        else if (cameraShakeActive)
            RestoreCameraPosition();
    }

    void CaptureCameraPosition()
    {
        Camera targetCamera = cameraShake != null ? cameraShake : Camera.main;
        if (targetCamera == null)
            return; // TODO: Failure camera shake icin Camera referansi atanabilir.

        cameraShake = targetCamera;
        originalCameraPosition = targetCamera.transform.localPosition;
        hasCameraOriginalPosition = true;
    }

    void ApplyCameraShake()
    {
        if (cameraShake == null)
            CaptureCameraPosition();
        if (cameraShake == null || !hasCameraOriginalPosition)
            return;

        Vector3 offset = UnityEngine.Random.insideUnitSphere * cameraShakeAmount;
        offset.z = 0f;
        cameraShake.transform.localPosition = originalCameraPosition + offset;
        cameraShakeActive = true;
    }

    void RestoreCameraPosition()
    {
        if (cameraShake != null && hasCameraOriginalPosition)
            cameraShake.transform.localPosition = originalCameraPosition;
        cameraShakeActive = false;
    }

    void RaiseAlarmForStatus(string status)
    {
        if (status == lastThermalStatus)
            return;

        if (status == "NORMAL")
        {
            if (alarmPanel != null)
                alarmPanel.SetActive(false);
            ClearSimpleThermalAlarms();
            return;
        }

        if (status == "WARNING")
        {
            if (alarmPanelController != null)
                alarmPanelController.AddAlarm("Transformer temperature warning", AlarmPanelController.AlarmSeverity.Warning);
            ShowAlarm("UYARI: Trafo sicakligi yukseliyor");
        }
        else if (status == "CRITICAL")
        {
            if (alarmPanelController != null)
                alarmPanelController.AddAlarm("Critical transformer temperature", AlarmPanelController.AlarmSeverity.Critical);
            ShowAlarm("KRITIK: Gercek trafo sicakligi kritik seviyede");
        }
        else if (status == "SMOKE")
        {
            if (alarmPanelController != null)
                alarmPanelController.AddAlarm("Transformer overheating smoke detected", AlarmPanelController.AlarmSeverity.Critical);
            ShowAlarm("DUMAN: Trafo uzerinde asiri isinma");
        }
        else if (status == "FAILURE")
        {
            if (alarmPanelController != null)
                alarmPanelController.AddAlarm("Transformer failure", AlarmPanelController.AlarmSeverity.Critical);
            ShowAlarm("FAILURE: Transformer failure");
        }
    }

    bool IsTemperatureAttackScenarioActive()
    {
        return telemetryAttackActive || (isUnderAttack && attackType == "temperature");
    }

    void ClearSimpleThermalAlarms()
    {
        if (alarmPanelController == null)
            return;

        alarmPanelController.RemoveAlarmsContaining("Transformer temperature warning");
        alarmPanelController.RemoveAlarmsContaining("Critical transformer temperature");
        alarmPanelController.RemoveAlarmsContaining("Transformer overheating smoke detected");
        alarmPanelController.RemoveAlarmsContaining("Transformer failure");
    }
    
    void ShowAlarm(string message)
    {
        if (alarmPanel != null && !alarmPanel.activeSelf)
        {
            alarmPanel.SetActive(true);
            Text alarmText = alarmPanel.GetComponentInChildren<Text>();
            if (alarmText != null)
                alarmText.text = message;
            
            StartCoroutine(HideAlarmAfterSeconds(5));
        }
    }
    
    IEnumerator HideAlarmAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (alarmPanel != null)
            alarmPanel.SetActive(false);
    }
    
    void OnDestroy()
    {
        isRunning = false;
        if (mqttThread != null && mqttThread.IsAlive)
            mqttThread.Join(1000);
        
        if (stream != null)
            stream.Close();
        if (tcpClient != null)
            tcpClient.Close();
    }
    
    // ===== TERMINALCONTROLLER İÇİN GEREKLİ METODLAR =====
    
    /// <summary>
    /// Saldırı modunu ayarlar - TerminalController tarafından çağrılır
    /// </summary>
    public void SetAttackMode(bool active, string type, float value)
    {
        isUnderAttack = active;
        attackType = type;
        attackValue = value;
        
        if (!active)
        {
            // Saldırı bitti, normale dön
            dataLossSimulated = false;
            Debug.Log("[MQTT] ✅ Saldırı sona erdi, normale dönülüyor.");
            
            // Alarm panelini kapat
            if (alarmPanel != null)
                alarmPanel.SetActive(false);
        }
        else if (type == "dos")
        {
            // DoS saldırısı - veri akışını kes
            dataLossSimulated = true;
            Debug.Log("[MQTT] 🔴 DoS Saldırısı başladı! Veri akışı kesiliyor.");
        }
        else
        {
            // FDI saldırısı
            dataLossSimulated = false;
            string typeName = type == "temperature" ? "Sıcaklık" : (type == "voltage" ? "Gerilim" : "Yük");
            Debug.Log($"[MQTT] 🔴 FDI Saldırısı başladı! {typeName} = {value}");
        }
        
        // UI hemen guncelle. Terminal komutlari ana thread'den gelir.
        UpdateUI();
    }
    
    /// <summary>
    /// Son alınan sıcaklık değerini döndürür
    /// </summary>
    public float GetLastTemperature()
    {
        if (isUnderAttack && attackType == "temperature")
            return attackValue;
        return lastDisplayedTemperature;
    }

    public float GetLastDisplayedTemperature()
    {
        return lastDisplayedTemperature;
    }

    public float GetLastRealTemperature()
    {
        return lastRealTemperature;
    }

    public bool IsTelemetryAttackActive()
    {
        return telemetryAttackActive;
    }

    public string GetTelemetryAttackType()
    {
        return telemetryAttackType;
    }

    public string GetTelemetryStatus()
    {
        return telemetryStatus;
    }
    
    /// <summary>
    /// Son alınan gerilim değerini döndürür
    /// </summary>
    public float GetLastVoltage()
    {
        if (isUnderAttack && attackType == "voltage")
            return attackValue;
        return lastVoltage;
    }
    
    /// <summary>
    /// Son alınan yük değerini döndürür
    /// </summary>
    public float GetLastLoad()
    {
        if (isUnderAttack && attackType == "load")
            return attackValue;
        return lastLoad;
    }

public float GetLastOilTemperature()
    {
        return lastOilTemperature;
    }

    public float GetLastSecondaryVoltage()
    {
        return lastSecondaryVoltage;
    }

    public float GetLastPrimaryCurrent()
    {
        return lastPrimaryCurrent;
    }

    public float GetLastSecondaryCurrent()
    {
        return lastSecondaryCurrent;
    }

    public int GetMessageCounter()
    {
        return messageCounter;
    }

    public string GetLastRawJson()
    {
        return lastRawJson;
    }

    
    /// <summary>
    /// Son mesajın alındığı zamanı döndürür (Time.time cinsinden)
    /// </summary>
    public float GetLastMessageTime()
    {
        return lastMessageTime;
    }
    
    /// <summary>
    /// MQTT bağlantı durumunu döndürür
    /// </summary>
    public bool IsConnected()
    {
        return isConnected && (Time.time - lastMessageTime) < 10f;
    }
    
    /// <summary>
    /// Saldırı altında mı değil mi döndürür
    /// </summary>
    public bool IsUnderAttack()
    {
        return isUnderAttack;
    }
    
    /// <summary>
    /// Aktif saldırı tipini döndürür
    /// </summary>
    public string GetAttackType()
    {
        return attackType;
    }
}

// JSON veri sınıfı (veri.py'deki yapıyla eşleşmeli)
[System.Serializable]
public class TrafoData
{
    public float sicaklik;
    public float yag_sicaklik;
    public float gerilim_primer;
    public float gerilim_sekonder;
    public float akim_primer;
    public float akim_sekonder;
    public float yuk_seviyesi;
    public float realTemperature;
    public float displayedTemperature;
    public bool attackActive;
    public string attackType;
    public string status;
}

// Ana thread'de çalıştırmak için yardımcı sınıf
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private Queue<Action> _actions = new Queue<Action>();
    
    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("MainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    
    public void Enqueue(Action action)
    {
        lock (_actions)
        {
            _actions.Enqueue(action);
        }
    }
    
    void Update()
    {
        lock (_actions)
        {
            while (_actions.Count > 0)
            {
                _actions.Dequeue()?.Invoke();
            }
        }
    }
}
