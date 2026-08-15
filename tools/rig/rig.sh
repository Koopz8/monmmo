#!/bin/bash
set -u
export DISPLAY=:99 LIBGL_ALWAYS_SOFTWARE=1
cd /home/claude/monmmo
DN=/home/claude/dotnet/dotnet
S=src/Server/bin/Release/net8.0/monmmo-server
C=src/Client/bin/Release/net8.0/monmmo

pkill -f "net8.0/monmmo-server.dll" 2>/dev/null; pkill -f "net8.0/monmmo.dll" 2>/dev/null; sleep 4
pgrep -f "Xvfb :99" >/dev/null || { rm -f /tmp/.X99-lock; setsid Xvfb :99 -screen 0 1024x768x24 >/dev/null 2>&1 < /dev/null & sleep 3; }

python3 -c "import json;p='/home/claude/monmmo/client.json';d=json.load(open(p));d['Username']='';json.dump(d,open(p,'w'),indent=2)"

setsid nohup $DN $S.dll "$@" > /tmp/s.log 2>&1 < /dev/null &
for i in $(seq 1 40); do grep -q "Listening on port" /tmp/s.log && break; sleep 1; done
grep -q "Listening on port" /tmp/s.log || { echo "SERVER FAILED"; tail -4 /tmp/s.log; exit 1; }

setsid nohup $DN $C.dll > /tmp/c.log 2>&1 < /dev/null &
for i in $(seq 1 30); do W=$(xdotool search --name MonMMO 2>/dev/null | head -1); [ -n "$W" ] && break; sleep 2; done
W=$(xdotool search --name MonMMO 2>/dev/null | head -1)
[ -z "$W" ] && { echo "NO WINDOW"; exit 1; }
sleep 4; echo "window=$W" > /tmp/win; echo "window=$W"

xdotool key --window "$W" Tab; sleep 0.3
for i in $(seq 1 40); do xdotool key --window "$W" BackSpace; sleep 0.02; done
xdotool type --window "$W" --delay 100 "a-good-password"; sleep 0.8
xdotool keydown --window "$W" Return; sleep 0.25; xdotool keyup --window "$W" Return; sleep 6
echo "--- server ---"; grep -v "^ " /tmp/s.log | tail -4
