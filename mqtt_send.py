import ssl
import sys
from pathlib import Path
from paho.mqtt.publish import single

if len(sys.argv) != 3:
    print(f"Usage: {sys.argv[0]} <topic> <int>")
    sys.exit(1)

STREAMING_ASSETS = Path(__file__).parent / "Assets/StreamingAssets"
CA_CERT     = STREAMING_ASSETS / "ca.crt"
CLIENT_CERT = STREAMING_ASSETS / "unity.crt"
CLIENT_KEY  = STREAMING_ASSETS / "unity.key"

tls = {
    "ca_certs": str(CA_CERT),
    "certfile": str(CLIENT_CERT),
    "keyfile":  str(CLIENT_KEY),
    "tls_version": ssl.PROTOCOL_TLS_CLIENT,
}

topic = sys.argv[1]
value = int(sys.argv[2])
single(topic, payload=bytes([value]), hostname="bARtender.local", port=8883, tls=tls)
print(f"Sent byte {value} to '{topic}'")
