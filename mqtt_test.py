#!/usr/bin/env python3
"""
MQTT test publisher for bARtender game engine.
Stateful interactive simulator — tracks screen, hover, held bottle, and round state.

Usage:
    python3 mqtt_test.py
    python3 mqtt_test.py --broker 192.168.0.22

Requires: pip install paho-mqtt
"""

import json
import argparse
import paho.mqtt.client as mqtt

BROKER     = "localhost"
PORT       = 1883
TOPIC      = "game"
MAX_ROUNDS = 3

# ── Drink catalogue ───────────────────────────────────────────────────────────
# (drink_int, name, ingredients, shake, bottle_map)
DRINKS = [
    (0, "Aviation",      ["Gin", "Purple Liqueur"],  True,  {"0": "Gin", "1": "Purple Liqueur", "3": "Scotch"}),
    (1, "Godfather",     ["Scotch", "Bourbon"],       True,  {"0": "Scotch", "1": "Bourbon",      "3": "Gin"}),
    (2, "Irish Coffee",  ["Bourbon", "Dark Rum"],     True,  {"0": "Bourbon", "1": "Dark Rum",     "3": "Gin"}),
    (3, "Martini",       ["Gin", "Vodka"],            True,  {"0": "Gin",    "1": "Vodka",         "3": "Scotch"}),
    (4, "Midori Sour",   ["Midori", "Vodka"],         True,  {"0": "Midori", "1": "Vodka",         "3": "Scotch"}),
    (5, "Old Fashioned", ["Bourbon", "Rye Whiskey"],  True,  {"0": "Bourbon", "1": "Rye Whiskey",  "3": "Gin"}),
    (6, "Scotch Neat",   ["Scotch"],                  False, {"0": "Scotch", "1": "Gin",           "3": "Vodka"}),
    (7, "Tuxedo",        ["Gin", "Scotch"],            True,  {"0": "Gin",    "1": "Scotch",        "3": "Vodka"}),
    (8, "Vodka Neat",    ["Vodka"],                   False, {"0": "Vodka",  "1": "Gin",           "3": "Scotch"}),
    (9, "Whiskey Neat",  ["Rye Whiskey"],             False, {"0": "Rye Whiskey", "1": "Gin",      "3": "Scotch"}),
]

# ── Session state ─────────────────────────────────────────────────────────────
state = {
    "screen":      "start",  # start | game | end
    "hall_id":     None,
    "picked_up":   None,
    "drink":       None,
    "round":       0,
    "score":       0,
    "pour_step":   0,
    "round_score": 1,        # 1 = pass, 0 = fail; reset each round
}

client = mqtt.Client()


def pub(payload: dict):
    msg = json.dumps(payload)
    client.publish(TOPIC, msg)
    print(f"  → {msg}")


def status():
    d         = state["drink"]
    name      = d[1] if d else "none"
    held      = f"slot {state['picked_up']}" if state["picked_up"] is not None else "nothing"
    screen    = state["screen"]
    bmap      = d[4] if d else {}
    slot_info = "  ".join(f"[{k}={v}]" for k, v in sorted(bmap.items())) if bmap else ""
    pour_info = f"  pour_step={state['pour_step']}/{len(d[2]) if d else 0}" if d else ""
    print(
        f"\n  [{screen}]  hover={state['hall_id']}  held={held}"
        f"  drink={name}  round={state['round']}/{MAX_ROUNDS}"
        f"  score={state['score']}  round_score={'PASS' if state['round_score'] else 'FAIL'}"
        f"{pour_info}  {slot_info}"
    )


def bottle_at(slot: int) -> str:
    if slot == 2: return "shaker"
    if slot == 4: return "serving cup"
    bmap = state["drink"][4] if state["drink"] else {}
    return bmap.get(str(slot), f"slot {slot}")


# ── Commands ──────────────────────────────────────────────────────────────────

def cmd_start_screen():
    """Publish START_SCREEN (state 5) — the initial screen before game begins."""
    state["screen"]    = "start"
    state["hall_id"]   = None
    state["picked_up"] = None
    state["drink"]     = None
    state["round"]     = 0
    state["score"]     = 0
    state["pour_step"] = 0
    state["round_score"] = 1
    pub({"state": 5, "hall_id": None})
    print("  START_SCREEN shown — send '0' when player serves on start screen.")


def cmd_dismiss():
    """Dismiss start screen or restart after end screen → IDLE."""
    if state["screen"] == "start":
        # First game start — includes full scoring fields zeroed
        state["round"] = 0
        state["score"] = 0
        pub({"state": 0, "hall_id": None, "round_score": 0, "round": 0, "score": 0})
        print("  Game started — send '1' for a new order.")
    elif state["screen"] == "end":
        # Restart after end screen — minimal payload per spec
        state["round"] = 0
        state["score"] = 0
        pub({"state": 0, "hall_id": None})
        print("  Game restarted — send '1' for a new order.")
    else:
        print("  Already in game.")

    state["screen"]      = "game"
    state["hall_id"]     = None
    state["picked_up"]   = None
    state["drink"]       = None
    state["pour_step"]   = 0
    state["round_score"] = 1


def cmd_new_order():
    if state["screen"] != "game":
        print("  Not in game. Send 's' then '0' first."); return

    print("\nDrinks:")
    for d in DRINKS:
        tag = " [shake]" if d[3] else "        "
        print(f"  {d[0]}  {d[1]:<16}{tag}  —  {', '.join(d[2])}")
    try:
        choice = int(input("  Choose drink (0-9): ").strip())
    except ValueError:
        print("  Invalid."); return
    if not (0 <= choice <= 9):
        print("  Out of range."); return

    drink = DRINKS[choice]
    state["drink"]       = drink
    state["hall_id"]     = 0
    state["picked_up"]   = None
    state["pour_step"]   = 0
    state["round_score"] = 1

    pub({
        "state":      1,
        "hall_id":    state["hall_id"],
        "drink":      drink[0],
        "recipe":     {"ingredients": drink[2], "shake": drink[3]},
        "bottle_map": drink[4],
    })


def cmd_hover():
    d = state["drink"]
    if not d:
        print("  No active order."); return

    slots = sorted([int(k) for k in d[4].keys()]) + [2, 4]
    print("  Slots: " + "  ".join(f"[{s}={bottle_at(s)}]" for s in sorted(slots)))
    try:
        slot = int(input("  Move to slot: ").strip())
    except ValueError:
        print("  Invalid."); return

    state["hall_id"] = slot
    pub({"state": 1, "hall_id": slot})


def cmd_grab():
    if state["picked_up"] is not None:
        print(f"  Already holding slot {state['picked_up']}. Release first."); return
    slot = state["hall_id"]
    state["picked_up"] = slot
    pub({"state": 2, "hall_id": slot, "picked_up": slot})


def cmd_pour():
    held = state["picked_up"]
    if held is None:
        print("  Not holding anything."); return
    d = state["drink"]
    if not d:
        print("  No active order."); return

    if held == 2:
        # Finishing pour: shaker → serving glass (no pour_result)
        pub({"state": 3, "hall_id": held, "picked_up": held, "pour_target": "serving_glass"})
        print("  [hold] Finishing pour — press Enter to complete animation...")
        input()
        pub({"state": 2, "hall_id": held, "picked_up": held})
        print("  Pour animation complete → back to GRAB")
    else:
        shake      = d[3]
        target     = "shaker" if shake else "serving_glass"
        step       = state["pour_step"]
        ingredient = d[2][step] if step < len(d[2]) else "?"

        raw = input(f"  Pour result for {ingredient} — [c]orrect / [w]rong (default c): ").strip().lower()
        result = "wrong" if raw == "w" else "correct"
        if result == "wrong":
            state["round_score"] = 0

        pub({"state": 3, "hall_id": held, "picked_up": held,
             "pour_target": target, "pour_result": result})
        print(f"  [hold] Pouring {ingredient} → {target} ({result}) — press Enter to complete animation...")
        input()
        pub({"state": 2, "hall_id": held, "picked_up": held})
        print("  Pour animation complete → back to GRAB")
        state["pour_step"] += 1


def cmd_release():
    held = state["picked_up"]
    if held is None:
        print("  Not holding anything."); return
    state["picked_up"] = None
    pub({"state": 1, "hall_id": held})


def cmd_shake():
    held = state["picked_up"]
    if held is None:
        print("  Not holding anything."); return
    pub({"state": 4, "hall_id": held, "picked_up": held})
    print("  [hold] Shaking — press Enter to release...")
    input()
    pub({"state": 2, "hall_id": held, "picked_up": held})
    print("  Shake released → back to GRAB")


def cmd_serve():
    if state["screen"] != "game":
        print("  Not in game."); return

    state["round"] += 1
    state["score"] += state["round_score"]
    round_score     = state["round_score"]
    state["picked_up"] = None
    state["drink"]     = None
    state["pour_step"] = 0
    state["round_score"] = 1  # reset for next round

    if state["round"] >= MAX_ROUNDS:
        # Final round → END_SCREEN (state 6)
        state["screen"] = "end"
        pub({"state": 6, "hall_id": 4,
             "round_score": round_score,
             "round": state["round"],
             "score": state["score"]})
        print(f"  Round {state['round']} done — GAME OVER. Final score: {state['score']}/{MAX_ROUNDS}")
        print("  Send '0' to restart (END_SCREEN → IDLE).")
    else:
        # Mid-game → IDLE (state 0)
        pub({"state": 0, "hall_id": 4,
             "round_score": round_score,
             "round": state["round"],
             "score": state["score"]})
        print(f"  Round {state['round']} done — score: {state['score']}. Send '1' for next order.")


# ── Menu ──────────────────────────────────────────────────────────────────────

MENU = """
── bARtender MQTT Test Publisher ──────────────────────
  s   START_SCREEN (state 5)
  0   Dismiss start / restart after end → IDLE (state 0)
  1   New order → HOVER (state 1 with drink/recipe/bottle_map)
  2   Hover move (state 1, hall_id only)
  3   Grab current slot → GRAB (state 2)
  4   Pour  [c=correct / w=wrong, Enter to complete] → POUR→GRAB (state 3→2)
  5   Shake [Enter to release] → SHAKE→GRAB (state 4→2)
  6   Release bottle → HOVER (state 1)
  7   Serve → IDLE or END_SCREEN (state 0 or 6)
  ?   Show this menu
  q   Quit

  Shake drink flow:  s → 0 → 1 → 2(slot) → 3 → 4 → 4 → 2(shaker) → 3 → 5 → 4(finish) → 6 → 7
  Neat drink flow:   s → 0 → 1 → 2(slot) → 3 → 4 → 7
"""


def interactive():
    print(MENU)
    while True:
        status()
        cmd = input("cmd> ").strip().lower()
        if   cmd == "q": break
        elif cmd == "s": cmd_start_screen()
        elif cmd == "0": cmd_dismiss()
        elif cmd == "1": cmd_new_order()
        elif cmd == "2": cmd_hover()
        elif cmd == "3": cmd_grab()
        elif cmd == "4": cmd_pour()
        elif cmd == "5": cmd_shake()
        elif cmd == "6": cmd_release()
        elif cmd == "7": cmd_serve()
        else:            print(MENU)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--broker", default=BROKER)
    parser.add_argument("--port",   type=int, default=PORT)
    args = parser.parse_args()

    client.connect(args.broker, args.port)
    client.loop_start()
    print(f"Connected to {args.broker}:{args.port}, publishing to '{TOPIC}'")

    interactive()

    client.loop_stop()
    client.disconnect()
