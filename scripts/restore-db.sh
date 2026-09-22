#!/usr/bin/env bash
#
# Restore the database from a dump taken by backup-db.sh.
#
#   bash scripts/restore-db.sh <dump-file> [--yes] [--no-safety-dump] [--no-stop-api]
#
# DB_NAME selects the target database (default smartPetCareDb), which is what
# lets backup-drill.sh rehearse the whole path against a scratch copy.
#
# The dump file is a name inside <project>/backups or a path to one. The
# database is dropped and recreated rather than restored over: a migration that
# created a table the dump has never heard of would otherwise survive the
# restore and leave the schema in a state no release ever produced.
#
# The api container is left stopped afterwards. It runs Migrate() on startup,
# so starting it while the checkout is still on the release you are rolling
# back from would immediately re-apply the migration you just undid. The script
# prints what to do next instead of guessing.
set -euo pipefail

# Git Bash rewrites arguments that look like absolute paths, which turns the
# container path /backups into C:/Program Files/Git/backups. Ignored elsewhere.
export MSYS_NO_PATHCONV=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BACKUP_DIR="$PROJECT_DIR/backups"

DB_NAME="${DB_NAME:-smartPetCareDb}"
DB_USER="${DB_USER:-postgres}"

ASSUME_YES=0
SAFETY_DUMP=1
STOP_API=1
DUMP_ARG=""

for arg in "$@"; do
    case "$arg" in
        --yes|-y) ASSUME_YES=1 ;;
        --no-safety-dump) SAFETY_DUMP=0 ;;
        --no-stop-api) STOP_API=0 ;;
        -*) echo "unknown option: $arg" >&2; exit 2 ;;
        *) DUMP_ARG="$arg" ;;
    esac
done

log() { echo "[restore-db] $*"; }

if [ -z "$DUMP_ARG" ]; then
    echo "usage: bash scripts/restore-db.sh <dump-file> [--yes] [--no-safety-dump] [--no-stop-api]" >&2
    echo >&2
    echo "available dumps:" >&2
    ls -1t "$BACKUP_DIR"/*.dump 2> /dev/null | head -20 >&2 || echo "  (none)" >&2
    exit 2
fi

DUMP_PATH="$DUMP_ARG"
[ -f "$DUMP_PATH" ] || DUMP_PATH="$BACKUP_DIR/$DUMP_ARG"
if [ ! -f "$DUMP_PATH" ]; then
    log "ERROR: no such dump: $DUMP_ARG"
    exit 1
fi

DUMP_NAME="$(basename "$DUMP_PATH")"
# The db container only sees /backups, so a dump kept anywhere else has to be
# copied in before pg_restore can read it.
if [ "$(cd "$(dirname "$DUMP_PATH")" && pwd)" != "$BACKUP_DIR" ]; then
    log "copying $DUMP_NAME into $BACKUP_DIR"
    cp "$DUMP_PATH" "$BACKUP_DIR/$DUMP_NAME"
fi

cd "$PROJECT_DIR"

cat <<EOF

  Restoring : $DUMP_NAME
  Into      : $DB_NAME (dropped and recreated)
  Checkout  : $(git rev-parse --short HEAD 2> /dev/null || echo "unknown") $(git log -1 --format=%s 2> /dev/null || true)

  Everything written to the database after this dump was taken will be lost.

EOF

if [ "$ASSUME_YES" -ne 1 ]; then
    if [ ! -t 0 ]; then
        log "ERROR: not a terminal and --yes was not given, refusing to restore"
        exit 1
    fi
    read -r -p "Type the database name to confirm: " CONFIRM
    if [ "$CONFIRM" != "$DB_NAME" ]; then
        log "aborted"
        exit 1
    fi
fi

if [ "$SAFETY_DUMP" -eq 1 ]; then
    # Restoring the wrong file is a normal mistake under pressure; this is what
    # makes it survivable.
    log "taking a safety dump of the current database first"
    bash "$SCRIPT_DIR/backup-db.sh" pre-restore
fi

if [ "$STOP_API" -eq 1 ]; then
    log "stopping api"
    docker compose stop api
fi

log "recreating $DB_NAME"
docker compose exec -T db psql -U "$DB_USER" -d postgres -v ON_ERROR_STOP=1 \
    -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DB_NAME' AND pid <> pg_backend_pid();" \
    -c "DROP DATABASE IF EXISTS \"$DB_NAME\";" \
    -c "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\";" \
    > /dev/null

log "restoring $DUMP_NAME"
docker compose exec -T db pg_restore -U "$DB_USER" -d "$DB_NAME" \
    --no-owner --no-privileges --exit-on-error "/backups/$DUMP_NAME"

APPLIED="$(docker compose exec -T db psql -U "$DB_USER" -d "$DB_NAME" -tAc \
    'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1' 2> /dev/null || true)"

log "done. $DB_NAME is back at: ${APPLIED:-unknown migration}"

if [ "$STOP_API" -eq 1 ]; then
    cat <<EOF

  The api container is still stopped. Before starting it, make sure the
  checkout matches that schema -- a newer release will re-apply its migrations
  the moment it boots:

      git log -1 --format='%h %s'
      docker compose up -d --build api
      docker compose logs -f api

EOF
fi
