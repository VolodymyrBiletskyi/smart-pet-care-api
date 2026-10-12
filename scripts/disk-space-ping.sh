#!/usr/bin/env bash
#
# Report that this host still has room, so a watcher elsewhere can alert when
# the report stops arriving.
#
#   bash scripts/disk-space-ping.sh
#
# Deliberately inverted. An alert that fires when the disk is full has to run on
# the host that is full: in October 2026 the nightly backup did exactly that,
# failed every night on a 100% disk, and told nobody, because a cron with
# nowhere to write cannot raise an alarm about having nowhere to write. So the
# ping is sent while things are *fine* and the absence of it is the signal.
#
# Settings come from the environment or from the project .env:
#   DISK_PING_URL       url pinged while free space is above the floor; with it
#                       unset the script does nothing, which is why installing
#                       the cron is harmless before the watcher exists
#   DISK_MIN_FREE_MB    the floor, in MB (default 1000)
#
# The floor is absolute rather than a percentage because what matters is whether
# Postgres can still write a WAL segment and the backup can still write a dump,
# and neither of those costs a share of the volume. A percentage also quietly
# loosens as the disk grows, which is the mistake journald's 10% default makes.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

# Read, not sourced -- see the same note in backup-db.sh: .env holds unquoted
# secrets a shell would choke on.
env_value() {
    [ -f "$PROJECT_DIR/.env" ] || return 0
    sed -n "s/^[[:space:]]*$1[[:space:]]*=//p" "$PROJECT_DIR/.env" \
        | tail -1 | tr -d '\r' | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'\$/\1/"
}

PING_URL="${DISK_PING_URL-$(env_value DISK_PING_URL)}"
MIN_FREE_MB="${DISK_MIN_FREE_MB:-$(env_value DISK_MIN_FREE_MB)}"
MIN_FREE_MB="${MIN_FREE_MB:-1000}"

log() { echo "[disk-space-ping] $*"; }

[ -n "$PING_URL" ] || exit 0

# The project directory, not /: it is where the dumps are written, and on this
# host it is the same filesystem as the docker and containerd stores anyway.
AVAIL_MB="$(df -Pk "$PROJECT_DIR" | awk 'NR == 2 { print int($4 / 1024) }')"

if [ "${AVAIL_MB:-0}" -lt "$MIN_FREE_MB" ]; then
    # No ping, and a log line for whoever comes looking once the mail arrives.
    log "only ${AVAIL_MB}MB free, below the ${MIN_FREE_MB}MB floor -- not pinging"
    exit 1
fi

curl -fsS -m 10 "$PING_URL" > /dev/null
