#!/usr/bin/env bash
#
# Everything a fresh cloud session needs, after the repo has been staged into it.
#
# The container start-up used to be about fifteen tool calls and five minutes of working
# out the same things again: where dotnet is (nowhere), what the tip commit is, whether
# the cartridge came across, whether the build works. None of that is thinking. This is.
#
# The three lines that come BEFORE this script, because it lives inside the thing they
# fetch — put them in the session prompt, not here:
#
#   mkdir -p ~/work && tar xzf /mnt/user-data/uploads/pokemmo/_transfer.tar.gz -C ~/work
#   git -c safe.directory='*' clone -q ~/work/repo.git ~/pokemmo
#   bash ~/pokemmo/tools/session-setup.sh
#
# And the two device-bridge calls that make that tarball, which are tool calls rather
# than shell and so cannot live here either:
#
#   device_bash:        rm -rf /tmp/repo.git /tmp/repo.tar.gz \
#                       && git clone --no-hardlinks --bare "$HOME/mnt/pokemmo" /tmp/repo.git \
#                       && tar czf /tmp/repo.tar.gz -C /tmp repo.git \
#                       && cp /tmp/repo.tar.gz "$HOME/mnt/pokemmo/_transfer.tar.gz"
#   device_stage_files: _transfer.tar.gz and (with permission) firered.gba
#
# A local clone reads the working copy's .git and writes nothing to it, which is why it
# is safe where running git in that folder is not — OneDrive leaves an index.lock behind.

set -u

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

say() { printf '  %s\n' "$*"; }

echo
echo "SETTING UP — $repo"
echo

# ---------------------------------------------------------------- the .NET 8 SDK
#
# It is not in the image and apt's index is usually stale enough to 404 on it, so the
# install script is the reliable path. Doing nothing when it is already there matters:
# this script gets run again after a resume.

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
    say "dotnet $(dotnet --version) — already here"
else
    say "installing the .NET 8 SDK (a minute)"
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
        && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_ROOT" --no-path >/tmp/dotnet-install.log 2>&1 \
        || { say "the SDK did not install — see /tmp/dotnet-install.log"; exit 1; }
    say "dotnet $(dotnet --version)"
fi

# ------------------------------------------------------------------ the cartridge
#
# NEVER COMMITTED. `*.gba` is in .gitignore and this only copies what the bridge already
# staged, with permission, into the uploads directory. If it is not there the reading
# instruments simply cannot run, and that is a state to report rather than work around.

staged="/mnt/user-data/uploads/pokemmo/firered.gba"

if [ -f "$repo/firered.gba" ]; then
    say "cartridge already in place"
elif [ -f "$staged" ]; then
    cp "$staged" "$repo/firered.gba"
    say "cartridge copied in — $(sha1sum "$repo/firered.gba" | cut -c1-40)"
else
    say "NO CARTRIDGE STAGED — the instruments cannot run; ask before staging one"
fi

# ------------------------------------------------------- no remote, and say why
#
# The transfer clone leaves an `origin` pointing at a bare copy inside this container.
# It is not a remote in any sense that matters: nothing can be pushed to it and nothing
# reads it. Left in place it makes every stop-hook ask for a push that cannot happen.

cd "$repo" || exit 1

if [ -n "$(git remote)" ]; then
    git remote | while read -r r; do git remote remove "$r"; done
    say "removed the transfer remote — this repo is handed over as bundles, not pushed"
fi

git config user.name "Claude"
git config user.email "noreply@anthropic.com"

# ------------------------------------------------------------------- build and say
echo
say "tip: $(git log --oneline -1)"

if dotnet build -c Release --nologo -v q src/Tools/RomDump >/tmp/build.log 2>&1; then
    say "RomDump built"
else
    say "BUILD FAILED — see /tmp/build.log"
    exit 1
fi

echo
echo "  Ready. Start with the three the prompt asks for:"
echo "    dotnet run -c Release --no-build --project src/Tools/RomDump -- firered.gba --the-floor"
echo "    dotnet run -c Release --no-build --project src/Tools/RomDump -- firered.gba --flags"
echo "    dotnet run -c Release --no-build --project src/Tools/RomDump -- firered.gba --who-knows"
echo
echo "  and put this in front of every later shell:"
echo "    export DOTNET_ROOT=\$HOME/.dotnet PATH=\$HOME/.dotnet:\$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1"
echo
