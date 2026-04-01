using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

public class MQTTManager : MonoBehaviour
{
    [Header("MQTT Broker Settings")]
    public string brokerAddress = "172.20.10.2";
    public int securePort = 8883;
    public int insecurePort = 1883;
    public string subscribeTopic = "game";
    public bool useTLS = true;

    [Header("Authentication")]
    public string mqttUsername = "";
    public string mqttPassword = "";

    [Header("mTLS Certificates (only used when useTLS = true)")]
    public string caCertFileName = "ca.crt";
    public string clientCertFileName = "unity.pfx";
    public string clientCertPassword = "bartender";

    [Header("References")]
    public SimpleHandSimulator handSimulator;
    public CocktailManager cocktailManager;
    public GameUIManager gameUIManager;
    public RecipeOverlay recipeOverlay;

    [Header("Debug")]
    public bool showDebugLogs = true;

    [Header("Reconnection")]
    public float reconnectInterval = 5f;

    private MqttClient client;
    private int currentState = 5; // 5 = start screen
    private bool isReconnecting = false;

    void Start()
    {
#if !UNITY_EDITOR
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
#endif
        ConnectToBroker();
        InvokeRepeating(nameof(CheckConnection), 5f, 5f);
    }

    void CheckConnection()
    {
        if (client == null || !client.IsConnected)
        {
            Debug.Log("[DEBUG] 🔄 Reconnecting to MQTT...");
            ConnectToBroker();
        }
    }

    void Update()
    {
        if (!isReconnecting && (client == null || !client.IsConnected))
        {
            isReconnecting = true;
            Invoke(nameof(Reconnect), reconnectInterval);
        }
    }

    void Reconnect()
    {
        isReconnecting = false;
        Debug.Log("[DEBUG] 🔁 Attempting to reconnect...");
        ConnectToBroker();
    }

    void ConnectToBroker()
    {
        try
        {
            int port = useTLS ? securePort : insecurePort;
            Debug.Log($"[DEBUG] 🔄 Connecting to MQTT broker at {brokerAddress}:{port} (TLS: {useTLS})...");

            if (useTLS)
            {
                string caCertPath = Path.Combine(Application.streamingAssetsPath, caCertFileName);
                string clientCertPath = Path.Combine(Application.streamingAssetsPath, clientCertFileName);

                byte[] caCertBytes = File.ReadAllBytes(caCertPath);
                X509Certificate2 clientCert = new X509Certificate2(
                    File.ReadAllBytes(clientCertPath), clientCertPassword
                );

                client = new MqttClient(brokerAddress, port, true, null, clientCert, MqttSslProtocols.TLSv1_2,
                    (sender, serverCert, chain, errors) => ValidateServerCert(serverCert, caCertBytes));
            }
            else
            {
                client = new MqttClient(brokerAddress, port, false, null, null, MqttSslProtocols.None, null);
            }

            client.MqttMsgPublishReceived += OnMessageReceived;

            string clientId = "Unity_iPhone_" + Guid.NewGuid().ToString().Substring(0, 8);

            if (string.IsNullOrEmpty(mqttUsername))
                client.Connect(clientId);
            else
                client.Connect(clientId, mqttUsername, mqttPassword);

            if (client.IsConnected)
            {
                client.Subscribe(new string[] { subscribeTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
                Debug.Log($"[DEBUG] ✓ Connected and subscribed to {subscribeTopic}");
            }
            else
            {
                Debug.LogError("[DEBUG] ❌ Failed to connect to MQTT broker");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DEBUG] ❌ MQTT Connection Error: {e.Message}\n{e}");
            Debug.LogError($"[DEBUG]    Ensure broker is running at {brokerAddress}:{(useTLS ? securePort : insecurePort)}");
        }
    }

    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string message = Encoding.UTF8.GetString(e.Message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessMessage(message));
    }

    void ProcessMessage(string json)
    {
        if (showDebugLogs) Debug.Log($"[DEBUG] 📩 MQTT: {json}");

        try
        {
            MQTTMessage msg = JsonUtility.FromJson<MQTTMessage>(json);
            if (msg == null) return;

            switch (msg.state)
            {
                case 0: HandleIdle(msg);          break;
                case 1: HandleHover(msg, json);   break;
                case 2: HandleGrab(msg);           break;
                case 3: HandlePour(msg);           break;
                case 4: HandleShake(msg);          break;
                case 6: HandleGameEnd(msg);        break;
                default:
                    if (showDebugLogs) Debug.LogWarning($"[DEBUG] Unknown state: {msg.state}");
                    break;
            }

            currentState = msg.state;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DEBUG] ❌ Error parsing MQTT message: {ex.Message}");
        }
    }

    // state 0: idle — dismisses start/end screens, or shows round end mid-game
    void HandleIdle(MQTTMessage msg)
    {
        handSimulator?.ExitPourState();
        handSimulator?.OnReleaseCupButton();
        recipeOverlay?.Hide();

        gameUIManager?.OnIdle();

        if (currentState == 5 || currentState == 6)
            Debug.Log("[DEBUG] Entered idle from " + (currentState == 5 ? "start screen" : "game end"));
        else
            Debug.Log($"[DEBUG] Round {msg.round} ended — score: {msg.score} (round: {(msg.round_score == 1 ? "PASS" : "FAIL")})");
    }

    // state 6: game end
    void HandleGameEnd(MQTTMessage msg)
    {
        handSimulator?.ExitPourState();
        handSimulator?.OnReleaseCupButton();
        recipeOverlay?.Hide();
        gameUIManager?.OnGameEnd(msg.score);
        Debug.Log($"[DEBUG] Game ended — total score: {msg.score}");
    }

    // state 1: new order (has bottle_map) OR hand hover position update OR release from GRAB
    void HandleHover(MQTTMessage msg, string rawJson)
    {
        if (rawJson.Contains("\"bottle_map\""))
        {
            // New order arriving — set up the bar layout
            Dictionary<int, string> bottleMap = ParseBottleMap(rawJson);
            cocktailManager?.SetupFromMQTT(msg.drink, bottleMap);
            Debug.Log($"[DEBUG] 🍹 New order: drink {msg.drink}");
            gameUIManager?.OnNewOrder();
            var recipe = ParseRecipe(rawJson);
            recipeOverlay?.SetupRecipe(msg.drink, recipe.ingredients, recipe.shake);
        }
        else if (currentState == 2 || currentState == 3)
        {
            // Transitioning GRAB/POUR → HOVER = bottle released
            handSimulator?.ExitPourState();
            handSimulator?.OnReleaseCupButton();
        }

        // Parse hall_id manually — JsonUtility converts null to 0 which would wrongly highlight slot 0
        int hallId = ParseHallId(rawJson);
        if (hallId >= 0)
            handSimulator?.HighlightSlot(hallId);
        else
            handSimulator?.ClearHighlight();
    }

    // state 2: bottle grabbed (or returned to grab after pour/shake)
    void HandleGrab(MQTTMessage msg)
    {
        if (currentState == 3)
            handSimulator?.ExitPourState();

        // Only grab if this is a fresh GRAB transition (not returning from POUR/SHAKE)
        if (currentState != 3 && currentState != 4)
            handSimulator?.GrabObjectAtSlot(msg.picked_up);
    }

    // state 3: pouring
    void HandlePour(MQTTMessage msg)
    {
        handSimulator?.OnMQTTPour(msg.pour_target);

        if (!string.IsNullOrEmpty(msg.pour_result))
            recipeOverlay?.MarkIngredientStep(msg.pour_result);   // ingredient pour
        else if (msg.pour_target == "serving_glass" && msg.picked_up == 2)
            recipeOverlay?.MarkFinishingPour();                   // shaker → glass
    }

    // state 4: shaking
    void HandleShake(MQTTMessage msg)
    {
        handSimulator?.OnMQTTShake();
    }

    struct RecipeData { public string[] ingredients; public bool shake; }

    RecipeData ParseRecipe(string rawJson)
    {
        var result = new RecipeData();
        var ingredientMatches = Regex.Matches(rawJson, "\"ingredients\"\\s*:\\s*\\[([^\\]]*)\\]");
        if (ingredientMatches.Count > 0)
        {
            var items = Regex.Matches(ingredientMatches[0].Groups[1].Value, "\"([^\"]+)\"");
            result.ingredients = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
                result.ingredients[i] = items[i].Groups[1].Value;
        }
        else
        {
            result.ingredients = new string[0];
        }
        result.shake = rawJson.Contains("\"shake\"\\s*:\\s*true") ||
                       Regex.IsMatch(rawJson, "\"shake\"\\s*:\\s*true");
        return result;
    }

    // Returns hall_id as int, or -1 if the field is null or missing
    int ParseHallId(string rawJson)
    {
        var match = Regex.Match(rawJson, "\"hall_id\"\\s*:\\s*(\\d+|null)");
        if (!match.Success) return -1;
        string val = match.Groups[1].Value;
        if (val == "null") return -1;
        return int.Parse(val);
    }

    // Extracts { "0": "Gin", "1": "Vodka", "3": "Scotch" } into Dictionary<int, string>
    Dictionary<int, string> ParseBottleMap(string rawJson)
    {
        var result = new Dictionary<int, string>();

        int keyStart = rawJson.IndexOf("\"bottle_map\"");
        if (keyStart < 0) return result;

        int braceOpen = rawJson.IndexOf('{', keyStart);
        int braceClose = rawJson.IndexOf('}', braceOpen);
        if (braceOpen < 0 || braceClose < 0) return result;

        string section = rawJson.Substring(braceOpen + 1, braceClose - braceOpen - 1);
        var matches = Regex.Matches(section, "\"(\\d+)\"\\s*:\\s*\"([^\"]+)\"");

        foreach (Match m in matches)
            result[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;

        return result;
    }

    bool ValidateServerCert(X509Certificate serverCert, byte[] caCertBytes)
    {
        try
        {
            X509Certificate2 ca = new X509Certificate2(caCertBytes);
            X509Certificate2 server = new X509Certificate2(serverCert);
            // Accept if server cert was issued by our CA
            if (server.Issuer != ca.Subject)
            {
                Debug.LogError($"[DEBUG] ❌ Server cert issuer mismatch. Got: {server.Issuer}, expected: {ca.Subject}");
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DEBUG] ❌ Cert validation error: {e.Message}");
            return false;
        }
    }

    void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }

    void OnDestroy()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}

[System.Serializable]
public class MQTTMessage
{
    public int state;
    public int hall_id;
    public int picked_up;
    public int drink;
    public string pour_target;
    public string pour_result;
    public int round_score;
    public int round;
    public int score;
}
