#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
compose_file="$repo_root/deploy/compose.yaml"
env_file="$repo_root/deploy/.env"
timestamp="$(date -u +%Y%m%d%H%M%S)"
backup_dir="${CRATEBASE_RESTORE_DRILL_DIR:-$repo_root/artifacts/restore-drills/$timestamp}"
source_project="${CRATEBASE_RESTORE_DRILL_SOURCE_PROJECT:-cratebase_backup_source}"
target_project="${CRATEBASE_RESTORE_DRILL_TARGET_PROJECT:-cratebase_restore_drill_$timestamp}"

mkdir -p "$backup_dir"

wait_for_postgres() {
  local project="$1"
  local label="$2"

  echo "Waiting for PostgreSQL in $label"
  for _ in {1..30}; do
    if docker compose --env-file "$env_file" -p "$project" -f "$compose_file" exec -T postgres \
      pg_isready -h 127.0.0.1 -U cratebase -d cratebase >/dev/null 2>&1; then
      return 0
    fi

    sleep 1
  done

  echo "PostgreSQL did not become ready in $label" >&2
  return 1
}

if [[ ! -f "$env_file" ]]; then
  cp "$repo_root/deploy/.env.example" "$env_file"
fi

echo "Starting restore drill source compose project: $source_project"
docker compose --env-file "$env_file" -p "$source_project" -f "$compose_file" up -d postgres
wait_for_postgres "$source_project" "source project"

echo "Creating PostgreSQL backup artifact"
docker compose --env-file "$env_file" -p "$source_project" -f "$compose_file" exec -T postgres \
  pg_dump -h 127.0.0.1 -U cratebase -d cratebase -Fc > "$backup_dir/postgres.dump"

echo "Creating service-storage backup artifact"
docker run --rm \
  -v "${source_project}_cratebase-service-data:/data:ro" \
  -v "$backup_dir:/backup" \
  alpine:3.20 \
  tar -czf /backup/service-storage.tgz -C /data .

echo "Starting isolated restore drill target compose project: $target_project"
docker compose --env-file "$env_file" -p "$target_project" -f "$compose_file" up -d postgres
wait_for_postgres "$target_project" "target project"

echo "Restoring PostgreSQL backup into drill target"
docker compose --env-file "$env_file" -p "$target_project" -f "$compose_file" exec -T postgres \
  pg_restore --clean --if-exists --no-owner -h 127.0.0.1 -U cratebase -d cratebase < "$backup_dir/postgres.dump"

echo "Restoring service-storage backup into drill target"
docker run --rm \
  -v "${target_project}_cratebase-service-data:/data" \
  -v "$backup_dir:/backup" \
  alpine:3.20 \
  sh -c "rm -rf /data/* && tar -xzf /backup/service-storage.tgz -C /data"

echo "Verifying restored database"
docker compose --env-file "$env_file" -p "$target_project" -f "$compose_file" exec -T postgres \
  psql -h 127.0.0.1 -U cratebase -d cratebase -v ON_ERROR_STOP=1 \
  -c "select current_database() as restored_database;" \
  -c "select count(*) as public_tables from information_schema.tables where table_schema = 'public';"

cat <<SUMMARY
Hosted restore drill completed.
Backup artifacts: $backup_dir
Target compose project: $target_project

Inspect or remove the drill target with:
  docker compose --env-file "$env_file" -p "$target_project" -f "$compose_file" down -v
SUMMARY
