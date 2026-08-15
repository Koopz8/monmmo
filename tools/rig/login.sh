#!/bin/bash
# Held rather than tapped. A press and release inside one frame is a press raylib never
# sees, which is the same thing that made walking unreliable.
export DISPLAY=:99
W=$(sed 's/window=//' /tmp/win)

tap() { xdotool keydown --window $W "$1"; sleep 0.06; xdotool keyup --window $W "$1"; sleep 0.05; }
clear_field() { for i in $(seq 1 20); do tap BackSpace; done; }
attempt() {
  clear_field; xdotool type --window $W --delay 90 "$1"; sleep 0.8
  tap Tab; sleep 0.4
  clear_field; xdotool type --window $W --delay 90 "$2"; sleep 0.8
  tap Return; sleep 5
}

attempt "tester" "a-good-password"
grep -q "^+ tester" /tmp/s.log && { echo "signed in"; exit 0; }

tap Tab
attempt "tester" "a-good-password"
grep -q "^+ tester" /tmp/s.log && echo "signed in" || echo "SIGN IN FAILED"
