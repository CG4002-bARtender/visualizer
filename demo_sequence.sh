#!/bin/bash
cd "$(dirname "$0")"

run() { python3.11 mqtt_send.py "$1" "$2"; sleep 3; }

run glove 2
run hall 1
run glove 0
run glove 2
run glove 3
run glove 1
run hall 2
run glove 0
run glove 3
run hall 2
run glove 2
run glove 2
run glove 1
python3.11 mqtt_send.py glove 4
