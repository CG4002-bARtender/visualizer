#!/usr/bin/env python3
"""
Simplified bARtender MQTT sender — type a number, send a command.
Usage: python3 mqtt_menu.py [--broker 172.20.10.13]
"""

import json
import time
import argparse
import paho.mqtt.client as mqtt

BROKER     = "localhost"
PORT       = 1883
TOPIC      = "game"
MAX_ROUNDS = 3

DRINKS = [
    (0, "Aviation",      ["Gin", "Purple Liqueur"],  True,  {"0": "Gin", "1": "Purple Liqueur", "3": "Scotch"}),
    (1, "Godfather",     ["Scotch", "Bourbon"],       True,  {"0": "Scotch", "1": "Bourbon",     "3": "Gin"}),
    (2, "Irish Coffee",  ["Bourbon", "Dark Rum"],     True,  {"0": "Bourbon", "1": "Dark Rum",    "3": "Gin"}),
    (3, "Martini",       ["Gin", "Vodka"],            True,  {"0": "Gin",    "1": "Vodka",        "3": "Scotch"}),
    (4, "Midori Sour",   ["Midori", "Vodka"],         True,  {"0": "Midori", "1": "Vodka",        "3": "Scotch"}),
    (5, "Old Fashioned", ["Bourbon", "Rye Whiskey"],  True,  {"0": "Bourbon", "1": "Rye Whiskey", "3": "Gin"}),
    (6, "Scotch Neat",   ["Scotch"],                  False, {"0": "Scotch", "1": "Gin",          "3": "Vodka"}),
    (7, "Tuxedo",        ["Gin", "Scotch"],            True,  {"0": "Gin",    "1": "Scotch",       "3": "Vodka"}),
    (8, "Vodka Neat",    ["Vodka"],                   False, {"0": "Vodka",  "1": "Gin",          "3": "Scotch"}),
    (9, "Whiskey Neat",  ["Rye Whiskey"],             False, {"0": "Rye Whiskey", "1": "Gin",     "3": "Scotch"}),
]

state = {
    "screen":      "start",
    "hall_id":     None,
    "picked_up":   None,
    "drink":       None,
    "round":       0,
    "score":       0,
    "pour_step":   0,
    "round_score": 1,
}

client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)


def pub(payload: dict):
    msg = json.dumps(payload)
    client.publish(TOPIC, msg)
    print(f"  → {msg}")


def status():
    d     = state["drink"]
    name  = d[1] if d else "none"
    held  = f"slot {state['picked_up']}" if state["picked_up"] is not None else "nothing"
    bmap  = d[4] if d else {}
    slots = "  ".join(f"[{k}={v}]" for k, v in sorted(bmap.items())) if bmap else ""
    step  = f"  pour_step={state['pour_step']}/{len(d[2])}" if d else ""
    print(
        f"\n  [{state['screen']}]  hover={state['hall_id']}  held={held}"
        f"  drink={name}  round={state['round']}/{MAX_ROUNDS}"
        f"  score={state['score']}  {'PASS' if state['round_score'] else 'FAIL'}"
        f"{step}  {slots}"
    )


MENU = """
── bARtender MQTT Menu ────────────────────────────────
  1   START_SCREEN
  2   Dismiss / restart → IDLE
  3   New order  (pick drink by number)
  4   Grab current hover slot
  5   Pour next ingredient  (always correct)
  6   Shake  (auto-completes)
  7   Release bottle → HOVER
  8   Serve → IDLE or END_SCREEN
  9   Move hover to next slot
  0   Quit

  Shake flow:  1→2→3→4→5→5→4(shaker)→5→6→5(finish)→7→8
  Neat flow:   1→2→3→4→5→8
──────────────────────────────────────────────────────
"""


def bottle_at(slot):
    if slot == 2: return "shaker"
    if slot == 4: return "serving cup"
    bmap = state["drink"][4] if state["drink"] else {}
    return bmap.get(str(slot), f"slot {slot}")


def cmd_start_screen():
    state.update(screen="start", hall_id=None, picked_up=None, drink=None,
                 round=0, score=0, pour_step=0, round_score=1)
    pub({"state": 5, "hall_id": None})
    print("  START_SCREEN shown — send 2 to dismiss.")


def cmd_dismiss():
    if state["screen"] == "start":
        pub({"state": 0, "hall_id": None, "round_score": 0, "round": 0, "score": 0})
    elif state["screen"] == "end":
        pub({"state": 0, "hall_id": None})
    else:
        print("  Already in game."); return
    state.update(screen="game", hall_id=None, picked_up=None, drink=None,
                 pour_step=0, round_score=1)
    print("  IDLE — send 3 for a new order.")


def cmd_new_order():
    if state["screen"] != "game":
        print("  Not in game. Send 1 then 2 first."); return
    print("\n  Drinks:")
    for d in DRINKS:
        tag = "[shake]" if d[3] else "       "
        print(f"    {d[0]}  {tag}  {d[1]:<16}  {', '.join(d[2])}")
    try:
        choice = int(input("  Pick drink (0-9): ").strip())
    except (ValueError, EOFError):
        print("  Invalid."); return
    if not (0 <= choice <= 9):
        print("  Out of range."); return

    drink = DRINKS[choice]
    state.update(drink=drink, hall_id=0, picked_up=None, pour_step=0, round_score=1)
    pub({"state": 1, "hall_id": 0, "drink": drink[0],
         "recipe": {"ingredients": drink[2], "shake": drink[3]},
         "bottle_map": drink[4]})
    print(f"  New order: {drink[1]} — send 4 to grab slot 0.")


def cmd_hover_next():
    d = state["drink"]
    if not d:
        print("  No active order."); return
    slots = sorted([int(k) for k in d[4].keys()] + [2, 4])
    cur   = state["hall_id"] if state["hall_id"] is not None else -1
    nxt   = next((s for s in slots if s > cur), slots[0])
    state["hall_id"] = nxt
    pub({"state": 1, "hall_id": nxt})
    print(f"  Hovering slot {nxt} ({bottle_at(nxt)})")


def cmd_grab():
    slot = state["hall_id"]
    if slot is None:
        print("  No hover target."); return
    if state["picked_up"] is not None:
        print(f"  Already holding slot {state['picked_up']}."); return
    state["picked_up"] = slot
    pub({"state": 2, "hall_id": slot, "picked_up": slot})
    print(f"  Grabbed {bottle_at(slot)} — send 5 to pour.")


def cmd_pour():
    held = state["picked_up"]
    if held is None:
        print("  Not holding anything."); return
    d = state["drink"]
    if not d:
        print("  No active order."); return

    if held == 2:
        pub({"state": 3, "hall_id": held, "picked_up": held, "pour_target": "serving_glass"})
        time.sleep(1.5)
        pub({"state": 2, "hall_id": held, "picked_up": held})
        print("  Finishing pour complete. Send 7 to release, then 8 to serve.")
    else:
        shake      = d[3]
        target     = "shaker" if shake else "serving_glass"
        step       = state["pour_step"]
        ingredient = d[2][step] if step < len(d[2]) else "?"
        pub({"state": 3, "hall_id": held, "picked_up": held,
             "pour_target": target, "pour_result": "correct"})
        time.sleep(1.5)
        pub({"state": 2, "hall_id": held, "picked_up": held})
        state["pour_step"] += 1
        remaining = len(d[2]) - state["pour_step"]
        if remaining > 0:
            print(f"  Poured {ingredient} → {target}. {remaining} left — send 5 to pour next.")
        elif shake:
            print(f"  Poured {ingredient} → {target}. Send 6 to shake.")
        else:
            print(f"  Poured {ingredient} → {target}. Send 8 to serve.")


def cmd_shake():
    held = state["picked_up"]
    if held is None:
        print("  Not holding anything."); return
    pub({"state": 4, "hall_id": held, "picked_up": held})
    time.sleep(2.0)
    pub({"state": 2, "hall_id": held, "picked_up": held})
    print("  Shake done. Send 7 to release, hover to shaker (slot 2) with 9, grab with 4, then 5 to finish pour.")


def cmd_release():
    held = state["picked_up"]
    if held is None:
        print("  Not holding anything."); return
    state["picked_up"] = None
    state["hall_id"]   = held
    pub({"state": 1, "hall_id": held})
    print(f"  Released slot {held}. Send 9 to move hover or 8 to serve.")


def cmd_serve():
    if state["screen"] != "game":
        print("  Not in game."); return
    state["round"]  += 1
    state["score"]  += state["round_score"]
    round_score      = state["round_score"]
    state.update(picked_up=None, drink=None, pour_step=0, round_score=1)

    if state["round"] >= MAX_ROUNDS:
        state["screen"] = "end"
        pub({"state": 6, "hall_id": 4, "round_score": round_score,
             "round": state["round"], "score": state["score"]})
        print(f"  GAME OVER — score: {state['score']}/{MAX_ROUNDS}. Send 2 to restart.")
    else:
        pub({"state": 0, "hall_id": 4, "round_score": round_score,
             "round": state["round"], "score": state["score"]})
        print(f"  Round {state['round']} done — score: {state['score']}. Send 3 for next order.")


def interactive():
    print(MENU)
    COMMANDS = {
        "1": cmd_start_screen,
        "2": cmd_dismiss,
        "3": cmd_new_order,
        "4": cmd_grab,
        "5": cmd_pour,
        "6": cmd_shake,
        "7": cmd_release,
        "8": cmd_serve,
        "9": cmd_hover_next,
    }
    while True:
        status()
        try:
            cmd = input("cmd> ").strip()
        except (EOFError, KeyboardInterrupt):
            break
        if cmd == "0":
            break
        elif cmd in COMMANDS:
            COMMANDS[cmd]()
        else:
            print(MENU)


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
