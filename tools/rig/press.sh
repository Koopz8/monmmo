#!/bin/bash
# Keys to one window and nothing else. $1 = window file, rest = keys.
export DISPLAY=:99
W=$(sed 's/window=//' "$1"); shift
for k in "$@"; do
  xdotool keydown --window $W "$k"; sleep 0.15; xdotool keyup --window $W "$k"; sleep 0.55
done
