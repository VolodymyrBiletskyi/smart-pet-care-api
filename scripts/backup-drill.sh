#!/usr/bin/env bash
#
# Rehearse the whole backup/restore path against a scratch database.
#
#   bash scripts/backup-drill.sh [--keep]
#
# It runs the real backup-db.sh and restore-db.sh -- same dump, same
# verification, same drop-and-recreate -- but with DB_NAME pointed at a
# throwaway database, so the application data is never touched and the api is
# never stopped. Safe to run against production; it is the quarterly proof that
# the dumps are restorable.
#
# --keep leaves the drill database and its dumps behind for inspection.
set -euo pipefail

export MSYS_NO_PATHCONV=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BACKUP_DIR="$PROJECT_DIR/backups"

DRILL_DB="backup_drill"
DB_USER="${DB_USER:-postgres}"
ROWS=500
KEEP=0
[ "${1:-}" = "--keep" ] && KEEP=1

cd "$PROJECT_DIR"

log() { echo "[drill] $*"; }
fail() { echo "[drill] FAILED: $*" >&2; exit 1; }

psql_drill() { docker compose exec -T db psql -U "$DB_USER" -d "$DRILL_DB" -v ON_ERROR_STOP=1 "$@"; }
psql_admin() { docker compose exec -T db psql -U "$DB_USER" -d postgres -v ON_ERROR_STOP=1 "$@"; }

cleanup() {
    [ "$KEEP" -eq 1 ] && { log "--keep: leaving $DRILL_DB and backups/$DRILL_DB-*.dump"; return; }
    psql_admin -c "DROP DATABASE IF EXISTS \"$DRILL_DB\";" > /dev/null 2>&1 || true
    rm -f "$BACKUP_DIR/$DRILL_DB"-*.dump "$BACKUP_DIR/$DRILL_DB"-*.dump.partial
}
trap cleanup EXIT

[ -n "$(docker compose ps -q db 2> /dev/null)" ] || fail "the db container is not running (docker compose up -d db)"

log "creating $DRILL_DB with $ROWS rows"
psql_admin -c "DROP DATABASE IF EXISTS \"$DRILL_DB\";" > /dev/null
psql_admin -c "CREATE DATABASE \"$DRILL_DB\" OWNER \"$DB_USER\";" > /dev/null
psql_drill -c "CREATE TABLE drill_rows (id int primary key, payload text not null);" > /dev/null
psql_drill -c "INSERT INTO drill_rows SELECT g, md5(g::text) FROM generate_series(1, $ROWS) g;" > /dev/null

BEFORE="$(psql_drill -tAc "SELECT md5(string_agg(payload, ',' ORDER BY id)) FROM drill_rows;")"
log "checksum before: $BEFORE"

# BACKUP_S3_BUCKET is cleared: a drill has no business uploading anything.
log "--- backup ---"
DB_NAME="$DRILL_DB" BACKUP_S3_BUCKET="" bash "$SCRIPT_DIR/backup-db.sh" drill

DUMP="$(ls -1t "$BACKUP_DIR/$DRILL_DB"-*-drill.dump 2> /dev/null | head -1)" || true
[ -n "${DUMP:-}" ] || fail "backup-db.sh produced no dump"
DUMP_NAME="$(basename "$DUMP")"

log "--- verification rejects a truncated dump ---"
TRUNCATED="$BACKUP_DIR/$DRILL_DB-truncated.dump"
# Two thirds of the file: enough to keep a valid table of contents, so this
# only fails if the check really reads the data blocks.
head -c "$(( $(wc -c < "$DUMP") * 2 / 3 ))" "$DUMP" > "$TRUNCATED"
if docker compose exec -T db pg_restore -f /dev/null "/backups/$(basename "$TRUNCATED")" > /dev/null 2>&1; then
    rm -f "$TRUNCATED"
    fail "pg_restore accepted a truncated dump -- the verification step is not protecting anything"
fi
rm -f "$TRUNCATED"
log "ok: a cut-off dump is rejected"

log "--- destroying the data ---"
psql_drill -c "DROP TABLE drill_rows;" > /dev/null
psql_drill -c "CREATE TABLE leftover_from_bad_migration (id int);" > /dev/null

log "--- restore ---"
DB_NAME="$DRILL_DB" bash "$SCRIPT_DIR/restore-db.sh" "$DUMP_NAME" --yes --no-safety-dump --no-stop-api

AFTER="$(psql_drill -tAc "SELECT md5(string_agg(payload, ',' ORDER BY id)) FROM drill_rows;")"
COUNT="$(psql_drill -tAc 'SELECT count(*) FROM drill_rows;')"
LEFTOVER="$(psql_drill -tAc "SELECT count(*) FROM pg_tables WHERE tablename = 'leftover_from_bad_migration';")"

[ "$COUNT" = "$ROWS" ] || fail "expected $ROWS rows, got $COUNT"
[ "$AFTER" = "$BEFORE" ] || fail "checksum mismatch: $BEFORE -> $AFTER"
# The drop-and-recreate is the point: a table the dump never knew about must not
# survive, or the restored schema is one no release ever produced.
[ "$LEFTOVER" = "0" ] || fail "leftover_from_bad_migration survived the restore"

cat <<EOF

[drill] PASSED
          dump       : $DUMP_NAME
          rows       : $COUNT
          checksum   : $AFTER
          truncated dump rejected, stray table gone

EOF
