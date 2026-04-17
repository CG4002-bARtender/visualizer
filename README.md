# bARtender Visualizer

AR phone visualizer for the bARtender system. Runs on iOS inside a Google Cardboard headset, rendering 3D cocktail-making visuals anchored to QR codes on a physical bar station. Receives game state over MQTT from a Python server and drives all animations in response.

## Bar Layout

```
            [ QR4: Serving Glass ]
[ QR0 ]  [ QR1 ]  [ QR2: Shaker ]  [ QR3 ]
 Slot 0   Slot 1     Mixer         Slot 3
```

Five QR codes are printed and affixed to the bar surface. Slots 0, 1, and 3 hold ingredient bottles (randomized each round). Slot 2 is always the shaker. Slot 4 is always the serving glass.

## Requirements

- Unity 2024+
- iOS device with ARKit support (iPhone/iPad)
- MQTT broker running on the local network
- mTLS certificates in `Assets/StreamingAssets/` (`ca.crt`, `unity.pfx`)

## Project Structure

```
Assets/
├── Scripts/
│   ├── MQTTManager.cs              # Central orchestrator, state machine, MQTT connection
│   ├── QRCodeManager.cs            # AR image tracking, persistent anchors, label registry
│   ├── CocktailManager.cs          # 3D bottle/glass/shaker instantiation and lifecycle
│   ├── SimpleHandSimulator.cs      # Hand interaction, grab/pour/shake animations
│   ├── GreenCircleTracker.cs       # Camera-based green marker detection for hand tracking
│   ├── GameUIManager.cs            # Screen management (start, HUD, idle, game end)
│   ├── RecipeOverlay.cs            # Recipe checklist with real-time pass/fail feedback
│   ├── TutorialManager.cs          # 9-step guided tutorial
│   ├── SplitScreenManager.cs       # Stereo rendering for Cardboard headset
│   ├── GreenDotDebugVisualizer.cs  # Debug overlay for hand tracking
│   ├── PourReceiver.cs             # Container fill tracking
│   ├── UnityMainThreadDispatcher.cs# Thread-safe MQTT-to-main-thread bridge
│   ├── BillboardLabel.cs           # Rotates labels to face camera
│   ├── BottleLabelHelper.cs        # Static label factory
│   └── ARSessionPersist.cs         # Keeps AR session alive across scene loads
├── MyPrefabs/Final/                # Bottle, glass, shaker, and drink prefabs
├── QR/                             # QR code reference images (qr0-qr4)
├── Mqtt/                           # Embedded M2Mqtt library
├── Plugins/iOS/                    # Native mDNS resolver plugin
├── StreamingAssets/                 # mTLS certificates
└── Scenes/
    └── bARtender.unity             # Main scene
```

## MQTT Communication

**Broker:** Configurable, default `bARtender.local` (TLS) or `:1883` (plaintext)

| Topic | Direction | Payload | Purpose |
|-------|-----------|---------|---------|
| `game` | Subscribe | JSON | Game state updates from server |
| `anim` | Publish | `0x01` byte | Animation completion acknowledgment |

### Game States

| State | Name | What Happens |
|-------|------|-------------|
| 0 | IDLE | Round start/end, show idle UI, display pass/fail markers |
| 1 | HOVER | New order received (spawn bottles), hand highlight updates |
| 2 | GRAB | Object picked up, follows hand position |
| 3 | POUR | Bottle tilts, liquid particle stream, recipe step updated |
| 4 | SHAKE | Shaker oscillates in sinusoidal arc |
| 5 | START_SCREEN | Start screen shown, all state reset |
| 6 | GAME_END | Final score displayed |

## Game Modes

- **Normal (0):** 3 rounds of cocktail making with ingredient validation
- **Tutorial (1):** 9-step guided walkthrough using 3 QR codes (qr1, qr2, qr4)
- **Cheat (2):** All actions accepted, for demo/testing

## Key Configuration in Unity

| Setting | Default | Location |
|---------|---------|----------|
| Broker address | `bARtender.local` | MQTTManager inspector |
| TLS enabled | `true` | MQTTManager inspector |
| Green threshold | `0.6` | GreenCircleTracker inspector |
| Saturation threshold | `0.4` | GreenCircleTracker inspector |
| Hold distance | `0.4m` | SimpleHandSimulator inspector |
| Stereo enabled | `true` | SplitScreenManager inspector |

## Packages

| Package | Version | Purpose |
|---------|---------|---------|
| AR Foundation | 5.2.2 | AR abstraction layer |
| ARKit | 5.2.2 | iOS AR backend |
| XR Management | 4.5.4 | XR subsystem lifecycle |
| TextMeshPro | 3.0.7 | Text rendering |
| M2Mqtt (embedded) | - | MQTT client with mTLS |

## Building

1. Open the project in Unity 2024+
2. Set build target to iOS
3. Place mTLS certificates (`ca.crt`, `unity.pfx`) in `Assets/StreamingAssets/`
4. Configure broker address in the MQTTManager component
5. Build and deploy to iOS device
