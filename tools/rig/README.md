# The rig

Scripts for driving the client headlessly: Xvfb, xdotool, and screenshots. They live here
now because they have been rebuilt from memory several times and because two of them have
each cost a milestone by doing something helpful at the wrong moment.

## One client

    ./rig.sh --operator tester     # server + one client, signed in
    ./cmd.sh "tp 3.1 11 22"        # a console command, with retries
    ./shot.sh                      # /tmp/shot.png

`cmd.sh` retries, and its recovery presses direction keys and Z. That is right for driving
one client around a world and **wrong for anything involving a second person**: it walks the
player out of reach and dismisses whatever is on screen.

## Two clients

    ./twoclients.sh                # server + two clients, both signed in
    ./say.sh /tmp/win  "trade 2"   # one command, once, no recovery at all
    ./press.sh /tmp/win2 z c       # keys to one window and nothing else
    ./shot.sh ; ./shot2.sh

`say.sh` and `press.sh` exist because a negotiation between two clients cannot survive a
helper that presses keys on its own initiative.

## Two things learned the hard way

**A window that is not on top captures as a black rectangle.** With two clients and no
compositor, `import -window` on an obscured window returns black — which looks exactly like
a rendering bug and is not one. `shot.sh` raises the window first.

**Escape is not a safe way to close a console.** It is also "call it off" on the trade
screen, so a helper that pressed it after every command cancelled the trade it had just
opened. The console closes on Return by itself; `say.sh` presses nothing afterwards.
