#!/bin/bash
# Opens 4 Terminal.app windows with commands, then tiles them via Rectangle.
# Usage: chmod +x dev-setup.sh && ./dev-setup.sh

CMD1="sudo /opt/homebrew/sbin/mosquitto -v -c /opt/homebrew/etc/mosquitto/mosquitto.conf"
CMD2="cd ~/Projects/server && uv run main.py"
CMD3="cd ~/Projects/visualizer"
CMD4="cd ~/Projects/visualizer && claude"

# Open 4 Terminal windows, each running a command
osascript <<EOF
tell application "Terminal"
    activate
    do script "$CMD1"
    do script "$CMD2"
    do script "$CMD3"
    do script "$CMD4"
end tell
EOF

sleep 1.5

# Tile windows into quadrants using AppleScript + screen geometry
osascript <<'TILE'
tell application "Terminal"
    activate
end tell

tell application "Finder"
    set screenBounds to bounds of window of desktop
    set screenW to item 3 of screenBounds
    set screenH to item 4 of screenBounds
end tell

-- Menu bar height
set menuBar to 25
set halfW to screenW / 2
set halfH to (screenH - menuBar) / 2

tell application "Terminal"
    set wins to every window
    if (count of wins) < 4 then return

    -- Top-left
    set bounds of item 1 of wins to {0, menuBar, halfW, menuBar + halfH}
    -- Top-right
    set bounds of item 2 of wins to {halfW, menuBar, screenW, menuBar + halfH}
    -- Bottom-left
    set bounds of item 3 of wins to {0, menuBar + halfH, halfW, screenH}
    -- Bottom-right
    set bounds of item 4 of wins to {halfW, menuBar + halfH, screenW, screenH}
end tell
TILE

echo "Done — 4 terminals tiled."
