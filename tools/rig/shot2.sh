#!/bin/bash
# Raised before it is captured. With two windows on one display and no compositor, an
# obscured window captures as a black rectangle — which is not a rendering bug, though it
# looks exactly like one.
export DISPLAY=:99
W=$(sed 's/window=//' /tmp/win2)
xdotool windowraise $W 2>/dev/null; sleep 0.6
import -window $W /tmp/shot2.png 2>/dev/null || import -window root /tmp/shot2.png
