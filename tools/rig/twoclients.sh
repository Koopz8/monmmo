#!/bin/bash
# Brings up a server and two signed-in clients, and leaves them alone.
#
# Written because driving two clients through a negotiation is a different job from walking
# one client around, and the helper built for the second is actively hostile to the first:
# its recovery presses direction keys, so every mistyped console command either cancels a
# trade or walks somebody out of reach of the person they are trading with.
set -u
export DISPLAY=:99 LIBGL_ALWAYS_SOFTWARE=1
cd /home/claude/monmmo

for p in $(pgrep -x dotnet); do
  tr '\0' ' ' < /proc/$p/cmdline 2>/dev/null | grep -qE "monmmo(-server)?\.dll" && kill $p
done
sleep 4

pgrep -f "Xvfb :99" >/dev/null || { rm -f /tmp/.X99-lock; setsid Xvfb :99 -screen 0 1024x768x24 >/dev/null 2>&1 < /dev/null & sleep 3; }

setsid nohup /home/claude/dotnet/dotnet src/Server/bin/Release/net8.0/monmmo-server.dll \
  --operator tester --operator tester2 > /tmp/s.log 2>&1 < /dev/null &
for i in $(seq 1 40); do grep -q "Listening on port" /tmp/s.log && break; sleep 1; done
grep -q "Listening on port" /tmp/s.log || { echo "SERVER FAILED"; exit 1; }

/tmp/second.sh /tmp/win  /tmp/c.log  >/dev/null || exit 1
/tmp/second.sh /tmp/win2 /tmp/c2.log >/dev/null || exit 1

/tmp/login.sh  >/dev/null 2>&1
for i in 1 2 3; do grep -qE "^\+ tester2 " /tmp/s.log && break; /tmp/login2.sh >/dev/null 2>&1; done

grep -qE "^\+ tester "  /tmp/s.log && grep -qE "^\+ tester2 " /tmp/s.log \
  && echo "both signed in" || { echo "SIGN IN FAILED"; exit 1; }
