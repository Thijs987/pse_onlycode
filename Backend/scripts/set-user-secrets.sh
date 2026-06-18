#!/usr/bin/env bash
set -euo pipefail
# Usage: ./set-user-secrets.sh [path/to/secrets.env]
# If no file is provided, the script uses the workspace root .env file.
# The file format is KEY=VALUE and supports comments beginning with #.

PROJ_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../src" && pwd)"
CSProj="$PROJ_DIR/CodeGreen.csproj"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEFAULT_ENV_FILE="$ROOT_DIR/.env"

trim() {
  local var="$*"
  var="${var#${var%%[![:space:]]*}}"
  var="${var%${var##*[![:space:]]}}"
  printf '%s' "$var"
}

if [ ! -f "$CSProj" ]; then
  echo "Error: project file not found at $CSProj" >&2
  exit 1
fi

cd "$PROJ_DIR"

if ! grep -q "<UserSecretsId>" "$CSProj"; then
  echo "Initializing user-secrets (will update $CSProj)..."
  dotnet user-secrets init
fi

SECRETS_FILE="${1:-$DEFAULT_ENV_FILE}"
if [ ! -f "$SECRETS_FILE" ]; then
  echo "Error: secrets file not found: $SECRETS_FILE" >&2
  exit 1
fi

mapfile -t LINES < "$SECRETS_FILE"

COUNT=0
for raw in "${LINES[@]}"; do
  line="${raw%%#*}"
  line="$(trim "$line")"
  [ -z "$line" ] && continue

  if [[ "$line" != *=* ]]; then
    echo "Skipping invalid line: $line" >&2
    continue
  fi

  key="$(trim "${line%%=*}")"
  value="$(trim "${line#*=}")"

  if [[ ${value:0:1} == "'" && ${value: -1} == "'" && ${#value} -ge 2 ]]; then
    value="${value:1:-1}"
  elif [[ ${value:0:1} == '"' && ${value: -1} == '"' && ${#value} -ge 2 ]]; then
    value="${value:1:-1}"
  fi

  secretKey="${key//__/":"}"
  echo "Setting: $secretKey"
  dotnet user-secrets set "$secretKey" "$value"
  COUNT=$((COUNT+1))
done

echo "Done. Set $COUNT secrets for project $CSProj"
