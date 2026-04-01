# bARtender Visualizer

Unity AR app for iOS that renders a virtual cocktail-making experience on a physical bar surface tracked via QR codes. The app is a **pure visual client** — all game logic lives on a separate server that communicates over MQTT.

## Architecture

```
Server (game logic, gesture recognition)
  │
  │  MQTT (TLS or plain)
  │  topic: "game" (server → visualizer)
  │  topic: "animation" (visualizer → server, 1-byte completion signal)
  │
  ▼
Unity Visualizer (this repo)
  ├── AR camera + QR tracking (ARFoundation/ARKit)
  ├── Red dot hand tracking (camera image processing)
  ├── Bottle/glass spawning on QR anchors
  ├── Animations (pour, shake) — fixed duration, self-terminating
  └── UI overlays (recipe, start screen, game end)
```

## MQTT Protocol

The visualizer subscribes to `game` and receives JSON messages with a `state` field that drives all behavior:

| State | Name | What it does |
|-------|------|-------------|
| 0 | Idle | Dismiss start/end screens, release bottles, end of round scores |
| 1 | Hover | New order (with `bottle_map`, `drink`, `recipe`) OR hand position update (`hall_id`) OR release from grab |
| 2 | Grab | Pick up bottle at `picked_up` slot |
| 3 | Pour | Fixed-duration pour animation → publishes `0x01` to `animation` topic when done |
| 4 | Shake | Fixed-duration shake animation → publishes `0x01` to `animation` topic when done |
| 5 | (Start) | Internal only — initial state showing start screen |
| 6 | Game End | Show final score screen |

**Animation completion flow:** Pour and shake are self-terminating. The server sends state 3 or 4 once to trigger, then waits. When the animation finishes, the visualizer publishes a single byte `0x01` to the `animation` MQTT topic. The server must not send new states until it receives this signal.

### Message shape (`MQTTMessage` in MQTTManager.cs)
```json
{
  "state": 3,
  "hall_id": 0,
  "picked_up": 0,
  "drink": 0,
  "pour_target": "shaker",
  "pour_result": "correct",
  "round_score": 1,
  "round": 1,
  "score": 3
}
```

State 1 with a new order also includes `bottle_map` (slot→ingredient) and `recipe` (ingredients array + shake bool), parsed manually from raw JSON since they're nested.

## QR Slot Layout

Five physical QR codes on the bar surface:

| QR | Slot | Default contents |
|----|------|-----------------|
| qr0 | 0 | Bottle (ingredient varies per drink) |
| qr1 | 1 | Bottle (ingredient varies per drink) |
| qr2 | 2 | Shaker (always) |
| qr3 | 3 | Bottle (ingredient varies per drink) |
| qr4 | 4 | Serving glass (type varies per drink) |

Bottles at slots 0, 1, 3 are swapped per order via `CocktailManager.SetupFromMQTT()`. Shaker (qr2) and glass (qr4) are always present.

## Scripts Overview

All scripts live in `Assets/Scripts/`.

### MQTTManager.cs — Network layer
- Connects to MQTT broker (TLS with mTLS certs from StreamingAssets, or plain)
- Subscribes to `game` topic, dispatches to handlers by state
- `PublishAnimationComplete()` sends `0x01` on `animation` topic after pour/shake
- Auto-reconnects every 5s on disconnect
- Uses `UnityMainThreadDispatcher` to marshal callbacks to main thread

### SimpleHandSimulator.cs — Core interaction controller
- **Hand tracking:** Follows red dot screen position (or screen center fallback) via `UpdateHeldObjectPosition()`
- **Grab/Release:** `GrabObjectAtSlot(int)` detaches bottle from QR parent and holds in front of camera; `OnReleaseCupButton()` returns it
- **Highlight:** Gold tint on hovered slot object
- **Pour sequence** (`PourSequence` coroutine): entry tilt (0.4s) → active pour (`pourActiveDuration`, default 2s) → stop particles + untilt (0.3s) → callback
  - Tilts around `arCamera.right` axis by `pourAngle` (60 deg)
  - Spawns `LiquidStream.prefab` particle system at bottle's `SpoutPosition` child transform
  - Fills target `PourReceiver` at `0.5 * deltaTime` per frame
- **Shake sequence** (`ShakeSequence` coroutine): move to center (0.5s) → arc shake (`shakeActiveDuration`, default 2s) → return to hand (0.4s) → callback
  - Sin-wave vertical oscillation with forward/back arc and tilt
- Hand tracking is suppressed during pour (`pourSequenceCoroutine != null`) and shake (`isInShakeState`)

### QRCodeManager.cs — AR anchor management
- Uses `ARTrackedImageManager` to detect QR codes and spawn bottle prefabs as children of tracked image transforms
- Maintains `spawnedBottles` dictionary (qr name → GameObject)
- `GetBottleAtQR(string)` used by SimpleHandSimulator for slot-based grab
- Height offsets per slot are Inspector-tunable

### CocktailManager.cs — Drink setup
- `SetupFromMQTT(int drinkInt, Dictionary<int, string> bottleMap)` — destroys previous items, spawns correct bottle prefabs at slots 0/1/3, shaker at slot 2, drink-specific glass at slot 4
- Maps ingredient strings to prefabs, drink ints to glass prefabs
- All prefabs in `Assets/MyPrefabs/Final/`

### RedCircleTracker.cs — Hand position tracking
- Processes AR camera CPU image every 2 frames at 1/4 resolution
- Finds centroid of red pixels (HSV thresholding) and reports normalized screen coords to `SimpleHandSimulator.OnHandPositionReceived()`
- Physical setup: user wears a red dot/sticker on their hand

### PourReceiver.cs — Container fill state
- Attached to shaker and glass objects
- `ContainerType` enum: Shaker (capacity 2.0) or ServingGlass (capacity 1.0)
- `AddLiquid(string, float)` increments `fillAmount`, tracks liquid types in `contents` list
- `CanReceiveLiquid()` checks if not full

### RecipeOverlay.cs — Recipe step UI
- Shows drink name + ordered steps ("Pour X into shaker", "Shake", "Pour shaker into glass")
- `MarkIngredientStep(string pourResult)` colors steps green/red
- `MarkFinishingPour()` marks shake + final pour steps

### GameUIManager.cs — Screen management
- Toggles start screen and game end screen
- `OnIdle()` hides both, `OnGameEnd(int)` shows score

### UnityMainThreadDispatcher.cs — Threading utility
- Singleton; queues `Action`s from MQTT background thread for main-thread execution

## Prefabs

- `Assets/Prefabs/LiquidStream.prefab` — Particle system for pour stream (looping, play-on-awake disabled)
- `Assets/MyPrefabs/Final/` — All bottle prefabs (Gin, Vodka, Bourbon, etc.), empty glass prefabs per drink, Shaker
- `Assets/StepRowPrefab.prefab` — Recipe overlay row (TextMeshPro)
- Bottle prefabs must have a `SpoutPosition` child transform for pour stream placement

## Key Inspector-Tunable Parameters

On `SimpleHandSimulator`:
- `holdDistance` (0.4) — how far in front of camera to hold objects
- `pourAngle` (60), `pourActiveDuration` (2s) — pour tilt and active time
- `shakeMoveTime` (0.5s), `shakeActiveDuration` (2s), `shakeReturnTime` (0.4s) — shake phase durations
- `shakeFrequency` (3), `shakeAmplitude` (0.08), `shakeTiltAngle` (25) — shake motion feel

On `MQTTManager`:
- `brokerAddress`, `securePort`/`insecurePort`, `useTLS` — broker connection
- mTLS certs loaded from `Assets/StreamingAssets/` (ca.crt, unity.pfx)

On `RedCircleTracker`:
- `redThreshold` (0.6), `saturationThreshold` (0.4), `sampleStep` (6) — detection sensitivity

## Drinks Catalogue

| Int | Drink | Ingredients | Shake? |
|-----|-------|-------------|--------|
| 0 | Aviation | Gin, Purple Liqueur | Yes |
| 1 | Godfather | Scotch, Bourbon | No |
| 2 | Irish Coffee | Bourbon, Dark Rum | No |
| 3 | Martini | Gin, Vodka | No |
| 4 | Midori Sour | Midori, Vodka | No |
| 5 | Old Fashioned | Bourbon, Rye Whiskey | No |
| 6 | Scotch Neat | Scotch | No |
| 7 | Tuxedo | Gin, Scotch | No |
| 8 | Vodka Neat | Vodka | No |
| 9 | Whiskey Neat | Rye Whiskey | No |

## Testing

### mqtt_test.py — Interactive MQTT simulator
```
pip install paho-mqtt
python3 mqtt_test.py --broker localhost --port 1883
```
Menu-driven. Correct flow: `0` (dismiss start screen) → `1` (new order) → `2` (hover) → `3` (grab) → `4` (pour) → ... → `7` (serve). Sends realistic JSON to the `game` topic.

### mqtt_send.py — Raw byte sender
```
uv run mqtt_send.py animation 1
```
Useful for manually sending the animation-complete signal.

## Build & Deploy

Target: iOS (iPhone with ARKit). Unity builds to `Build/` as an Xcode project, then deploy to device via Xcode.

The scene is `Assets/Scenes/bARtender.unity`. There is one scene only.

## Common Pitfalls

- **Hand tracking fights animations:** `UpdateHeldObjectPosition()` runs every frame and will override animation transforms if not suppressed. Any new animation coroutine must ensure the Update guard (`pourSequenceCoroutine == null && !isInShakeState`) blocks hand tracking for its duration.
- **MQTT threading:** All MQTT callbacks arrive on a background thread. Always dispatch through `UnityMainThreadDispatcher.Instance().Enqueue()`.
- **QR parent transforms:** Bottles are parented to AR tracked image transforms. When picking up, the bottle is un-parented (`transform.parent = null`). On release, it's re-parented. If you skip this, the bottle will rubber-band to the QR position.
- **Nested JSON fields:** `JsonUtility` can't handle `bottle_map` (dict) or `recipe` (nested object), so these are parsed with regex from raw JSON in MQTTManager.
- **SpoutPosition child:** Pour stream placement requires a child transform named `SpoutPosition` on bottle prefabs. Missing it logs a warning and falls back to `bottle.transform.position + up * 0.15`.
