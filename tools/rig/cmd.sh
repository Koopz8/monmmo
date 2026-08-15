#!/bin/bash
# Types a console command and checks the server received it.
#
# Three hazards, all seen live:
#  - the console will not open while a dialogue box is up (`talking is null` guards it);
#  - if the leading slash misses, the command text is typed at the game, and Space is an
#    action key — so every space becomes a button press at whatever is being faced;
#  - and pressing Z to clear a dialogue while facing the thing that opened it just opens
#    it again, for ever. So the recovery turns away first.
export DISPLAY=:99
W=$(sed 's/window=//' /tmp/win)

tap() { xdotool keydown --window $W "$1"; sleep 0.10; xdotool keyup --window $W "$1"; sleep 0.30; }

send() {
  tap slash; sleep 0.4
  xdotool type --window $W --delay 55 "$1"; sleep 0.8
  tap Return; sleep 1.4
}

before=$(grep -c "^\$ tester: $1\$" /tmp/s.log)

for try in 1 2 3; do
  send "$1"
  [ "$(grep -c "^\$ tester: $1\$" /tmp/s.log)" -gt "$before" ] && { tap Escape; exit 0; }

  echo "cmd: '$1' did not arrive (try $try)"

  tap Escape
  for i in 1 2 3 4; do tap z; done      # finish whatever is being read
  tap Down; tap Down                    # and face somewhere with nothing in it
  for i in 1 2 3; do tap z; done
  tap Escape; sleep 0.5
done

echo "cmd: '$1' STILL not arrived"
exit 1
