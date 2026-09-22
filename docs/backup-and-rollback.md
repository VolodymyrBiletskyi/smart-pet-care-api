# Database backups and rollback

The api applies migrations itself on startup (`db.Database.Migrate()` in
`Program.cs`), and the deploy workflow rebuilds from `origin/main` with no
tagged image to go back to. So a release that goes wrong changes both the code
and the schema, and neither reverts on its own. Everything below exists to make
that recoverable.

## What runs

| | |
|---|---|
| Scheduled dump | daily at 03:17 UTC, host cron, re-installed on every deploy |
| Pre-deploy dump | every deploy, after the new code is checked out and before any container starts |
| Pre-restore dump | automatically, by `restore-db.sh`, before it overwrites anything |
| Pre-rollback dump | automatically, by `rollback.sh` |
| Format | `pg_dump -Fc` (compressed, restorable selectively) |
| Location | `/home/ubuntu/smart-pet-care-api/backups`, mounted into the db container as `/backups` |
| Off-host copy | `s3://$BACKUP_S3_BUCKET/` when `BACKUP_S3_BUCKET` is set |
| Local retention | 14 days (`BACKUP_RETENTION_DAYS`) |

File names carry why the dump exists:
`smartPetCareDb-20260922T031700Z-scheduled.dump`,
`…-predeploy-9891226.dump`, `…-pre-restore.dump`, `…-prerollback-b09adff.dump`.

Each deploy also writes `backups/releases/<stamp>-<sha>.txt` with the commit it
replaced and the migrations the release introduced, and updates
`backups/.last-deployed-sha` — which is what `rollback.sh --last` reads.

Every dump is verified before it is accepted: `pg_restore -f /dev/null` converts
the whole archive to SQL and throws it away, which reads and decompresses every
data block without touching a database. A dump that was cut short keeps its
`.partial` name and never looks restorable. (`pg_restore --list` is not a
substitute — it stops after the table of contents at the head of the file and
returns success on a dump whose data is missing. It costs a full decompression
pass on every backup, which is the price of the check meaning anything.)

## One-time server setup

1. **aws CLI on the host** — `sudo apt-get install -y awscli`, or the v2
   installer. The upload runs on the host rather than in a container on
   purpose: EC2's IMDSv2 hop limit is 1 by default, so a container cannot reach
   the instance metadata service and would not see the instance role. If the
   CLI is missing the script warns and keeps the local dump instead of failing
   the deploy.
2. **IAM** — attach a role to the instance allowing `s3:PutObject` (and
   `s3:ListBucket`) on the backup bucket only. No access keys in `.env`.
3. **Bucket** — block public access, enable versioning (so a malicious or buggy
   overwrite is still recoverable) and a lifecycle rule: transition to
   Glacier Instant Retrieval after 30 days, expire after 180. Pick your own
   numbers, but write them down somewhere.
4. **`.env` on the server** — add:

   ```
   BACKUP_S3_BUCKET=your-bucket/smart-pet-care/postgres
   BACKUP_RETENTION_DAYS=14
   ```

5. Deploy once. The cron entry installs itself; check it with `crontab -l`.

## Taking and checking backups by hand

```bash
cd /home/ubuntu/smart-pet-care-api

bash scripts/backup-db.sh manual        # one off, same pipeline as cron
ls -lt backups/*.dump | head
tail backups/backup.log                 # what cron did

docker compose exec -T db pg_restore --list /backups/<file>.dump | head
```

**A backup nobody has restored is not a backup.**

```bash
bash scripts/backup-drill.sh
```

The drill runs the real `backup-db.sh` and `restore-db.sh` against a scratch
`backup_drill` database — same dump, same verification, same drop-and-recreate —
and checks that the rows come back byte-identical, that a truncated dump is
rejected, and that a table the dump never knew about does not survive the
restore. It never touches the application database, never stops `api` and never
uploads anything to S3, so it is safe to run on production. Do it quarterly, and
after any change to these scripts.

To prove the *real* dumps are restorable (not just a synthetic one), restore the
newest production dump into the drill database by hand:

```bash
docker compose exec -T db psql -U postgres -d postgres -c 'CREATE DATABASE restore_drill;'
docker compose exec -T db pg_restore -U postgres -d restore_drill \
    --no-owner --no-privileges --exit-on-error /backups/<file>.dump
docker compose exec -T db psql -U postgres -d restore_drill \
    -c 'SELECT count(*) FROM "Pets";' -c 'SELECT max("MigrationId") FROM "__EFMigrationsHistory";'
docker compose exec -T db psql -U postgres -d postgres -c 'DROP DATABASE restore_drill;'
```

## Trying it before it matters

Everything except the deploy workflow itself can be exercised on a dev box:

```bash
docker compose up -d db          # api is not needed for any of this
bash scripts/backup-drill.sh     # the full loop, end to end
bash scripts/backup-db.sh manual && ls -lt backups/*.dump | head
bash scripts/restore-db.sh       # no arguments: prints the available dumps
bash scripts/rollback.sh         # no arguments: prints the recorded releases
bash scripts/rollback.sh HEAD~1  # answer "n" -- the migration diff prints first
```

`rollback.sh` shows which migrations differ and asks before it changes
anything, so stopping at the prompt is a complete rehearsal of the decision you
would be making during an incident.

Two things only a real deploy can prove: that the cron entry installs, and that
the pre-deploy dump lands before the new containers start. Check both on the
first deploy:

```bash
crontab -l
ls -lt backups/*predeploy* | head -3
cat backups/releases/$(ls -t backups/releases | head -1)
tail backups/backup.log
```

And check the S3 side once, by hand, before trusting it:

```bash
aws sts get-caller-identity                  # is the instance role attached?
aws s3 ls "s3://$BACKUP_S3_BUCKET/"          # does it list after a backup run?
```

## Rollback

Three different failures, three different answers. Picking the wrong one is how
a bad deploy turns into a bad week.

### Which case am I in?

```bash
cat backups/releases/$(ls -t backups/releases | head -1)
```

If `new_migrations` is empty, the schema is untouched → **case 1**. If it lists
migrations, open them and look at what `Up()` does: only `AddColumn`,
`CreateTable`, `CreateIndex` → the old code can still run on the new schema, so
**case 1** is usually enough. Anything that drops, renames or tightens a column
the old code writes to → **case 2**. If the data itself is wrong — a migration
mangled rows, or a bug wrote garbage — → **case 3**.

### Case 1 — bad code, schema is fine

```bash
cd /home/ubuntu/smart-pet-care-api
bash scripts/rollback.sh --last          # or a specific sha
```

It dumps first, resets the checkout, rebuilds and prints the migrations that
stay applied. Afterwards **revert the commit on `main` as well** — `main` still
points at the broken release, and the next push redeploys it.

### Case 2 — the migration has to come off

The schema does not travel with the code, and the down SQL has to be generated
from the *new* code (it is the one that knows the migration). From a dev
machine, with the two migration ids from the release manifest:

```bash
# FROM the migration that is applied now, TO the one you want to end at
dotnet ef migrations script 20260906102925_AddActivityAndSleepLogUpdatedAt \
                            20260902072056_AddActivityTypeIntensityDurationAndSleepLogs \
                            -o rollback.sql
```

Read the SQL before it goes anywhere near production. `Down()` is generated
code that nothing ever exercised, and a `DROP COLUMN` in it destroys the data
in that column for good — if that column holds anything users typed, do case 3
instead. The script also removes the migration's row from
`__EFMigrationsHistory`, which is what lets the old build start cleanly.

Then, on the server:

```bash
scp rollback.sql ubuntu@<host>:/home/ubuntu/smart-pet-care-api/backups/
cd /home/ubuntu/smart-pet-care-api
bash scripts/backup-db.sh pre-schema-rollback
docker compose stop api
docker compose exec -T db psql -U postgres -d smartPetCareDb -v ON_ERROR_STOP=1 -f /backups/rollback.sql
bash scripts/rollback.sh --last          # code, and it brings api back up
```

`api` is stopped first because it would otherwise be serving requests against a
half-reverted schema.

### Case 3 — the data is wrong, restore the dump

```bash
cd /home/ubuntu/smart-pet-care-api
ls -lt backups/*.dump | head

# 1. Code first: the build that comes up must match the schema in the dump,
#    or its Migrate() will re-apply the migration you are undoing.
bash scripts/rollback.sh --last

# 2. Then the data.
bash scripts/restore-db.sh smartPetCareDb-<stamp>-predeploy-<sha>.dump

# 3. restore-db.sh leaves api stopped on purpose. Start it once the two agree.
docker compose up -d --build api
docker compose logs -f api
```

**Everything written after that dump is gone.** With a daily schedule plus one
dump per deploy, that is up to 24 hours of user data — chat messages, logs,
reminders ticked off. Restoring is the last resort, not the first move; if only
a handful of rows are wrong, fix the rows.

If the host itself is gone, fetch the dump from S3 first
(`aws s3 ls s3://$BACKUP_S3_BUCKET/`), bring the stack up on the new host, then
restore.

## Keeping rollback cheap

Most of the work of a rollback is decided when the migration is written:

- **Expand, then contract.** Add the new column, deploy, backfill, let the code
  use it — and only drop the old one a release later, once nothing has to be
  rolled back past it. A release that adds and removes in one step has no
  case 1.
- **No destructive migration in the same release as the feature that needs it.**
- **Nullable first.** A `NOT NULL` column with no default is the classic reason
  an otherwise fine code rollback fails: the old build does not know to fill it.
- **Check `Down()` exists and is sane** for anything beyond an additive change.
  It is generated, not reviewed, unless someone reviews it.
- Destroying the volume (`docker compose down -v`) deletes the database. The
  deploy uses a plain `down` for a reason; never add `-v` to it.
