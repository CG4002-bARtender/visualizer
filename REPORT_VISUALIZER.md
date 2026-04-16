# Visualizer Design & Software Architecture

## 1. Visualizer Design

### 1.1 Data Displayed

The phone visualizer serves as the augmented reality display for the bARtender system, rendering the following data overlaid onto the real-world bar station:

| Data Element | Visual Representation | When Displayed |
|---|---|---|
| **Bottle identities** | 3D bottle models (Gin, Vodka, Bourbon, Scotch, etc.) with floating text labels | During active gameplay, anchored to QR-tracked positions |
| **Shaker** | 3D shaker model | Always present during a round at position 2 |
| **Serving glass** | Drink-specific empty glass model (e.g., Martini coupe, rocks glass) | At serving position (qr4) during a round |
| **Recipe overlay** | Step-by-step recipe card with real-time progress (green = correct, red = wrong) | During an active order |
| **Hand position** | Highlight glow on the nearest bottle/object (gold tint) | When the glove's hall-effect sensor detects proximity |
| **Pour animation** | Bottle tilts 60 degrees, particle-based liquid stream with ingredient-specific color | During pour state |
| **Shake animation** | Shaker moves in a sinusoidal arc (3 Hz, 0.08 m amplitude) | During shake state |
| **Round / Score HUD** | "Round N" and "Score: X" text | Throughout active gameplay |
| **Pass/Fail markers** | 3D checkmark or cross marker above the serving glass | After each round ends |
| **Final drink** | Completed cocktail 3D model replacing the empty glass | On successful round completion |
| **Game mode indicator** | Green highlight on the selected mode box (Normal / Tutorial / Cheat) | On the start screen during mode selection |
| **QR scanning progress** | "Scanning QR codes... (N/5)" with instruction text | Before gameplay when not all QR codes are detected |
| **MQTT connection status** | "MQTT: Connected to [broker]" text, auto-hides after 5 seconds | On initial connection |
| **Tutorial instructions** | Step-by-step text ("Grab bottle", "Pour into shaker", etc.) | During tutorial mode |

### 1.2 Phone Placement

The phone is mounted inside a Google Cardboard-style VR headset worn by the bartender. This placement was chosen for several reasons:

1. **Hands-free operation**: The bartender's hands must remain free to interact with the physical bar station (picking up bottles, shaking, pouring). A handheld phone would be impractical.
2. **First-person AR perspective**: Mounting the phone at eye level inside a headset ensures the AR overlays align naturally with the bartender's field of view. The camera sees what the bartender sees.
3. **Split-screen stereo rendering**: The `SplitScreenManager` divides the screen into left-eye and right-eye viewports, each occupying half the display. A `CommandBuffer` blits the AR camera background into the right eye before the 3D scene renders. The inter-pupillary distance (IPD) offset is configurable. This provides a stereoscopic 3D effect through the Cardboard lenses.
4. **Stable tracking**: A head-mounted position provides a relatively stable camera platform compared to a handheld device, which improves AR tracking consistency.

The physical bar station layout from the bartender's perspective is:

```
         [ QR4: Serving Glass ]
[ QR0 ]  [ QR1 ]  [ QR2: Shaker ]  [ QR3 ]
 Slot 0   Slot 1     Mixer         Slot 3
```

Five printed QR codes are affixed to the bar surface at these fixed positions. Slots 0, 1, and 3 hold ingredient bottles (randomized each round), slot 2 always holds the shaker, and slot 4 always holds the serving glass.

### 1.3 Design Constraints

| Constraint | Impact | Mitigation |
|---|---|---|
| **Limited phone processing power** | AR tracking + 3D rendering + MQTT + camera-based hand detection all compete for CPU/GPU | Green dot detection downscales camera frames to 1/4 resolution; only every 6th pixel is sampled; detection runs every 2nd frame |
| **AR tracking stability** | QR codes can be lost when occluded by hands or bottles; `TrackingState.Limited` can report incorrect positions | Persistent anchor system decouples game objects from `ARTrackedImage` lifecycle; anchors freeze at last known good position when tracking degrades |
| **Network latency** | MQTT messages must arrive in real time for responsive animations | QoS level 0 (fire-and-forget) chosen for lowest latency; animation ACK protocol ensures server and visualizer stay synchronized |
| **Small display in headset** | All UI must be readable through Cardboard lenses at close range | UI elements use large TextMeshPro fonts; minimal on-screen clutter; recipe overlay uses simple checklist format |
| **Single-camera stereo** | Only one physical camera exists; true stereo depth is not possible | Right eye reuses the same AR background via `CommandBuffer` blit; the IPD offset provides parallax for 3D-rendered objects only, not the camera feed |
| **Outdoor/variable lighting** | Lighting changes affect QR detection and green dot tracking | Green pixel detection uses saturation-based thresholding (saturation > 0.4, green channel > 0.6) which is more robust than raw RGB thresholds |
| **mTLS certificate management** | Certificates must be bundled with the iOS build | Certificates stored in `StreamingAssets/` (ca.crt, unity.pfx); native iOS mDNS resolution via a custom Objective-C plugin for `.local` hostname discovery |

---

## 2. Visualizer Software Architecture

### 2.1 Software Framework and Libraries

**Game Engine:** Unity (2024+) targeting iOS (iPhone/iPad)

**Core AR/XR Packages:**

| Package | Version | Purpose |
|---|---|---|
| AR Foundation | 5.2.2 | Cross-platform AR abstraction layer |
| AR Kit | 5.2.2 | iOS-specific AR backend (plane detection, image tracking) |
| XR Hands | 1.4.3 | Hand tracking support APIs |
| XR Management | 4.5.4 | XR subsystem lifecycle management |
| Google Cardboard XR Plugin | (git) | Stereo split-screen rendering for Cardboard headsets |
| TextMeshPro | 3.0.7 | High-quality text rendering for UI and floating labels |

**Communication Library:**

| Library | Purpose |
|---|---|
| M2Mqtt (uPLibrary) | Embedded C# MQTT client with TLS/mTLS support |
| Native iOS mDNS Plugin (MDNSResolver.mm) | Objective-C plugin for resolving `.local` hostnames on iOS where Mono's `Dns.GetHostEntry` fails |

### 2.2 Module Architecture

The visualizer is composed of 12 C# modules organized into four functional layers:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                              │
│                                                                        │
│  ┌──────────────────┐  ┌───────────────────┐  ┌─────────────────────┐  │
│  │  GameUIManager   │  │  RecipeOverlay     │  │ GreenDotDebug       │  │
│  │  ─────────────   │  │  ──────────────    │  │ Visualizer          │  │
│  │  Start screen    │  │  Recipe steps      │  │ ────────────────    │  │
│  │  HUD (round,     │  │  Ingredient        │  │  Crosshair overlay  │  │
│  │    score)        │  │    pass/fail       │  │  Debug HUD          │  │
│  │  Game end screen │  │  Shake/pour marks  │  │  Stereo support     │  │
│  │  Idle info panel │  │  Drink name        │  │                     │  │
│  │  Mode highlights │  │                    │  │                     │  │
│  └──────────────────┘  └───────────────────┘  └─────────────────────┘  │
│                                                                        │
│  ┌──────────────────┐  ┌───────────────────┐                           │
│  │  TutorialManager │  │  BillboardLabel   │                           │
│  │  ─────────────── │  │  ──────────────── │                           │
│  │  9-step guided   │  │  Rotates labels   │                           │
│  │    tutorial      │  │  to face camera   │                           │
│  │  Instruction text│  │                   │                           │
│  │  End panel       │  │                   │                           │
│  └──────────────────┘  └───────────────────┘                           │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       GAME LOGIC LAYER                                 │
│                                                                        │
│  ┌──────────────────┐  ┌───────────────────┐  ┌─────────────────────┐  │
│  │  MQTTManager     │  │ CocktailManager   │  │ SimpleHand          │  │
│  │  ────────────    │  │ ────────────────  │  │ Simulator           │  │
│  │  State machine   │  │ Bottle/glass/     │  │ ──────────────────  │  │
│  │  (7 states)      │  │   shaker prefab   │  │  Grab/release       │  │
│  │  Message parsing │  │   instantiation   │  │  Pour animation     │  │
│  │  Mode switching  │  │ Ingredient→prefab │  │  (tilt + particles) │  │
│  │  Order buffering │  │   mapping         │  │  Shake animation    │  │
│  │  Anim ACK        │  │ Pass/fail markers │  │  (sinusoidal arc)   │  │
│  │  mTLS connection │  │ Final drink       │  │  Object highlight   │  │
│  │  Auto-reconnect  │  │   display         │  │  (gold tint)        │  │
│  └──────────────────┘  └───────────────────┘  └─────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       AR TRACKING LAYER                                │
│                                                                        │
│  ┌──────────────────┐  ┌───────────────────┐  ┌─────────────────────┐  │
│  │  QRCodeManager   │  │ GreenCircleTracker│  │ SplitScreenManager  │  │
│  │  ──────────────  │  │ ────────────────  │  │ ──────────────────  │  │
│  │  ARTrackedImage  │  │ Camera frame      │  │  Left/right eye     │  │
│  │    listener      │  │   acquisition     │  │    viewports        │  │
│  │  Persistent      │  │ Green pixel       │  │  CommandBuffer      │  │
│  │    anchors       │  │   detection       │  │    blit for AR bg   │  │
│  │  Label registry  │  │ Centroid + grip   │  │  IPD offset         │  │
│  │  Object registry │  │   offset calc     │  │  Projection sync    │  │
│  │  Scan control    │  │ Downscaled        │  │                     │  │
│  │                  │  │   processing      │  │                     │  │
│  └──────────────────┘  └───────────────────┘  └─────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       INFRASTRUCTURE LAYER                             │
│                                                                        │
│  ┌────────────────────────────┐  ┌──────────────────────────────────┐  │
│  │ UnityMainThreadDispatcher  │  │  BottleLabelHelper               │  │
│  │ ────────────────────────── │  │  ──────────────────────────────  │  │
│  │ Thread-safe action queue   │  │  Static factory for floating     │  │
│  │ MQTT thread → main thread  │  │    labels with BillboardLabel    │  │
│  └────────────────────────────┘  └──────────────────────────────────┘  │
│                                                                        │
│  ┌────────────────────────────┐  ┌──────────────────────────────────┐  │
│  │ PourReceiver               │  │  ARSessionPersist               │  │
│  │ ────────────────────────── │  │  ──────────────────────────────  │  │
│  │ Container fill tracking    │  │  DontDestroyOnLoad for AR       │  │
│  │ Capacity limits (shaker,   │  │    session across scene loads   │  │
│  │   serving glass, bottle)   │  │                                  │  │
│  └────────────────────────────┘  └──────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

#### Module Descriptions

**MQTTManager** (Central Orchestrator, 647 lines): The hub of the visualizer. Connects to the MQTT broker over mTLS (port 8883) or plaintext (port 1883). Subscribes to the `game` topic and dispatches incoming JSON messages through a 7-state state machine (START_SCREEN, IDLE, HOVER, GRAB, POUR, SHAKE, GAME_END). Publishes single-byte ACK messages on the `anim` topic to signal animation completion to the server. Handles automatic reconnection every 5 seconds and buffers incoming drink orders until all required QR codes are detected.

**QRCodeManager** (AR Anchor System, 267 lines): Listens to `ARTrackedImagesChangedEventArgs` from AR Foundation's `ARTrackedImageManager`. Maintains a persistent anchor system: when a QR code is first detected, a standalone `GameObject` is created at its world position. All game objects (bottles, shakers, glasses) and floating labels are parented to this anchor rather than to the `ARTrackedImage` directly. This is critical because AR Foundation destroys `ARTrackedImage` GameObjects when tracking is lost, but our anchors persist at their last known good position. Anchors only update their position when `TrackingState == Tracking`, preventing jumps during `Limited` tracking states.

**CocktailManager** (3D Object Factory, 273 lines): Responsible for instantiating and managing all 3D bar objects. When a new order arrives, it receives a `bottle_map` (slot ID to ingredient name mapping) and a drink ID. It maps each ingredient string to a bottle prefab, each drink ID to an empty glass prefab, and places them at the corresponding QR anchor positions. Also handles spawning the shaker at position 2, showing pass/fail result markers, and displaying the final completed drink model.

**SimpleHandSimulator** (Animation Engine, 573 lines): Drives all hand-object interactions. Receives hand position from two sources: (1) the `GreenCircleTracker` for screen-space hand position, and (2) MQTT hall-effect sensor data for slot-level hover detection. Implements grab (reparent object to camera-follow point), pour (tilt object 60 degrees, spawn particle-based liquid stream with ingredient-specific color, 2-second duration), shake (sinusoidal arc motion at 3 Hz, 0.08 m amplitude), and release (return object to its QR anchor). Each animation is a Unity coroutine that invokes a callback on completion to trigger the MQTT ACK.

**GreenCircleTracker** (Hand Detection, 160 lines): Acquires CPU-accessible camera frames from the `ARCameraManager`, downscales to 1/4 resolution, and performs color segmentation to detect the green marker on the bartender's glove. A pixel is classified as green if: green channel > 0.6, green > red, green > blue, and HSV saturation > 0.4. The centroid of all green pixels is computed and shifted by a configurable offset (0.375 screen units at 45 degrees) to approximate the grip center rather than the wrist center. Runs every 2nd frame to conserve CPU.

**GameUIManager** (Screen Manager, 164 lines): Controls visibility of all UI panels: start screen (with mode selection boxes), HUD (round and score), idle info panel (scanning instructions), and game end screen (final score). Provides a clean API: `OnStartScreen()`, `OnIdle()`, `OnNewOrder()`, `OnGameEnd()`.

**RecipeOverlay** (Recipe Display, 140 lines): Dynamically builds a recipe checklist from the ingredient list and shake requirement received in the MQTT order message. Each step is a UI row instantiated from a prefab. As the bartender completes actions, steps are color-coded: green for correct pours, red for wrong pours, green for completed shake and finishing pour.

**TutorialManager** (Guided Tutorial, 256 lines): Orchestrates a 9-step guided tutorial that teaches the bartender the full workflow: hover, grab bottle, pour into shaker, release, grab shaker, shake, pour into glass, release, serve. Only 3 QR codes are tracked during tutorial (qr1, qr2, qr4). Objects are spawned progressively as the user advances through steps.

**SplitScreenManager** (Stereo Rendering, 108 lines): Splits the display into left and right eye viewports for Google Cardboard. Creates a child camera with an IPD offset. Uses a `CommandBuffer` to blit the AR camera background material into the right eye before opaque geometry renders. Enforces camera rects every frame in `LateUpdate()` because AR Foundation resets them.

**GreenDotDebugVisualizer** (Debug Overlay, 220 lines): Renders a screen-space crosshair at the detected hand position, a raw centroid indicator, and a debug HUD showing pixel count and coordinates. Supports stereo-aware rendering.

**UnityMainThreadDispatcher** (Thread Bridge, 51 lines): Singleton that provides a thread-safe queue for marshaling actions from the MQTT background thread to Unity's main thread, where all Unity API calls must occur.

### 2.3 Data Inputs and Communication Protocol

#### MQTT Communication Architecture

```
┌──────────────┐         ┌──────────────────┐         ┌──────────────────────┐
│   GLOVE      │         │     SERVER       │         │     VISUALIZER       │
│   (ESP32)    │         │    (Python)      │         │   (Unity / iOS)      │
│              │         │                  │         │                      │
│ Hall sensors ├──mqtt──►│ "hall" topic     │         │                      │
│ (0-4 byte)   │         │   ↓              │         │                      │
│              │         │ MQTTBridge       │         │                      │
│ Gestures    ├──mqtt──►│ "glove" topic    │         │                      │
│ (0-4 byte)   │         │   ↓              │         │                      │
│              │         │ GameEngine       │         │                      │
│              │         │ (state machine)  │         │                      │
│              │         │   ↓              │         │                      │
│              │         │ Publish JSON  ───┼──mqtt──►│ "game" topic         │
│              │         │                  │         │   ↓                  │
│              │         │                  │         │ MQTTManager          │
│              │         │                  │         │   ↓                  │
│              │         │                  │         │ ProcessMessage()     │
│              │         │                  │         │   ↓                  │
│              │         │                  │         │ State handlers       │
│              │         │                  │         │   ↓                  │
│              │         │                  │         │ Animate              │
│              │         │                  │         │   ↓                  │
│              │         │ Wait for ACK  ◄──┼──mqtt───┤ "anim" topic (0x01) │
│              │         │   ↓              │         │                      │
│              │         │ Continue to      │         │                      │
│              │         │ next state       │         │                      │
└──────────────┘         └──────────────────┘         └──────────────────────┘
```

#### MQTT Topics

| Topic | Direction (relative to visualizer) | Payload | Purpose |
|---|---|---|---|
| `game` | Subscribe | JSON (see schema below) | All game state updates from server |
| `anim` | Publish | Single byte `0x01` | Animation completion acknowledgment |

#### JSON Message Schema

All messages from the server contain at minimum a `state` field. Additional fields are present depending on the state transition:

```json
{
  "state": 0-6,
  "mode": 0|1|2,
  "hall_id": 0-4 | null,
  "picked_up": 0-4,
  "drink": 0-9,
  "pour_target": "shaker" | "serving_glass",
  "pour_result": "correct" | "wrong",
  "round_score": 0|1,
  "round": 1-3,
  "score": 0-3,
  "tutorial_step": 0-7,
  "bottle_map": {"0": "Gin", "1": "Vodka", "3": "Scotch"},
  "recipe": {"ingredients": ["Gin", "Vodka"], "shake": true}
}
```

#### Data Flow Per State

| Server State | Key Data Received | Visualizer Action |
|---|---|---|
| **0 (IDLE)** | `mode`, `round`, `score`, `round_score` | Clear previous round, show idle panel, update HUD, display pass/fail marker |
| **1 (HOVER)** | `drink`, `bottle_map`, `recipe`, `hall_id` | Spawn bottles/shaker/glass at QR positions, show recipe overlay, highlight hovered object |
| **2 (GRAB)** | `picked_up` | Reparent object to camera-follow point, begin hand tracking |
| **3 (POUR)** | `pour_target`, `pour_result` | Tilt held object, spawn liquid particle stream, update recipe step color |
| **4 (SHAKE)** | (none extra) | Play sinusoidal shake animation on held shaker |
| **5 (START_SCREEN)** | (none extra) | Show start screen, clear all game objects, reset state |
| **6 (GAME_END)** | `score`, `round_score` | Show final score screen, display pass/fail for last round |

#### Animation Acknowledgment Protocol

The server and visualizer use a synchronization handshake to ensure animations play fully before the game state advances:

```
Server                              Visualizer
  │                                     │
  │──── state:3 (POUR) ───────────────►│
  │                                     │── Start pour animation
  │         (server blocks,             │   (tilt, particles, 2s)
  │          waiting for ACK)           │
  │                                     │── Animation complete
  │◄─── "anim" topic (0x01) ───────────│
  │                                     │
  │── proceed to next state ──          │
```

This applies to POUR, SHAKE, round-end, game-end, and tutorial-complete transitions.

#### Connection Security

The MQTT connection supports mutual TLS (mTLS):
- **CA Certificate**: `ca.crt` validates the broker's identity
- **Client Certificate**: `unity.pfx` (password: "bartender") authenticates the visualizer to the broker
- **Protocol**: TLS 1.2
- **Fallback**: Plaintext on port 1883 when `useTLS = false`
- **iOS mDNS**: A native Objective-C plugin (`MDNSResolver.mm`) resolves `.local` hostnames on iOS, where Mono's `Dns.GetHostEntry` does not support mDNS

### 2.4 Overlaying Information Over the Camera Feed

AR Foundation provides the camera passthrough as the background layer. The overlay architecture works as follows:

```
┌─────────────────────────────────────────────────────────────┐
│                    RENDERING PIPELINE                        │
│                                                             │
│  Layer 4 (top):  Screen-Space Overlay Canvas                │
│                  ┌─────────────────────────────────────┐    │
│                  │ GameUIManager panels (Start, HUD,   │    │
│                  │   Idle, GameEnd)                     │    │
│                  │ RecipeOverlay (step checklist)       │    │
│                  │ TutorialManager panels               │    │
│                  │ MQTT status text                     │    │
│                  │ GreenDotDebugVisualizer (crosshair)  │    │
│                  └─────────────────────────────────────┘    │
│                                                             │
│  Layer 3:        World-Space Floating Labels                │
│                  ┌─────────────────────────────────────┐    │
│                  │ Slot number labels ("0","1","2"...) │    │
│                  │ Ingredient name labels ("Gin", etc.)│    │
│                  │ BillboardLabel rotates to face cam   │    │
│                  │ sortingOrder = 100 (always on top)   │    │
│                  └─────────────────────────────────────┘    │
│                                                             │
│  Layer 2:        World-Space 3D Objects                     │
│                  ┌─────────────────────────────────────┐    │
│                  │ Bottle prefabs at QR anchors         │    │
│                  │ Shaker prefab at qr2 anchor          │    │
│                  │ Glass prefab at qr4 anchor           │    │
│                  │ Held object (follows camera)         │    │
│                  │ Liquid stream particles               │    │
│                  │ Pass/fail markers                     │    │
│                  └─────────────────────────────────────┘    │
│                                                             │
│  Layer 1 (bg):   AR Camera Background                       │
│                  ┌─────────────────────────────────────┐    │
│                  │ ARCameraBackground component         │    │
│                  │ Real-world camera feed               │    │
│                  │ (blitted via CommandBuffer for        │    │
│                  │  right eye in stereo mode)            │    │
│                  └─────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

**Layer 1 — AR Camera Background**: AR Foundation's `ARCameraBackground` component renders the live camera feed as the background of the scene. In stereo mode, `SplitScreenManager` uses a `CommandBuffer` to blit this material into the right-eye camera before opaque geometry renders, ensuring both eyes see the passthrough feed.

**Layer 2 — World-Space 3D Objects**: All bottles, shakers, glasses, and markers are standard Unity `GameObjects` parented to persistent QR anchor transforms. Because they exist in world space at the anchor's position, they appear to "sit on" the corresponding QR code in the real world. The AR camera's projection matrix (provided by ARKit) ensures correct perspective alignment between the virtual objects and the real-world surface.

**Layer 3 — Floating Labels**: Text labels (slot numbers and ingredient names) are rendered using TextMeshPro with `sortingOrder = 100`, ensuring they render on top of 3D geometry. The `BillboardLabel` component rotates each label in `LateUpdate()` to always face the camera, maintaining readability regardless of viewing angle.

**Layer 4 — Screen-Space UI**: All UI panels (start screen, HUD, recipe overlay, tutorial instructions) use Unity's Canvas system in Screen Space - Overlay mode. This places them on top of everything else, independent of camera position or 3D scene content.

### 2.5 Enabling AR for Various Events

#### QR Code-Based World Anchoring

The foundation of the AR experience is image-based tracking using AR Foundation's `ARTrackedImageManager`. Five QR code reference images are registered in a `XRReferenceImageLibrary` asset. When the phone's camera detects a QR code:

1. **Detection**: `ARTrackedImageManager` fires an `added` event with the detected `ARTrackedImage`, which includes the image's name ("qr0" through "qr4") and its world-space `Transform` (position and rotation).

2. **Anchor Creation**: `QRCodeManager` creates a persistent `GameObject` anchor at this position. Unlike the `ARTrackedImage` itself (which AR Foundation may destroy on tracking loss), the anchor persists indefinitely.

3. **Position Updates**: On subsequent `updated` events, the anchor position is synced to the tracked image — but only when `TrackingState == Tracking`. During `TrackingState.Limited`, the anchor freezes at its last known good position, preventing objects from jumping to incorrect locations.

4. **Object Placement**: When `CocktailManager.SetupFromMQTT()` is called with a bottle map, it calls `QRCodeManager.GetQRTransform("qrN")` to obtain the anchor transform, then parents newly instantiated 3D objects to it. As the anchor moves with tracking updates, all child objects move with it.

```
┌──────────────────────────────────────────────────────────────────────┐
│                    QR ANCHOR LIFECYCLE                                │
│                                                                      │
│  Camera detects QR ──► ARTrackedImage created ──► Anchor created     │
│                                                      │               │
│  Camera still sees QR ──► ARTrackedImage updated ──► Anchor synced   │
│                            (Tracking state)          (moves with QR) │
│                                                                      │
│  Camera loses QR ──► ARTrackedImage state = Limited                  │
│                      Anchor FROZEN at last good position             │
│                      (objects stay in place, no jumping)             │
│                                                                      │
│  Camera re-detects QR ──► ARTrackedImage state = Tracking            │
│                           Anchor resumes syncing                     │
└──────────────────────────────────────────────────────────────────────┘
```

#### AR Events by Game Action

**Bottle Placement (State 1 — New Order)**:
When the server sends a new order with a `bottle_map`, `CocktailManager` maps each ingredient string to a 3D bottle prefab and instantiates it at the corresponding QR anchor with a configurable height offset (0.08 m). A floating `BillboardLabel` with the ingredient name is attached above each bottle. The shaker appears at QR2 (0.06 m height) and the drink-specific serving glass at QR4 (0.03 m height). This creates the illusion of physical bottles sitting on the real bar surface.

**Hand Highlighting (State 1 — Hover)**:
When the glove's hall-effect sensor detects proximity to a bottle position, the server sends the `hall_id`. `SimpleHandSimulator.HighlightSlot()` temporarily changes the material color of the object at that slot to gold (RGB 1, 0.85, 0), providing a visual cue that the object is within reach. The highlight is cleared when the hand moves away.

**Object Grabbing (State 2 — Grab)**:
When the bartender grabs a bottle, the 3D object is reparented from its QR anchor to a camera-relative follow point. The object is held 0.4 m in front of the camera at the screen position reported by the green dot tracker (or screen center if the green dot is lost). Position is smoothly interpolated each frame. This creates the AR illusion of the bartender holding the virtual bottle.

**Pouring (State 3 — Pour)**:
The held object tilts 60 degrees over 0.4 seconds toward the pour target's QR position. A particle system (`LiquidStream` prefab) is spawned at the object's spout position, emitting colored particles matching the ingredient (e.g., clear blue for Gin, deep purple for Purple Liqueur, amber for Bourbon). The particle stream falls toward the target container. After 2 seconds, the object untilts over 0.3 seconds. The pour target container's `PourReceiver` component tracks fill amount and contents.

**Shaking (State 4 — Shake)**:
The shaker moves to the screen center over 0.5 seconds, then oscillates in a sinusoidal arc for 2 seconds (frequency: 3 Hz, amplitude: 0.08 m) with a tilting motion. This creates a convincing shaking animation visible through the AR headset.

**Round End (State 0 — Idle with round > 0)**:
A 3D checkmark or cross marker is spawned above the serving glass (QR4) to indicate pass or fail. On success, the empty glass is replaced with the completed drink's final 3D model (e.g., `Drink_Aviation.prefab`), showing the bartender the finished cocktail in AR.

**Tutorial Mode**:
During the 9-step tutorial, objects are spawned progressively rather than all at once. The bottle appears first, then the shaker when the bottle is grabbed, then the glass when the shaker is grabbed. Each step triggers the corresponding animation automatically (highlight, grab, pour, shake) with on-screen text instructions guiding the bartender. Only 3 of the 5 QR codes are tracked (qr1, qr2, qr4) to reduce visual clutter.

#### Green Dot Hand Tracking

In addition to the hall-effect sensor data from the glove, the visualizer performs vision-based hand tracking using a green marker on the glove:

```
┌────────────────────────────────────────────────────────────────────┐
│                GREEN DOT DETECTION PIPELINE                        │
│                                                                    │
│  AR Camera Frame                                                   │
│       │                                                            │
│       ▼                                                            │
│  Downscale to 1/4 resolution (performance)                        │
│       │                                                            │
│       ▼                                                            │
│  For every 6th pixel:                                              │
│    ┌──────────────────────────────────┐                            │
│    │ Is green channel > 0.6?          │── No ──► Skip              │
│    │ Is green > red AND green > blue? │── No ──► Skip              │
│    │ Is saturation > 0.4?             │── No ──► Skip              │
│    │         ↓ Yes                    │                            │
│    │ Accumulate (x, y) for centroid   │                            │
│    └──────────────────────────────────┘                            │
│       │                                                            │
│       ▼                                                            │
│  Compute centroid of green pixels                                  │
│       │                                                            │
│       ▼                                                            │
│  Apply grip offset (0.375 units at 45 deg)                        │
│  to shift from wrist center to grip center                        │
│       │                                                            │
│       ▼                                                            │
│  Report normalized (x, y) to SimpleHandSimulator                  │
│  Held object follows this position                                 │
└────────────────────────────────────────────────────────────────────┘
```

This dual-input approach (green dot vision + hall-effect MQTT) provides both continuous screen-space hand tracking and discrete slot-level interaction detection, enabling a responsive AR experience where virtual objects follow the bartender's hand movements while the game logic responds to precise bottle interactions.

#### State Machine Diagram

```
                    ┌───────────────┐
                    │  START_SCREEN │ (state 5)
                    │  Mode select  │
                    └───────┬───────┘
                            │ Serve gesture
               ┌────────────┼────────────┐
               │            │            │
          mode=0       mode=1       mode=2
          Normal      Tutorial      Cheat
               │            │            │
               ▼            ▼            ▼
          ┌─────────┐  ┌─────────┐  ┌─────────┐
          │  IDLE   │  │TUTORIAL │  │  IDLE   │
          │(state 0)│  │ 9 steps │  │(state 0)│
          └────┬────┘  └────┬────┘  └────┬────┘
               │            │            │
               │       state 5 from      │
               │       server when       │
               │       complete          │
               │            │            │
               ▼            ▼            ▼
          ┌─────────┐ ┌──────────┐
          │  HOVER  │ │  START   │
          │(state 1)│ │  SCREEN  │
          │ New     │ └──────────┘
          │ order   │
          └────┬────┘
               │ Grab gesture
               ▼
          ┌─────────┐
          │  GRAB   │◄─────────────────────────┐
          │(state 2)│                          │
          └────┬────┘                          │
          ┌────┴────┐                          │
          │         │                          │
     Pour gesture  Shake gesture               │
          │         │                          │
          ▼         ▼                          │
     ┌─────────┐ ┌─────────┐                  │
     │  POUR   │ │  SHAKE  │    Release        │
     │(state 3)│ │(state 4)│────gesture───────►│
     │ Tilt +  │ │ Arc     │                   │
     │ stream  │ │ motion  │                   │
     └────┬────┘ └────┬────┘                   │
          │           │                        │
          └─────┬─────┘                        │
           ACK  │                              │
                ▼                              │
          ┌─────────┐                          │
          │  GRAB   ├─────────────────────────►│
          │(return) │  (continues until serve)
          └────┬────┘
               │ Serve gesture (all steps done)
               ▼
          ┌─────────┐      Round < 3       ┌─────────┐
          │  IDLE   │◄─────────────────────│  IDLE   │
          │(round   │                      │(next    │
          │ result) │                      │ round)  │
          └────┬────┘                      └─────────┘
               │ Round = 3
               ▼
          ┌─────────┐
          │GAME END │ (state 6)
          │Final    │
          │ score   │
          └────┬────┘
               │ Serve gesture
               ▼
          ┌───────────────┐
          │  START_SCREEN │
          └───────────────┘
```
