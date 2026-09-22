#!/usr/bin/env bash
#
# Take a compressed dump of the production database, verify it, prune old
# copies and ship it off the host.
#
#   bash scripts/backup-db.sh [label]
#
# The label ends up in the file name and says why the dump exists: "scheduled"
# for the cron run, "predeploy-<sha>" for the one the deploy workflow takes,
# "pre-restore" for the safety copy restore-db.sh takes before overwriting the
# database. Anything else is free-text.
#
# Dumps live in <project>/backups, which docker-compose mounts into the db
# container as /backups. Both sides of that mount are assumed below, so the
# directory is not configurable.
#
# Settings come from the environment or from the project .env:
#   BACKUP_RETENTION_DAYS   how long local dumps are kept (default 14)
#   BACKUP_S3_BUCKET        bucket (or bucket/prefix) for the off-host copy;
#                           the upload is skipped when it is unset
set -euo pipefail

# Git Bash rewrites arguments that look like absolute paths, which turns the
# container path /backups into C:/Program Files/Git/backups. Ignored elsewhere.
export MSYS_NO_PATHCONV=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

# .env is read, not sourced: docker compose parses it as plain key=value, so the
# secrets in it are unquoted and contain characters (&, $, quotes) that a shell
# would choke on or expand. Only the two keys this script needs are taken, and
# the CR is stripped in case the file was saved from Windows.
env_value() {
    [ -f "$PROJECT_DIR/.env" ] || return 0
    sed -n "s/^[[:space:]]*$1[[:space:]]*=//p" "$PROJECT_DIR/.env" \
        | tail -1 | tr -d '\r' | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'\$/\1/"
}

LABEL="${1:-manual}"
DB_NAME="${DB_NAME:-smartPetCareDb}"
DB_USER="${DB_USER:-postgres}"
BACKUP_DIR="$PROJECT_DIR/backups"
S3_BUCKET="${BACKUP_S3_BUCKET-$(env_value BACKUP_S3_BUCKET)}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-$(env_value BACKUP_RETENTION_DAYS)}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

mkdir -p "$BACKUP_DIR"
cd "$PROJECT_DIR"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
NAME="$DB_NAME-$STAMP-$LABEL.dump"
DEST="$BACKUP_DIR/$NAME"
TMP="$DEST.partial"

log() { echo "[backup-db] $*"; }

# The cron run and a deploy can collide; a second dump while the first is still
# streaming competes for the same connection budget for no benefit. Git Bash has
# no flock, and no cron either, so on a dev box the lock is simply skipped.
if command -v flock > /dev/null 2>&1; then
    exec 9>"$BACKUP_DIR/.backup.lock"
    if ! flock -w 600 9; then
        log "another backup is still running, giving up"
        exit 1
    fi
fi

log "dumping $DB_NAME -> $NAME"
# Written under a .partial name first: a dump killed halfway through must never
# be left sitting in the directory looking like something you can restore from.
docker compose exec -T db pg_dump -U "$DB_USER" -d "$DB_NAME" -Fc > "$TMP"

# Converting the whole archive to SQL and throwing it away reads and
# decompresses every data block, so a truncated or corrupt dump fails here
# rather than during an incident. `pg_restore --list` is not enough: it stops
# after the table of contents at the head of the file and happily accepts a
# dump whose data was cut off. Nothing is written and no database is touched.
if ! docker compose exec -T db pg_restore -f /dev/null "/backups/$(basename "$TMP")" > /dev/null; then
    log "ERROR: dump did not verify, keeping it as $TMP for inspection"
    exit 1
fi

mv "$TMP" "$DEST"
log "wrote $DEST ($(du -h "$DEST" | cut -f1))"

if [ -n "$S3_BUCKET" ]; then
    if command -v aws > /dev/null 2>&1; then
        log "uploading to s3://$S3_BUCKET/$NAME"
        aws s3 cp "$DEST" "s3://$S3_BUCKET/$NAME" --only-show-errors
    else
        # Not fatal: a local dump still exists, and failing a deploy over a
        # missing CLI would be worse than the missing off-host copy.
        log "WARNING: BACKUP_S3_BUCKET is set but the aws CLI is not installed, skipping upload"
    fi
fi

DELETED="$(find "$BACKUP_DIR" -maxdepth 1 -name "$DB_NAME-*.dump" -mtime "+$RETENTION_DAYS" -print -delete | wc -l)"
if [ "$DELETED" -gt 0 ]; then
    log "pruned $DELETED dump(s) older than $RETENTION_DAYS days"
fi

log "done"
