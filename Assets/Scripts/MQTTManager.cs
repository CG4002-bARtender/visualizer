using System;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;

public class MQTTManager : MonoBehaviour
{
    [Header("MQTT Broker Settings")]
    public string brokerAddress = "172.20.10.2";
    public int brokerPort = 1883;  // ← Changed from 8883 to 1883 (no TLS)
    public string subscribeTopic = "game/state";
    
    [Header("Authentication")]
    public string mqttUsername = "";
    public string mqttPassword = "";
    
    [Header("References")]
    public SimpleHandSimulator handSimulator;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private MqttClient client;
    private bool isConnected = false;
    
    void Start()
    {
        ConnectToBroker();
    }
    
    void ConnectToBroker()
    {
        try
        {
            Debug.Log($"🔄 Connecting to MQTT broker at {brokerAddress}:{brokerPort}...");
            
            // Create MQTT client WITHOUT TLS (simple and works!)
            client = new MqttClient(brokerAddress, brokerPort, false, null, null, MqttSslProtocols.None);
            
            // Register callback for received messages
            client.MqttMsgPublishReceived += OnMessageReceived;
            
            // Generate unique client ID
            string clientId = "Unity_iPhone_" + Guid.NewGuid().ToString().Substring(0, 8);
            
            Debug.Log($"📱 Client ID: {clientId}");
            
            // Connect
            if (string.IsNullOrEmpty(mqttUsername))
            {
                client.Connect(clientId);
            }
            else
            {
                client.Connect(clientId, mqttUsername, mqttPassword);
            }
            
            if (client.IsConnected)
            {
                isConnected = true;
                Debug.Log($"✓ Connected to MQTT broker!");
                
                // Subscribe to topic
                client.Subscribe(new string[] { subscribeTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
                Debug.Log($"✓ Subscribed to topic: {subscribeTopic}");
            }
            else
            {
                Debug.LogError("❌ Failed to connect to MQTT broker");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ MQTT Connection Error: {e.Message}");
            Debug.LogError($"   Stack: {e.StackTrace}");
            Debug.LogError($"   Make sure:");
            Debug.LogError($"   1. Broker is running on {brokerAddress}:{brokerPort}");
            Debug.LogError($"   2. Both devices on WiFi: IphoneAlam");
            Debug.LogError($"   3. Firewall allows port {brokerPort}");
        }
    }
    
    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        // This runs on MQTT thread, need to process on main thread
        string message = Encoding.UTF8.GetString(e.Message);
        
        // Queue for main thread processing
        UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessMessage(message));
    }
    
    void ProcessMessage(string jsonMessage)
    {
        if (showDebugLogs)
        {
            Debug.Log($"📩 MQTT: {jsonMessage}");
        }
        
        try
        {
            // Parse JSON
            GameStateData data = JsonUtility.FromJson<GameStateData>(jsonMessage);
            
            if (data == null)
            {
                Debug.LogWarning($"⚠ Failed to parse JSON: {jsonMessage}");
                return;
            }
            
            // Process based on message type
            switch (data.type)
            {
                case "hand_position":
                    HandleHandPosition(data);
                    break;
                    
                case "action":
                    HandleAction(data);
                    break;
                    
                default:
                    if (showDebugLogs)
                        Debug.Log($"⚠ Unknown message type: {data.type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Error parsing JSON: {ex.Message}");
        }
    }
    
    void HandleHandPosition(GameStateData data)
    {
        if (handSimulator != null)
        {
            // Send hand position to simulator
            handSimulator.OnHandPositionReceived(data.handX, data.handY);
            
            if (showDebugLogs)
            {
                Debug.Log($"👋 Hand: ({data.handX:F2}, {data.handY:F2})");
            }
        }
    }
    
    void HandleAction(GameStateData data)
    {
        if (handSimulator != null)
        {
            // Trigger action (grab, pour, release)
            handSimulator.SimulateFakeInput(data.action);
            
            if (showDebugLogs)
            {
                Debug.Log($"🎮 Action: {data.action}");
            }
        }
    }
    
    void OnApplicationQuit()
    {
        if (client != null && client.IsConnected)
        {
            client.Disconnect();
            Debug.Log("✓ Disconnected from MQTT");
        }
    }
    
    void OnDestroy()
    {
        if (client != null && client.IsConnected)
        {
            client.Disconnect();
        }
    }
}

// JSON data structure matching your glove's messages
[System.Serializable]
public class GameStateData
{
    public string type;      // "hand_position" or "action"
    public float handX;      // 0.0 to 1.0
    public float handY;      // 0.0 to 1.0
    public string action;    // "grab", "release", "pour"
}