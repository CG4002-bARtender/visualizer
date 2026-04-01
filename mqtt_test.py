#!/usr/bin/env python3
"""
MQTT test publisher for bARtender game engine.
Stateful interactive simulator — tracks current hover, held bottle, and round state.

Usage:
    python3 mqtt_test.py
    python3 mqtt_test.py --broker 192.168.0.22

Requires: pip install paho-mqtt
"""

import json
import time
import argparse
import paho.mqtt.client as mqtt

BROKER = "localhost"
PORT   = 1883
TOPIC  = "game"

# ── Drink catalogue ───────────────────────────────────────────────────────────
# (drink_int, name, ingredients, shake, default bottle_map)
DRINKS = [
    (0, "Aviation",     ["Gin", "Purple Liqueur"],  True,  {"0": "Gin", "1": "Purple Liqueur", "3": "Scotch"}),
    (1, "Godfather",    ["Scotch", "Bourbon"],       False, {"0": "Scotch", "1": "Bourbon", "3": "Gin"}),
    (2, "Irish Coffee", ["Bourbon", "Dark Rum"],     False, {"0": "Bourbon", "1": "Dark Rum", "3": "Gin"}),
    (3, "Martini",      ["Gin", "Vodka"],            False, {"0": "Gin", "1": "Vodka", "3": "Scotch"}),
    (4, "Midori Sour",  ["Midori", "Vodka"],         False, {"0": "Midori", "1": "Vodka", "3": "Scotch"}),
    (5, "Old Fashioned",["Bourbon", "Rye Whiskey"],  False, {"0": "Bourbon", "1": "Rye Whiskey", "3": "Gin"}),
    (6, "Scotch Neat",  ["Scotch"],                  False, {"0": "Scotch", "1": "Gin", "3": "Vodka"}),
    (7, "Tuxedo",       ["Gin", "Scotch"],            False, {"0": "Gin", "1": "Scotch", "3": "Vodka"}),
    (8, "Vodka Neat",   ["Vodka"],                   False, {"0": "Vodka", "1": "Gin", "3": "Scotch"}),
    (9, "Whiskey Neat", ["Rye Whiskey"],             False, {"0": "Rye Whiskey", "1": "Gin", "3": "Scotch"}),
]

# ── Session state ─────────────────────────────────────────────────────────────
state = {
    "hall_id":    0,      # current hover position
    "picked_up":  None,   # slot currently held (None = empty hand)
    "drink":      None,   # current drink tuple
    "round":      0,
    "score":      0,
    "pour_step":  0,      # which ingredient we're on
}

client = mqtt.Client()


def pub(payload: dict):
    msg = json.dumps(payload)
    client.publish(TOPIC, msg)
    print(f"  → {msg}")


def status():
    d = state["drink"]
    drink_name = d[1] if d else "none"
    held = f"slot {state['picked_up']}" if state["picked_up"] is not None else "nothing"
    bmap = d[4] if d else {}
    slot_info = "  ".join(f"[{k}={v}]" for k, v in sorted(bmap.items())) if bmap else ""
    print(f"\n  hover={state['hall_id']}  held={held}  drink={drink_name}  {slot_info}")


def bottle_at(slot: int) -> str:
    """Return ingredient name at a slot, or label for fixed slots."""
    if slot == 2: return "shaker"
    if slot == 4: return "serving cup"
    bmap = state["drink"][4] if state["drink"] else {}
    return bmap.get(str(slot), f"slot {slot}")


# ── Commands ──────────────────────────────────────────────────────────────────

def cmd_start():
    """Dismiss start screen → idle/ready state."""
    pub({"state": 0, "round": 0, "score": 0})
    print("  Start screen dismissed — ready for orders.")


def cmd_new_order():
    print("\nDrinks:")
    for d in DRINKS:
        shake_tag = " [shake]" if d[3] else ""
        print(f"  {d[0]}  {d[1]}{shake_tag}  —  {', '.join(d[2])}")
    try:
        choice = int(input("  Choose drink (0-9): ").strip())
    except ValueError:
        print("  Invalid."); return
    if not (0 <= choice <= 9):
        print("  Out of range."); return

    drink = DRINKS[choice]
    state["drink"]     = drink
    state["hall_id"]   = 0
    state["picked_up"] = None
    state["pour_step"] = 0

    pub({
        "state": 1,
        "hall_id": state["hall_id"],
        "drink": drink[0],
        "recipe": {"ingredients": drink[2], "shake": drink[3]},
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

    is_shaker = (held == 2)

    if is_shaker:
        # Finishing pour: shaker → serving glass
        pub({"state": 3, "hall_id": held, "picked_up": held, "pour_target": "serving_glass"})
        print("  [hold 4] Finishing pour — press Enter to complete...")
        input()
        pub({"state": 2, "hall_id": held, "picked_up": held})
        print("  Pour animation complete → back to GRAB")
    else:
        # Ingredient pour
        shake = d[3]
        target = "shaker" if shake else "serving_glass"
        step   = state["pour_step"]
        ingredients = d[2]
        ingredient  = ingredients[step] if step < len(ingredients) else "?"
        result = "correct"  # always correct in test mode
        pub({"state": 3, "hall_id": held, "picked_up": held,
             "pour_target": target, "pour_result": result})
        print(f"  [hold 4] Pouring {ingredient} → {target} — press Enter to complete...")
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
    print("  [hold 5] Shaking — press Enter to release...")
    input()
    pub({"state": 2, "hall_id": held, "picked_up": held})
    print("  Shake released → back to GRAB")


def cmd_serve():
    state["round"] += 1
    state["score"] += 1
    state["picked_up"] = None
    state["drink"]     = None
    state["pour_step"] = 0
    pub({"state": 0, "hall_id": 4,
         "round_score": 1, "round": state["round"], "score": state["score"]})


# ── Menu ──────────────────────────────────────────────────────────────────────

MENU = """
── bARtender MQTT Test Publisher ──────────────────────
  0   Start (dismiss start screen)
  1   New order — choose drink
  2   Hover move
  3   Grab (current hover target)
  4   Pour  [hold: ingredient→shaker or shaker→glass]
  5   Shake [hold: shake, Enter to release]
  6   Release bottle
  7   Serve (pass)
  ?   Show this menu
  q   Quit
"""

def interactive():
    print(MENU)
    print("  Send '0' first to dismiss the start screen before starting an order.\n")
    while True:
        status()
        cmd = input("cmd> ").strip().lower()
        if   cmd == "q": break
        elif cmd == "0": cmd_start()
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
