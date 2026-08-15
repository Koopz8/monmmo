#!/bin/bash
# Signs the second client in as tester2, creating the account the first time.
export DISPLAY=:99
W=$(sed 's/window=//' /tmp/win2)
tap() { xdotool keydown --window $W "$1"; sleep 0.08; xdotool keyup --window $W "$1"; sleep 0.14; }
clear_field() { for i in $(seq 1 30); do tap BackSpace; done; }

fill() {
  tap Tab; sleep 0.4; clear_field
  xdotool type --window $W --delay 80 "tester2"; sleep 0.8
  tap Tab; sleep 0.4; clear_field
  xdotool type --window $W --delay 80 "a-good-password"; sleep 0.8
}

sleep 1; fill
xdotool keydown --window $W Return; sleep 0.2; xdotool keyup --window $W Return; sleep 6
grep -qE "^\+ tester2" /tmp/s.log && { echo "signed in"; exit 0; }

fill; tap F1; sleep 0.6
xdotool keydown --window $W Return; sleep 0.2; xdotool keyup --window $W Return; sleep 8
grep -qE "^\+ tester2" /tmp/s.log && echo "registered" || echo "SIGN IN FAILED"
