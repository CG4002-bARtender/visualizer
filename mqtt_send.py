import sys
from paho.mqtt.publish import single

if len(sys.argv) != 3:
    print(f"Usage: {sys.argv[0]} <topic> <int>")
    sys.exit(1)

topic = sys.argv[1]
value = int(sys.argv[2])
single(topic, payload=bytes([value]), hostname="10.236.176.83", port=1883)
print(f"Sent byte {value} to '{topic}'")
