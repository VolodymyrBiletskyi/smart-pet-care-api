#!/usr/bin/env bash
#
# Roll the deployed code back to an earlier commit.
#
#   bash scripts/rollback.sh --last          # the release before this one
#   bash scripts/rollback.sh <sha> [--yes]
#
# This rolls back *code only*. The schema does not travel with it: a migration
# that has already run stays applied, and the old build will happily start on
# top of it. That is fine for an additive migration and wrong for one that
# dropped or tightened a column -- see docs/backup-and-rollback.md. The script
# prints the migrations the release being rolled back introduced so you can
# tell which case you are in before it touches anything.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BACKUP_DIR="$PROJECT_DIR/backups"
RELEASES_DIR="$BACKUP_DIR/releases"

cd "$PROJECT_DIR"

ASSUME_YES=0
TARGET=""

for arg in "$@"; do
    case "$arg" in
        --last) TARGET="--last" ;;
        --yes|-y) ASSUME_YES=1 ;;
        -*) echo "unknown option: $arg" >&2; exit 2 ;;
        *) TARGET="$arg" ;;
    esac
done

log() { echo "[rollback] $*"; }

if [ -z "$TARGET" ]; then
    echo "usage: bash scripts/rollback.sh <sha>|--last [--yes]" >&2
    echo >&2
    echo "recent releases:" >&2
    ls -1t "$RELEASES_DIR" 2> /dev/null | head -10 >&2 || echo "  (none recorded)" >&2
    exit 2
fi

CURRENT_SHA="$(git rev-parse HEAD)"

if [ "$TARGET" = "--last" ]; then
    if [ ! -f "$BACKUP_DIR/.last-deployed-sha" ]; then
        log "ERROR: no previous release recorded yet (backups/.last-deployed-sha is missing)"
        exit 1
    fi
    TARGET="$(cat "$BACKUP_DIR/.last-deployed-sha")"
    if [ "$TARGET" = "$CURRENT_SHA" ]; then
        log "ERROR: the recorded previous release is the one already checked out"
        exit 1
    fi
fi

git fetch origin --quiet
if ! git rev-parse --verify --quiet "$TARGET^{commit}" > /dev/null; then
    log "ERROR: unknown commit: $TARGET"
    exit 1
fi
TARGET_SHA="$(git rev-parse "$TARGET^{commit}")"

NEW_MIGRATIONS="$(git diff --name-only "$TARGET_SHA" "$CURRENT_SHA" -- Migrations/ \
    | grep -v -e '\.Designer\.cs$' -e 'ModelSnapshot\.cs$' || true)"

cat <<EOF

  Rolling back to : $(git log -1 --format='%h %s' "$TARGET_SHA")
  From            : $(git log -1 --format='%h %s' "$CURRENT_SHA")

EOF

if [ -n "$NEW_MIGRATIONS" ]; then
    cat <<EOF
  These migrations were added between the two and stay applied:

$(echo "$NEW_MIGRATIONS" | sed 's/^/      /')

  If any of them dropped or tightened something the old build writes to, the
  code rollback alone will not be enough -- undo the schema first
  (docs/backup-and-rollback.md, "Case 2 -- the migration has to come off").

EOF
else
    echo "  No migrations differ between the two commits, so the schema is unaffected."
    echo
fi

if [ "$ASSUME_YES" -ne 1 ]; then
    if [ ! -t 0 ]; then
        log "ERROR: not a terminal and --yes was not given, refusing to roll back"
        exit 1
    fi
    read -r -p "Proceed? [y/N] " CONFIRM
    case "$CONFIRM" in
        y|Y|yes) ;;
        *) log "aborted"; exit 1 ;;
    esac
fi

# The rollback is itself a change to production, and the state it replaces is
# the last thing anyone has a copy of.
bash "$SCRIPT_DIR/backup-db.sh" "prerollback-${TARGET_SHA:0:7}"

log "checking out $TARGET_SHA"
git reset --hard "$TARGET_SHA"

IMAGE_REPO="${API_IMAGE_REPO:-ghcr.io/volodymyrbiletskyi/smart-pet-care-api}"

# Going back to the image CI built for that commit, not to a fresh build of it:
# the point of a rollback is the binary that was known to run, and a rebuild is
# a new artifact however identical the source.
log "starting $IMAGE_REPO:${TARGET_SHA:0:7}"
export API_IMAGE="$IMAGE_REPO:$TARGET_SHA"
if docker compose pull api; then
    docker compose up -d
else
    # Releases from before the registry, and anything since pruned out of it,
    # have no image to go back to. Building on the host is the fallback rather
    # than the plan -- it pulls the ~3GB sdk image, which is exactly what the
    # registry exists to keep off this disk.
    log "no image for $TARGET_SHA in $IMAGE_REPO, falling back to building here"
    log "check free space first if this is the small volume: df -h /"
    unset API_IMAGE
    docker compose up -d --build
fi
docker image prune -f > /dev/null

docker compose ps
log "rolled back to ${TARGET_SHA:0:7}"
log "note: origin/main still points at the bad release -- revert it there too,"
log "      or the next push will redeploy it"
