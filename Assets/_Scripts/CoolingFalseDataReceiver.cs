using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoolingFalseDataReceiver : MonoBehaviour
{
    [Header("MQTT Settings")]
    public string brokerAddress = "localhost";
    public int brokerPort = 1883;
    public string subscriptionTopic = "substation/#";
    public string clientId = "UnityCoolingFalseData";
    public bool logReceivedPayloads = true;
    public bool autoReconnect = true;
    public float reconnectIntervalSeconds = 2f;

    [Header("Fan Visuals")]
    public Transform[] fanObjects;
    public MonoBehaviour[] fanRotationScripts;
    public bool rotateFansWhenCoolingOn = true;
    public Vector3 fanRotationAxis = Vector3.forward;
    public float fanRotationDegreesPerSecond = 360f;

    [Header("Transformer Heat Visuals")]
    public Renderer[] transformerRenderers;
    public Material transformerMaterial;
    public Color normalColor = Color.white;
    public Color overheatedColor = new Color(1f, 0.28f, 0.04f, 1f);
    public Color normalEmissionColor = Color.black;
    public Color overheatedEmissionColor = new Color(1f, 0.22f, 0.02f, 1f);
    public float criticalTemperature = 80f;

    [Header("Transformer Oil Alarm")]
    public float normalOilTemperature = 55f;
    public float criticalOilTemperature = 90f;
    public float normalOilLevel = 78f;
    public float criticalOilLevel = 30f;
    public Color oilAlarmColor = new Color(1f, 0.05f, 0.02f, 1f);
    public Color oilAlarmEmissionColor = new Color(1f, 0.1f, 0.02f, 1f);

    [Header("Busbar Voltage Alarm")]
    public float nominalPhaseVoltage = 34.5f;
    public float voltageSagThreshold = 30f;
    public float voltageOverThreshold = 38f;
    public float voltageImbalanceThreshold = 3f;
    public Color voltageWarningColor = new Color(1f, 0.82f, 0.25f, 1f);
    public Color voltageFaultColor = new Color(1f, 0.05f, 0.02f, 1f);

    [Header("Smoke Effect")]
    public ParticleSystem smokeParticle;
    public ParticleSystem[] smokeParticles;
    public GameObject smokeObject;
    public bool configureSmokeForDemo = true;
    public bool createVisibleSmokeEmitterForDemo = true;
    public bool createVisibleSmokeCloudForDemo = true;
    public Color demoSmokeColor = new Color(0.42f, 0.42f, 0.42f, 0.82f);
    public Color demoSmokeCloudColor = new Color(0.22f, 0.22f, 0.22f, 0.72f);
    public float demoSmokeStartSize = 1.35f;
    public float demoSmokeStartSpeed = 0.85f;
    public float demoSmokeLifetime = 4f;
    public float demoSmokeEmissionRate = 45f;
    public Vector3 visibleSmokeWorldOffset = new Vector3(0f, 4.0f, 0f);
    public Vector3 visibleSmokeCloudScale = new Vector3(1.7f, 1.15f, 1.7f);

    [Header("FDI Camera Focus")]
    public bool focusCameraOnSmokeAttack = true;
    public Camera smokeFocusCamera;
    public Transform smokeFocusTarget;
    public string smokeFocusTargetName = "TransformerSmoke";
    public string safeFocusReferenceObjectName = "FPS_Player";
    public Vector3 smokeFocusOffset = new Vector3(0f, 1.3f, -7.0f);
    public Vector3 smokeLookOffset = new Vector3(0f, 0.5f, 0f);
    public float smokeFocusFieldOfView = 42f;
    public float smokeFocusHoldSeconds = 5f;
    public bool disableCinemachineBrainDuringSmokeFocus = true;

    [Header("SCADA UI")]
    public TMP_Text scadaTemperatureText;
    public TMP_Text realTemperatureLogText;
    public TMP_Text attackLogText;
    public TMP_Text securityLogText;
    public Image alarmPanelImage;
    public Color alarmSuppressedColor = new Color(0.31f, 0.95f, 0.48f, 1f);

    [Header("Controllers")]
    public AlarmPanelController alarmPanelController;
    public SCADATerminalController terminalController;
    public FDISmokeEffectController fdiSmokeEffectController;

    TcpClient tcpClient;
    NetworkStream stream;
    Thread mqttThread;
    volatile bool isRunning;
    volatile bool isConnected;
    SynchronizationContext unityContext;
    float nextReconnectTime;

    readonly Queue<MqttMessage> pendingMessages = new Queue<MqttMessage>();
    readonly object pendingMessageLock = new object();
    readonly object streamLock = new object();
    readonly Dictionary<Renderer, Color> originalRendererColors = new Dictionary<Renderer, Color>();
    readonly Dictionary<Renderer, Color> originalRendererEmissionColors = new Dictionary<Renderer, Color>();
    const int MqttHandshakeTimeoutMs = 2000;

    bool coolingOn = true;
    bool smokeOn;
    bool alarmSuppressionOn;
    bool falseDataInjectionActive;
    bool oilCriticalAlarmActive;
    bool buchholzRelayWarning;
    bool voltageSagAlarmActive;
    bool voltageOverAlarmActive;
    bool voltageImbalanceAlarmActive;
    float fakeTemperature = 42f;
    float realTemperature = 45f;
    float oilTemperature;
    float oilLevel;
    float voltageA;
    float voltageB;
    float voltageC;
    Timer smokeFocusRestoreTimer;
    bool smokeFocusActive;
    ParticleSystem visibleDemoSmokeParticle;
    GameObject visibleSmokeCloudObject;
    Material visibleSmokeCloudMaterial;

    public bool IsCoolingOn => coolingOn;
    public bool IsAlarmSuppressionActive => alarmSuppressionOn;
    public bool IsFalseDataInjectionActive => falseDataInjectionActive;
    public float FakeTemperature => fakeTemperature;
    public float RealTemperature => realTemperature;
    public bool IsOilCriticalAlarmActive => oilCriticalAlarmActive;
    public float OilTemperature => oilTemperature;
    public float OilLevel => oilLevel;
    public bool IsVoltageAlarmActive => voltageSagAlarmActive || voltageOverAlarmActive || voltageImbalanceAlarmActive;
    public bool IsVoltageSagAlarmActive => voltageSagAlarmActive;
    public bool IsVoltageOverAlarmActive => voltageOverAlarmActive;
    public bool IsVoltageImbalanceAlarmActive => voltageImbalanceAlarmActive;
    public float VoltageA => voltageA;
    public float VoltageB => voltageB;
    public float VoltageC => voltageC;

    void Start()
    {
        unityContext = SynchronizationContext.Current;
        EnsureReferences();
        CaptureOriginalMaterialState();
        ApplyNormalState();
        Connect();
    }

    void Update()
    {
        if (autoReconnect && !IsConnected() && Time.unscaledTime >= nextReconnectTime)
        {
            nextReconnectTime = Time.unscaledTime + Mathf.Max(0.5f, reconnectIntervalSeconds);
            Connect();
        }

        DrainPendingMessages();

        if (coolingOn && rotateFansWhenCoolingOn && fanObjects != null)
        {
            foreach (Transform fan in fanObjects)
            {
                if (fan != null)
                    fan.Rotate(fanRotationAxis, fanRotationDegreesPerSecond * Time.deltaTime, Space.Self);
            }
        }
    }

    public bool IsConnected()
    {
        return isConnected && tcpClient != null && tcpClient.Connected;
    }

    public void PublishStartAttack()
    {
        Publish("substation/attack/type", "false_temperature_injection");
        Publish("substation/attack/temperature/set", "START");
    }

    public void PublishStopAttack()
    {
        Publish("substation/attack/temperature/set", "STOP");
        Publish("substation/attack/type", "none");
    }

    public void HandleMessage(string topic, string payload)
    {
        string value = payload == null ? string.Empty : payload.Trim();

        switch (topic)
        {
            case "substation/attack/type":
                if (string.Equals(value, "oil_critical_alarm", StringComparison.OrdinalIgnoreCase))
                {
                    falseDataInjectionActive = true;
                    AppendLog("Transformer oil critical alarm scenario armed");
                }
                else if (string.Equals(value, "voltage_sag", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(value, "voltage_over", StringComparison.OrdinalIgnoreCase))
                {
                    falseDataInjectionActive = true;
                    AppendLog("Busbar voltage disturbance scenario armed");
                }
                else
                {
                    falseDataInjectionActive =
                        string.Equals(value, "cooling_false_data", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "false_temperature_injection", StringComparison.OrdinalIgnoreCase);
                    AppendLog(falseDataInjectionActive ? "False Data Injection Active" : "Cooling false data attack cleared");
                }
                break;

            case "substation/attack/temperature/set":
                falseDataInjectionActive =
                    string.Equals(value, "START", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase);
                AppendLog(falseDataInjectionActive ? "False Temperature Injection command sent" : "False Temperature Injection stop command sent");
                break;

            case "substation/cooling/control":
                coolingOn = !string.Equals(value, "OFF", StringComparison.OrdinalIgnoreCase);
                ApplyCoolingState();
                break;

            case "substation/sensor/temperature/fake":
            case "substation/transformer/displayed_temperature":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fake))
                    fakeTemperature = fake;
                ApplyTemperatureText();
                break;

            case "substation/transformer/temperature/real":
            case "substation/transformer/real_temperature":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float real))
                    realTemperature = real;
                ApplyTemperatureText();
                if (falseDataInjectionActive || alarmSuppressionOn)
                {
                    ApplyHeatState();
                    AppendLog($"Real transformer temperature: {realTemperature:F0} C");
                }
                break;

            case "substation/effect/smoke":
                smokeOn = string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase);
                ApplySmokeState();
                break;

            case "substation/alarm/suppression":
                alarmSuppressionOn = string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase);
                ApplyAlarmSuppressionState();
                break;

            case "substation/transformer/oil_temperature":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float oilTemp))
                    oilTemperature = oilTemp;
                if (!buchholzRelayWarning && oilTemperature < criticalOilTemperature && oilLevel > criticalOilLevel)
                    SetOilCriticalAlarm(false, "Oil temperature returned to normal");
                break;

            case "substation/transformer/oil_level":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float oilLvl))
                    oilLevel = oilLvl;
                if (!buchholzRelayWarning && oilTemperature < criticalOilTemperature && oilLevel > criticalOilLevel)
                    SetOilCriticalAlarm(false, "Oil level returned to normal");
                break;

            case "substation/protection/buchholz":
                buchholzRelayWarning =
                    string.Equals(value, "WARNING", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "TRIP", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase);
                SetOilCriticalAlarm(buchholzRelayWarning, buchholzRelayWarning ? "Buchholz relay warning received" : "Buchholz relay warning cleared");
                break;

            case "substation/transformer/oil_alarm":
                bool oilAlarmOn =
                    string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "START", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "CRITICAL", StringComparison.OrdinalIgnoreCase);
                SetOilCriticalAlarm(oilAlarmOn, oilAlarmOn ? "Oil critical alarm command received" : "Oil critical alarm cleared");
                break;

            case "substation/busbar/voltage/a":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float va))
                    voltageA = va;
                if (IsVoltageAlarmActive)
                    EvaluateVoltageAlarm("Phase A voltage updated");
                break;

            case "substation/busbar/voltage/b":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float vb))
                    voltageB = vb;
                if (IsVoltageAlarmActive)
                    EvaluateVoltageAlarm("Phase B voltage updated");
                break;

            case "substation/busbar/voltage/c":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float vc))
                    voltageC = vc;
                if (IsVoltageAlarmActive)
                    EvaluateVoltageAlarm("Phase C voltage updated");
                break;

            case "substation/busbar/voltage_alarm":
                bool voltageAlarmOn =
                    string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "START", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "CRITICAL", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "SAG", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "IMBALANCE", StringComparison.OrdinalIgnoreCase);
                if (voltageAlarmOn)
                    EvaluateVoltageAlarm("Voltage alarm command received", true);
                else
                    ClearVoltageAlarm("Voltage alarm cleared");
                break;
        }
    }

    void EnsureReferences()
    {
        if (alarmPanelController == null)
            alarmPanelController = GetComponent<AlarmPanelController>() ?? FindFirstObjectByType<AlarmPanelController>();
        if (terminalController == null)
            terminalController = GetComponent<SCADATerminalController>() ?? FindFirstObjectByType<SCADATerminalController>();
        if (fdiSmokeEffectController == null)
            fdiSmokeEffectController = FDISmokeEffectController.GetOrCreate();
        if (smokeFocusCamera == null)
            smokeFocusCamera = Camera.main;
        AutoAssignTransformerRenderers();
    }

    void AutoAssignSmokeReferences()
    {
        if (smokeParticle != null && smokeParticles != null && smokeParticles.Length > 0)
            return;

        List<ParticleSystem> foundSmokeParticles = new List<ParticleSystem>();
        ParticleSystem[] sceneParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ParticleSystem particle in sceneParticles)
        {
            if (particle == null)
                continue;

            string objectName = particle.gameObject.name;
            if (objectName.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("duman", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foundSmokeParticles.Add(particle);
            }
        }

        if (foundSmokeParticles.Count == 0)
        {
            SimpleMQTTReceiver legacyReceiver = FindFirstObjectByType<SimpleMQTTReceiver>(FindObjectsInactive.Include);
            if (legacyReceiver != null && legacyReceiver.smokeParticle != null)
                foundSmokeParticles.Add(legacyReceiver.smokeParticle);
        }

        if (foundSmokeParticles.Count == 0)
        {
            Debug.LogWarning("[CoolingFalseDataReceiver] Smoke ParticleSystem could not be found. Add a ParticleSystem named TransformerSmoke or assign smokeParticle in Inspector.");
            return;
        }

        smokeParticles = foundSmokeParticles.ToArray();
        smokeParticle = smokeParticles[0];

        if (createVisibleSmokeEmitterForDemo)
            EnsureVisibleDemoSmokeEmitter(foundSmokeParticles);

        if (smokeFocusTarget == null)
            smokeFocusTarget = smokeParticle.transform;

        if (smokeObject == null && smokeParticle != null)
            smokeObject = smokeParticle.gameObject;

        Debug.Log($"[CoolingFalseDataReceiver] Auto-assigned {smokeParticles.Length} smoke ParticleSystem reference(s).");
    }

    void EnsureVisibleDemoSmokeEmitter(List<ParticleSystem> baseSmokeParticles)
    {
        if (visibleDemoSmokeParticle != null)
            return;

        Vector3 averagePosition = Vector3.zero;
        int count = 0;
        foreach (ParticleSystem particle in baseSmokeParticles)
        {
            if (particle == null)
                continue;

            averagePosition += particle.transform.position;
            count++;
        }

        if (count == 0)
            return;

        GameObject smokeEmitter = new GameObject("FDI_VisibleSmokeEmitter");
        smokeEmitter.transform.position = (averagePosition / count) + visibleSmokeWorldOffset;
        smokeEmitter.transform.rotation = Quaternion.LookRotation(Vector3.up);
        visibleDemoSmokeParticle = smokeEmitter.AddComponent<ParticleSystem>();

        ParticleSystemRenderer renderer = smokeEmitter.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && smokeParticle != null)
        {
            ParticleSystemRenderer sourceRenderer = smokeParticle.GetComponent<ParticleSystemRenderer>();
            if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        }

        ConfigureSmokeParticleForDemo(visibleDemoSmokeParticle);
        visibleDemoSmokeParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        smokeEmitter.SetActive(false);

        if (createVisibleSmokeCloudForDemo)
            CreateVisibleSmokeCloud(smokeEmitter.transform);

        baseSmokeParticles.Add(visibleDemoSmokeParticle);
        smokeParticles = baseSmokeParticles.ToArray();
        smokeFocusTarget = smokeEmitter.transform;
    }

    void CreateVisibleSmokeCloud(Transform parent)
    {
        if (visibleSmokeCloudObject != null || parent == null)
            return;

        visibleSmokeCloudObject = new GameObject("FDI_VisibleSmokeCloud");
        visibleSmokeCloudObject.transform.position = parent.position;
        visibleSmokeCloudObject.transform.rotation = Quaternion.identity;
        visibleSmokeCloudObject.transform.SetParent(parent, true);

        Shader smokeShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (smokeShader == null)
            smokeShader = Shader.Find("Unlit/Color");
        if (smokeShader == null)
            smokeShader = Shader.Find("Standard");
        visibleSmokeCloudMaterial = new Material(smokeShader);
        visibleSmokeCloudMaterial.color = demoSmokeCloudColor;

        Vector3[] localPositions =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-0.45f, 0.35f, 0.1f),
            new Vector3(0.45f, 0.45f, -0.1f),
            new Vector3(-0.2f, 0.85f, -0.35f),
            new Vector3(0.25f, 1.05f, 0.3f),
            new Vector3(-0.6f, 1.25f, 0.2f),
            new Vector3(0.6f, 1.45f, -0.25f)
        };

        for (int i = 0; i < localPositions.Length; i++)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = $"SmokePuff_{i + 1:00}";
            puff.transform.SetParent(visibleSmokeCloudObject.transform, false);
            puff.transform.localPosition = localPositions[i];
            float scale = 0.65f + (i * 0.08f);
            puff.transform.localScale = visibleSmokeCloudScale * scale;

            Collider puffCollider = puff.GetComponent<Collider>();
            if (puffCollider != null)
                Destroy(puffCollider);

            Renderer renderer = puff.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = visibleSmokeCloudMaterial;
        }

        visibleSmokeCloudObject.SetActive(false);
    }

    void AutoAssignTransformerRenderers()
    {
        bool hasRenderer = false;
        if (transformerRenderers != null)
        {
            foreach (Renderer targetRenderer in transformerRenderers)
            {
                if (targetRenderer != null)
                {
                    hasRenderer = true;
                    break;
                }
            }
        }

        if (hasRenderer)
            return;

        SimpleMQTTReceiver legacyReceiver = FindFirstObjectByType<SimpleMQTTReceiver>(FindObjectsInactive.Include);
        if (legacyReceiver != null && legacyReceiver.transformerRenderer != null)
            transformerRenderers = new[] { legacyReceiver.transformerRenderer };
    }

    void Connect()
    {
        if (IsConnected())
            return;

        CloseMqttConnection(true);

        try
        {
            string connectAddress = NormalizeBrokerAddress(brokerAddress);
            tcpClient = new TcpClient(AddressFamily.InterNetwork);
            tcpClient.ReceiveTimeout = MqttHandshakeTimeoutMs;
            tcpClient.SendTimeout = MqttHandshakeTimeoutMs;
            tcpClient.Connect(connectAddress, brokerPort);
            stream = tcpClient.GetStream();
            stream.ReadTimeout = MqttHandshakeTimeoutMs;
            stream.WriteTimeout = MqttHandshakeTimeoutMs;

            SendConnectPacket();
            if (!WaitForConnAck())
            {
                Debug.LogWarning("[CoolingFalseDataReceiver] MQTT CONNACK was not received. Cooling attack subscription was not started.");
                return;
            }

            SendSubscribePacket(subscriptionTopic);
            WaitForSubAck();
            tcpClient.ReceiveTimeout = 0;
            stream.ReadTimeout = Timeout.Infinite;

            isRunning = true;
            mqttThread = new Thread(ListenForMessages);
            mqttThread.IsBackground = true;
            mqttThread.Start();

            isConnected = true;
            nextReconnectTime = Time.unscaledTime + reconnectIntervalSeconds;
            Debug.Log($"[CoolingFalseDataReceiver] Connected to MQTT broker {connectAddress}:{brokerPort}, subscribed to {subscriptionTopic}");
        }
        catch (Exception ex)
        {
            isConnected = false;
            isRunning = false;
            nextReconnectTime = Time.unscaledTime + Mathf.Max(0.5f, reconnectIntervalSeconds);
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT connection failed. Check broker {brokerAddress}:{brokerPort}. Error: {ex.Message}");
            CloseMqttConnection(false);
        }
    }

    string NormalizeBrokerAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "127.0.0.1";

        string trimmed = address.Trim();
        if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase))
            return "127.0.0.1";

        return trimmed;
    }

    void SendConnectPacket()
    {
        byte[] protocolName = Encoding.ASCII.GetBytes("MQTT");
        byte[] clientBytes = Encoding.ASCII.GetBytes(clientId);

        List<byte> body = new List<byte>();
        body.Add(0x00);
        body.Add((byte)protocolName.Length);
        body.AddRange(protocolName);
        body.Add(0x04);
        body.Add(0x02);
        body.Add(0x00);
        body.Add(0x3C);
        body.Add((byte)(clientBytes.Length >> 8));
        body.Add((byte)(clientBytes.Length & 0xFF));
        body.AddRange(clientBytes);

        List<byte> packet = new List<byte>();
        packet.Add(0x10);
        packet.AddRange(EncodeRemainingLength(body.Count));
        packet.AddRange(body);

        WritePacket(packet);
    }

    void SendSubscribePacket(string topic)
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

        WritePacket(packet);
    }

    void Publish(string topic, string payload)
    {
        if (!IsConnected() || stream == null)
        {
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT publish skipped; broker is not connected. Topic={topic}, Payload={payload}");
            return;
        }

        byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        List<byte> body = new List<byte>();
        body.Add((byte)(topicBytes.Length >> 8));
        body.Add((byte)(topicBytes.Length & 0xFF));
        body.AddRange(topicBytes);
        body.AddRange(payloadBytes);

        List<byte> packet = new List<byte>();
        packet.Add(0x30);
        packet.AddRange(EncodeRemainingLength(body.Count));
        packet.AddRange(body);

        try
        {
            WritePacket(packet);
            Debug.Log($"[CoolingFalseDataReceiver] Published {topic}: {payload}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT publish failed for {topic}: {ex.Message}");
        }
    }

    void ListenForMessages()
    {
        while (isRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                MqttPacket packet = ReadPacket();
                if (packet != null)
                    ProcessMqttPacket(packet);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT listen stopped: {ex.Message}");
                break;
            }
        }

        isConnected = false;
        isRunning = false;
    }

    bool WaitForConnAck()
    {
        MqttPacket packet = ReadPacket();
        return packet != null && (packet.packetType & 0xF0) == 0x20 && packet.payload.Length >= 2 && packet.payload[1] == 0x00;
    }

    void WaitForSubAck()
    {
        MqttPacket packet = ReadPacket();
        if (packet == null || (packet.packetType & 0xF0) != 0x90)
            Debug.LogWarning("[CoolingFalseDataReceiver] SUBACK was not received; continuing to listen anyway.");
    }

    MqttPacket ReadPacket()
    {
        if (stream == null)
            return null;

        int firstByte = stream.ReadByte();
        if (firstByte < 0)
            return null;

        int multiplier = 1;
        int remainingLength = 0;
        int loops = 0;
        byte encodedByte;

        do
        {
            int raw = stream.ReadByte();
            if (raw < 0)
                return null;

            encodedByte = (byte)raw;
            remainingLength += (encodedByte & 127) * multiplier;
            multiplier *= 128;
            loops++;
        }
        while ((encodedByte & 128) != 0 && loops < 4);

        byte[] payload = new byte[remainingLength];
        int offset = 0;
        while (offset < remainingLength)
        {
            int read = stream.Read(payload, offset, remainingLength - offset);
            if (read <= 0)
                return null;
            offset += read;
        }

        return new MqttPacket { packetType = (byte)firstByte, payload = payload };
    }

    void ProcessMqttPacket(MqttPacket packet)
    {
        try
        {
            if ((packet.packetType & 0xF0) != 0x30 || packet.payload.Length < 2)
                return;

            int index = 0;
            int topicLength = (packet.payload[index] << 8) | packet.payload[index + 1];
            index += 2;

            if (index + topicLength > packet.payload.Length)
                return;

            string topic = Encoding.UTF8.GetString(packet.payload, index, topicLength);
            index += topicLength;
            string payload = Encoding.UTF8.GetString(packet.payload, index, packet.payload.Length - index);

            if (!IsCoolingAttackTopic(topic))
                return;

            if (logReceivedPayloads)
                Debug.Log($"[CoolingFalseDataReceiver] Received on {topic}: {payload}");

            if (unityContext != null)
                unityContext.Post(_ => HandleMessage(topic, payload), null);
            else
                EnqueueMessage(topic, payload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT packet parse failed: {ex.Message}");
        }
    }

    bool IsCoolingAttackTopic(string topic)
    {
        return topic == "substation/attack/type" ||
               topic == "substation/attack/temperature/set" ||
               topic == "substation/cooling/control" ||
               topic == "substation/sensor/temperature/fake" ||
               topic == "substation/transformer/temperature/real" ||
               topic == "substation/transformer/displayed_temperature" ||
               topic == "substation/transformer/real_temperature" ||
               topic == "substation/effect/smoke" ||
               topic == "substation/alarm/suppression" ||
               topic == "substation/transformer/oil_temperature" ||
               topic == "substation/transformer/oil_level" ||
               topic == "substation/protection/buchholz" ||
               topic == "substation/transformer/oil_alarm" ||
               topic == "substation/busbar/voltage/a" ||
               topic == "substation/busbar/voltage/b" ||
               topic == "substation/busbar/voltage/c" ||
               topic == "substation/busbar/voltage_alarm";
    }

    void EnqueueMessage(string topic, string payload)
    {
        lock (pendingMessageLock)
        {
            pendingMessages.Enqueue(new MqttMessage { topic = topic, payload = payload });
        }
    }

    void DrainPendingMessages()
    {
        while (true)
        {
            MqttMessage message = null;
            lock (pendingMessageLock)
            {
                if (pendingMessages.Count > 0)
                    message = pendingMessages.Dequeue();
            }

            if (message == null)
                break;

            HandleMessage(message.topic, message.payload);
        }
    }

    void ApplyCoolingState()
    {
        if (fanRotationScripts != null)
        {
            foreach (MonoBehaviour script in fanRotationScripts)
            {
                if (script != null)
                    script.enabled = coolingOn;
            }
        }

        AppendLog(coolingOn ? "Cooling system restored" : "Cooling system disabled");
    }

    void ApplyHeatState()
    {
        bool overheated = realTemperature > criticalTemperature;
        Color color = overheated ? overheatedColor : normalColor;
        Color emission = overheated ? overheatedEmissionColor : normalEmissionColor;

        if (transformerMaterial != null)
            ApplyMaterialHeat(transformerMaterial, color, emission);

        if (transformerRenderers != null)
        {
            foreach (Renderer targetRenderer in transformerRenderers)
            {
                if (targetRenderer == null)
                    continue;

                Material material = targetRenderer.material;
                ApplyMaterialHeat(material, color, emission);
            }
        }

        if (overheated)
        {
            AppendLog(alarmSuppressionOn ? "Critical transformer temperature alarm suppressed" : "Critical transformer temperature alarm active");
            AppendSecurityLog("Data Integrity Attack Detected");
        }
    }

    void ApplyMaterialHeat(Material material, Color color, Color emission)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
        {
            if (emission.maxColorComponent > 0f)
                material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
    }

    void ApplySmokeState()
    {
        if (fdiSmokeEffectController == null)
            fdiSmokeEffectController = FDISmokeEffectController.GetOrCreate();
        if (fdiSmokeEffectController != null)
        {
            if (smokeOn)
                fdiSmokeEffectController.StartSmokeAttack();
            else
                fdiSmokeEffectController.StopSmokeAttack();
            return;
        }

        if (smokeObject != null)
            smokeObject.SetActive(smokeOn);
        if (visibleSmokeCloudObject != null)
            visibleSmokeCloudObject.SetActive(smokeOn);

        if (smokeOn && focusCameraOnSmokeAttack)
            StartSmokeFocusSequence();

        if (smokeParticles != null && smokeParticles.Length > 0)
        {
            foreach (ParticleSystem particle in smokeParticles)
                ApplySmokeParticle(particle);
            return;
        }

        if (smokeParticle == null)
            return;

        ApplySmokeParticle(smokeParticle);
    }

    void ApplySmokeParticle(ParticleSystem particle)
    {
        if (particle == null)
            return;

        if (configureSmokeForDemo)
            ConfigureSmokeParticleForDemo(particle);

        particle.gameObject.SetActive(smokeOn);

        if (smokeOn)
        {
            if (!particle.isPlaying)
                particle.Play(true);
            particle.Emit(80);
        }
        else
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void SetOilCriticalAlarm(bool active, string reason)
    {
        if (active)
        {
            bool firstActivation = !oilCriticalAlarmActive;
            oilCriticalAlarmActive = true;
            falseDataInjectionActive = true;
            ApplyOilAlarmVisualState();

            if (fdiSmokeEffectController == null)
                fdiSmokeEffectController = FDISmokeEffectController.GetOrCreate();
            if (fdiSmokeEffectController != null)
                fdiSmokeEffectController.StartSmokeAttack();

            if (firstActivation)
            {
                AppendLog("OIL TEMP HIGH");
                AppendLog("BUCHHOLZ RELAY WARNING");
                AppendLog($"Oil temperature: {oilTemperature:F0} C");
                AppendLog($"Oil level: {oilLevel:F0}%");
                AppendSecurityLog("Transformer Oil Critical Alarm Attack Detected");

                if (alarmPanelController != null)
                {
                    alarmPanelController.AddAlarm("OIL TEMP HIGH", AlarmPanelController.AlarmSeverity.Critical);
                    alarmPanelController.AddAlarm("BUCHHOLZ RELAY WARNING", AlarmPanelController.AlarmSeverity.Critical);
                }
            }

            Debug.LogWarning($"[CoolingFalseDataReceiver][OIL] {reason}");
            return;
        }

        if (!oilCriticalAlarmActive)
            return;

        oilCriticalAlarmActive = false;
        buchholzRelayWarning = false;
        falseDataInjectionActive = false;
        oilTemperature = normalOilTemperature;
        oilLevel = normalOilLevel;
        RestoreOriginalMaterialState();

        if (fdiSmokeEffectController != null)
            fdiSmokeEffectController.StopSmokeAttack();

        AppendLog("Oil critical alarm cleared");
        AppendLog($"Oil temperature: {oilTemperature:F0} C");
        AppendLog($"Oil level: {oilLevel:F0}%");
        Debug.Log($"[CoolingFalseDataReceiver][OIL] {reason}");
    }

    void ApplyOilAlarmVisualState()
    {
        if (transformerMaterial != null)
            ApplyMaterialHeat(transformerMaterial, oilAlarmColor, oilAlarmEmissionColor);

        if (transformerRenderers == null)
            return;

        foreach (Renderer targetRenderer in transformerRenderers)
        {
            if (targetRenderer == null)
                continue;

            ApplyMaterialHeat(targetRenderer.material, oilAlarmColor, oilAlarmEmissionColor);
        }
    }

    void EvaluateVoltageAlarm(string reason, bool forceAlarm = false)
    {
        bool sag = voltageA < voltageSagThreshold || voltageB < voltageSagThreshold || voltageC < voltageSagThreshold;
        bool over = voltageA > voltageOverThreshold || voltageB > voltageOverThreshold || voltageC > voltageOverThreshold;
        float maxVoltage = Mathf.Max(voltageA, Mathf.Max(voltageB, voltageC));
        float minVoltage = Mathf.Min(voltageA, Mathf.Min(voltageB, voltageC));
        bool imbalance = maxVoltage - minVoltage >= voltageImbalanceThreshold;
        bool active = forceAlarm || sag || over || imbalance;

        if (!active)
        {
            ClearVoltageAlarm(reason);
            return;
        }

        bool firstActivation = !IsVoltageAlarmActive;
        voltageSagAlarmActive = sag;
        voltageOverAlarmActive = over;
        voltageImbalanceAlarmActive = imbalance || (forceAlarm && !sag && !over);
        falseDataInjectionActive = true;

        if (firstActivation)
        {
            string voltageAlarmLabel = sag ? "UNDER VOLTAGE ALARM" : over ? "OVER VOLTAGE ALARM" : "VOLTAGE IMBALANCE ALARM";

            if (fdiSmokeEffectController == null)
                fdiSmokeEffectController = FDISmokeEffectController.GetOrCreate();
            if (fdiSmokeEffectController != null)
                fdiSmokeEffectController.StartSmokeAttack();

            AppendLog("BUSBAR VOLTAGE SAG / IMBALANCE DETECTED");
            AppendLog($"Voltage A: {voltageA:F1} kV");
            AppendLog($"Voltage B: {voltageB:F1} kV");
            AppendLog($"Voltage C: {voltageC:F1} kV");
            AppendLog(voltageAlarmLabel);
            AppendLog("IED VOLTAGE PROTECTION WARNING");
            AppendSecurityLog("Voltage Integrity Attack Detected");

            if (alarmPanelController != null)
            {
                alarmPanelController.AddAlarm("BUSBAR VOLTAGE SAG / IMBALANCE", AlarmPanelController.AlarmSeverity.Critical);
                alarmPanelController.AddAlarm(voltageAlarmLabel, AlarmPanelController.AlarmSeverity.Critical);
            }
        }

        Debug.LogWarning($"[CoolingFalseDataReceiver][VOLTAGE] {reason}");
    }

    void ClearVoltageAlarm(string reason)
    {
        if (!IsVoltageAlarmActive)
            return;

        voltageSagAlarmActive = false;
        voltageOverAlarmActive = false;
        voltageImbalanceAlarmActive = false;
        falseDataInjectionActive = oilCriticalAlarmActive || alarmSuppressionOn;
        voltageA = nominalPhaseVoltage;
        voltageB = nominalPhaseVoltage;
        voltageC = nominalPhaseVoltage;

        if (fdiSmokeEffectController != null && !oilCriticalAlarmActive && !smokeOn)
            fdiSmokeEffectController.StopSmokeAttack();

        AppendLog("Busbar voltage alarm cleared");
        AppendLog($"Voltage A/B/C: {voltageA:F1}/{voltageB:F1}/{voltageC:F1} kV");
        Debug.Log($"[CoolingFalseDataReceiver][VOLTAGE] {reason}");
    }

    void ConfigureSmokeParticleForDemo(ParticleSystem particle)
    {
        ParticleSystem.MainModule main = particle.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = demoSmokeLifetime;
        main.startSpeed = demoSmokeStartSpeed;
        main.startSize = demoSmokeStartSize;
        main.startColor = demoSmokeColor;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.enabled = true;
        emission.rateOverTime = demoSmokeEmissionRate;

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = true;
        shape.angle = 22f;
        shape.radius = 0.25f;

        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            renderer.maxParticleSize = 2.5f;
            renderer.sortingFudge = 2f;
        }
    }

    void StartSmokeFocusSequence()
    {
        Transform target = ResolveSmokeFocusTarget();
        Camera cameraToMove = smokeFocusCamera != null ? smokeFocusCamera : Camera.main;

        if (target == null || cameraToMove == null)
        {
            Debug.LogWarning("[CoolingFalseDataReceiver] Smoke focus skipped; camera or smoke target is missing.");
            return;
        }

        StartSmokeFocus(cameraToMove, target);
    }

    Transform ResolveSmokeFocusTarget()
    {
        if (smokeFocusTarget != null)
            return smokeFocusTarget;

        if (!string.IsNullOrWhiteSpace(smokeFocusTargetName))
        {
            GameObject namedTarget = GameObject.Find(smokeFocusTargetName);
            if (namedTarget != null)
            {
                smokeFocusTarget = namedTarget.transform;
                return smokeFocusTarget;
            }
        }

        if (smokeParticle != null)
            return smokeParticle.transform;

        if (smokeParticles != null)
        {
            foreach (ParticleSystem particle in smokeParticles)
            {
                if (particle != null)
                    return particle.transform;
            }
        }

        return null;
    }

    void StartSmokeFocus(Camera cameraToMove, Transform target)
    {
        Transform cameraTransform = cameraToMove.transform;
        Transform originalParent = cameraTransform.parent;
        Vector3 originalLocalPosition = cameraTransform.localPosition;
        Quaternion originalLocalRotation = cameraTransform.localRotation;
        Vector3 originalWorldPosition = cameraTransform.position;
        Quaternion originalWorldRotation = cameraTransform.rotation;
        float originalFieldOfView = cameraToMove.fieldOfView;

        Behaviour brain = cameraToMove.GetComponent("CinemachineBrain") as Behaviour;
        bool brainWasEnabled = brain != null && brain.enabled;

        FPSKontrol fpsKontrol = FindFirstObjectByType<FPSKontrol>(FindObjectsInactive.Include);
        bool fpsWasEnabled = fpsKontrol != null && fpsKontrol.enabled;

        if (fpsKontrol != null)
            fpsKontrol.enabled = false;

        if (disableCinemachineBrainDuringSmokeFocus && brain != null)
            brain.enabled = false;

        cameraTransform.SetParent(null, true);
        cameraToMove.fieldOfView = smokeFocusFieldOfView;

        Vector3 lookPoint = target.position + smokeLookOffset;
        cameraTransform.position = GetSmokeFocusPosition(target, originalWorldPosition);
        cameraTransform.LookAt(lookPoint);

        Debug.Log("[CoolingFalseDataReceiver] Smoke focus started. Camera will restore after 5 seconds.");

        smokeFocusActive = true;
        smokeFocusRestoreTimer?.Dispose();
        smokeFocusRestoreTimer = new Timer(_ =>
        {
            SynchronizationContext context = unityContext;
            if (context != null)
            {
                context.Post(__ => RestoreSmokeFocus(cameraToMove, cameraTransform, originalParent, originalLocalPosition, originalLocalRotation, originalWorldPosition, originalWorldRotation, originalFieldOfView, brain, brainWasEnabled, fpsKontrol, fpsWasEnabled), null);
            }
            else
            {
                Debug.LogWarning("[CoolingFalseDataReceiver] Smoke focus restore skipped; Unity SynchronizationContext is missing.");
            }
        }, null, (int)(Mathf.Max(0.1f, smokeFocusHoldSeconds) * 1000f), Timeout.Infinite);
    }

    void RestoreSmokeFocus(
        Camera cameraToMove,
        Transform cameraTransform,
        Transform originalParent,
        Vector3 originalLocalPosition,
        Quaternion originalLocalRotation,
        Vector3 originalWorldPosition,
        Quaternion originalWorldRotation,
        float originalFieldOfView,
        Behaviour brain,
        bool brainWasEnabled,
        FPSKontrol fpsKontrol,
        bool fpsWasEnabled)
    {
        if (!smokeFocusActive)
            return;

        if (cameraToMove != null && cameraTransform != null)
        {
            cameraToMove.fieldOfView = originalFieldOfView;
            cameraTransform.SetParent(originalParent, true);

            if (originalParent != null)
            {
                cameraTransform.localPosition = originalLocalPosition;
                cameraTransform.localRotation = originalLocalRotation;
            }
            else
            {
                cameraTransform.position = originalWorldPosition;
                cameraTransform.rotation = originalWorldRotation;
            }
        }

        if (brain != null)
            brain.enabled = brainWasEnabled;

        if (fpsKontrol != null)
        {
            fpsKontrol.enabled = fpsWasEnabled;
            if (fpsWasEnabled)
                fpsKontrol.SetTerminalDurumu(false);
        }

        smokeFocusRestoreTimer?.Dispose();
        smokeFocusRestoreTimer = null;
        smokeFocusActive = false;
        Debug.Log("[CoolingFalseDataReceiver] Smoke focus finished. Camera restored to previous user view.");
    }

    Vector3 GetSmokeFocusPosition(Transform target, Vector3 viewerWorldPosition)
    {
        return target.position + smokeFocusOffset;
    }

    Transform FindFocusReference(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject referenceObject = GameObject.Find(objectName);
        return referenceObject != null ? referenceObject.transform : null;
    }

    void ApplyAlarmSuppressionState()
    {
        if (alarmSuppressionOn)
        {
            AppendLog("Alarm Suppression Active");
            AppendLog("False Data Injection Active");
            AppendSecurityLog("Data Integrity Attack Detected");

            if (alarmPanelImage != null)
                alarmPanelImage.color = alarmSuppressedColor;
        }
        else
        {
            AppendLog("Alarm suppression cleared");
        }
    }

    void ApplyTemperatureText()
    {
        if (scadaTemperatureText != null)
            scadaTemperatureText.text = $"{fakeTemperature:F0} C";
        if (realTemperatureLogText != null)
            realTemperatureLogText.text = $"Real transformer temperature: {realTemperature:F0} C";
    }

    void ApplyNormalState()
    {
        coolingOn = true;
        smokeOn = false;
        alarmSuppressionOn = false;
        falseDataInjectionActive = false;
        oilCriticalAlarmActive = false;
        buchholzRelayWarning = false;
        voltageSagAlarmActive = false;
        voltageOverAlarmActive = false;
        voltageImbalanceAlarmActive = false;
        fakeTemperature = 42f;
        realTemperature = 45f;
        oilTemperature = normalOilTemperature;
        oilLevel = normalOilLevel;
        voltageA = nominalPhaseVoltage;
        voltageB = nominalPhaseVoltage;
        voltageC = nominalPhaseVoltage;

        ApplyCoolingState();
        RestoreOriginalMaterialState();
        ApplySmokeState();
        ApplyTemperatureText();
    }

    void CaptureOriginalMaterialState()
    {
        if (transformerRenderers == null)
            return;

        foreach (Renderer targetRenderer in transformerRenderers)
        {
            if (targetRenderer == null || originalRendererColors.ContainsKey(targetRenderer))
                continue;

            Material material = targetRenderer.material;
            originalRendererColors[targetRenderer] = GetMaterialColor(material, normalColor);
            originalRendererEmissionColors[targetRenderer] = GetMaterialEmission(material, normalEmissionColor);
        }
    }

    void RestoreOriginalMaterialState()
    {
        foreach (KeyValuePair<Renderer, Color> entry in originalRendererColors)
        {
            if (entry.Key == null)
                continue;

            Material material = entry.Key.material;
            Color emission = originalRendererEmissionColors.ContainsKey(entry.Key)
                ? originalRendererEmissionColors[entry.Key]
                : normalEmissionColor;
            ApplyMaterialHeat(material, entry.Value, emission);
        }

        if (transformerMaterial != null)
            ApplyMaterialHeat(transformerMaterial, normalColor, normalEmissionColor);
    }

    Color GetMaterialColor(Material material, Color fallback)
    {
        if (material == null)
            return fallback;
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return fallback;
    }

    Color GetMaterialEmission(Material material, Color fallback)
    {
        if (material != null && material.HasProperty("_EmissionColor"))
            return material.GetColor("_EmissionColor");
        return fallback;
    }

    void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (attackLogText != null)
            attackLogText.text = line;
        if (terminalController != null)
            terminalController.WriteExternalLine(line);
        Debug.Log($"[CoolingFalseDataReceiver] {message}");
    }

    void AppendSecurityLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (securityLogText != null)
            securityLogText.text = line;
        if (terminalController != null)
            terminalController.WriteExternalLine(line);
        Debug.LogWarning($"[CoolingFalseDataReceiver][IDS] {message}");
    }

    void WritePacket(List<byte> packet)
    {
        lock (streamLock)
        {
            if (stream != null)
                stream.Write(packet.ToArray(), 0, packet.Count);
        }
    }

    static List<byte> EncodeRemainingLength(int length)
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

    void OnDestroy()
    {
        CloseMqttConnection(true);
        smokeFocusRestoreTimer?.Dispose();
        smokeFocusRestoreTimer = null;
    }

    void CloseMqttConnection(bool waitForThread)
    {
        isRunning = false;
        isConnected = false;

        lock (streamLock)
        {
            if (stream != null)
            {
                try { stream.Close(); }
                catch (Exception) { }
                stream = null;
            }

            if (tcpClient != null)
            {
                try { tcpClient.Close(); }
                catch (Exception) { }
                tcpClient = null;
            }
        }

        if (waitForThread && mqttThread != null && mqttThread.IsAlive)
            mqttThread.Join(1000);

        mqttThread = null;
    }

    class MqttPacket
    {
        public byte packetType;
        public byte[] payload;
    }

    class MqttMessage
    {
        public string topic;
        public string payload;
    }
}
