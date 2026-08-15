#!/bin/bash
# One console command to one window, once, with no recovery of any kind.
#
# The no-recovery part is the point. A helper that presses direction keys when it thinks a
# command was missed is a helper that cannot be used while two people are standing next to
# each other on purpose.
export DISPLAY=:99
W=$(sed 's/window=//' "$1")
tap() { xdotool keydown --window $W "$2"; sleep 0.15; xdotool keyup --window $W "$2"; sleep 0.35; }
tap x slash; sleep 0.5
xdotool type --window $W --delay 55 "$2"; sleep 0.7
tap x Return; sleep 1.2

# No trailing Escape. The console closes on Return by itself, and Escape is the trade
# screen's "call it off" — a helper that pressed it after every command cancelled the very
# trade it had just opened, twice, before anybody noticed.
