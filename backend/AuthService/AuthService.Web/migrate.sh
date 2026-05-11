#!/usr/bin/env sh
set -eu

connection="${ConnectionStrings__DefaultConnection:-${AUTH_SERVICE_DB_CONNECTION:-}}"

if [ -z "$connection" ]; then
    echo "AuthService migration connection string is missing. Set ConnectionStrings__DefaultConnection or AUTH_SERVICE_DB_CONNECTION." >&2
    exit 1
fi

export ConnectionStrings__DefaultConnection="$connection"

exec /app/migrations/efbundle --connection "$connection"
