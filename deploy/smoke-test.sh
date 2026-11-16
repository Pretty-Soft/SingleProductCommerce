#!/bin/sh
set -eu

origin="https://${ONECART_DOMAIN}"
connect_to="${ONECART_DOMAIN}:443:proxy:443"
mode="${1:-full}"

request() {
    curl --silent --show-error --fail --insecure --connect-to "$connect_to" "$@"
}

health=''
attempt=0
while [ "$attempt" -lt 60 ]; do
    if health="$(request "$origin/health" 2>/dev/null)"; then
        break
    fi
    attempt=$((attempt + 1))
    sleep 2
done
printf '%s' "$health" | jq -e '.status == "ok"' >/dev/null

if [ "$mode" = "health" ]; then
    exit 0
fi

install_page="$(request "$origin/install")"
printf '%s' "$install_page" | grep -q 'id="root"'
available="$(request "$origin/api/v1/setup/status")"
printf '%s' "$available" | jq -e '.available == true' >/dev/null

setup_token="${ONECART_BOOTSTRAP_TOKEN:?Set ONECART_BOOTSTRAP_TOKEN for the install smoke test}"
setup_body='{"email":"owner@example.test","password":"OneCart-CI-Bootstrap-2026!","confirmPassword":"OneCart-CI-Bootstrap-2026!"}'

invalid_token_status="$(curl --silent --output /dev/null --write-out '%{http_code}' --insecure --connect-to "$connect_to" \
    -H "Origin: $origin" -H 'X-OneCart-Setup-Token: invalid-token' -H 'Content-Type: application/json' \
    --data "$setup_body" "$origin/api/v1/setup/system-admin")"
test "$invalid_token_status" = 403

setup_result="$(request -H "Origin: $origin" -H "X-OneCart-Setup-Token: $setup_token" \
    -H 'Content-Type: application/json' --data "$setup_body" "$origin/api/v1/setup/system-admin")"
printf '%s' "$setup_result" | jq -e '.role == "SystemAdmin"' >/dev/null

completed="$(request "$origin/api/v1/setup/status")"
printf '%s' "$completed" | jq -e '.available == false' >/dev/null

forbidden_status="$(curl --silent --output /dev/null --write-out '%{http_code}' --insecure --connect-to "$connect_to" \
    -H 'Origin: https://attacker.example.test' -H "X-OneCart-Setup-Token: $setup_token" \
    -H 'Content-Type: application/json' --data "$setup_body" "$origin/api/v1/setup/system-admin")"
test "$forbidden_status" = 403
