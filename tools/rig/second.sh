#!/bin/bash
# Starts one more client and writes its window id to $1, by difference.
export DISPLAY=:99 LIBGL_ALWAYS_SOFTWARE=1
cd /home/claude/monmmo
before=$(xdotool search --name MonMMO 2>/dev/null | tr '\n' ' ')
python3 -c "import json;p='client.json';d=json.load(open(p));d['Username']='';json.dump(d,open(p,'w'),indent=2)"
setsid nohup /home/claude/dotnet/dotnet src/Client/bin/Release/net8.0/monmmo.dll > "$2" 2>&1 < /dev/null &
for i in $(seq 1 30); do
  for w in $(xdotool search --name MonMMO 2>/dev/null); do
    case " $before " in *" $w "*) ;; *) echo "window=$w" > "$1"; sleep 5; echo "started $w"; exit 0;; esac
  done
  sleep 2
done
echo "NO WINDOW"; exit 1
