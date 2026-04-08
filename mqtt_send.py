#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.10"
# dependencies = ["paho-mqtt"]
# ///
"""Send a raw byte to an MQTT topic. Usage: uv run mqtt_send.py <topic> <int>"""
import sys
from paho.mqtt.publish import single

if len(sys.argv) != 3:
    print(f"Usage: {sys.argv[0]} <topic> <int>")
    sys.exit(1)

topic = sys.argv[1]
value = int(sys.argv[2])
single(topic, payload=bytes([value]), hostname="bARtender.local", port=1883)
print(f"Sent byte {value} to '{topic}'")
