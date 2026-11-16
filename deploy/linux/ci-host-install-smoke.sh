#!/usr/bin/env bash
set -euo pipefail

api_output="${1:?Pass the published API directory}"
store_output="${2:?Pass the built storefront directory}"
host="shop.example.test"
origin="https://${host}"
database_password="onecart-ci-only"
setup_token="$(openssl rand -hex 32)"
certificate_dir="$RUNNER_TEMP/onecart-ci-tls"

on_exit() {
  result=$?
  if [ "$result" -ne 0 ]; then
    sudo journalctl -u onecart-api.service --no-pager -n 100 || true
    sudo nginx -t || true
  fi
  exit "$result"
}
trap on_exit EXIT

sudo useradd --system --user-group --home-dir /var/lib/onecart --shell /usr/sbin/nologin onecart
sudo install -d -o root -g onecart -m 0750 /opt/onecart/api
sudo install -d -o onecart -g onecart -m 0750 /var/lib/onecart/product-images /var/lib/onecart/data-protection-keys
sudo install -d -o root -g onecart -m 0750 /etc/onecart
sudo install -d -o root -g root -m 0755 /var/www/onecart
sudo cp -a "$api_output"/. /opt/onecart/api/
sudo cp -a "$store_output"/. /var/www/onecart/

sudo tee /etc/onecart/api.env >/dev/null <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:8080
Database__Provider=PostgreSql
ConnectionStrings__Store=Host=127.0.0.1;Port=5432;Database=onecart;Username=onecart_test;Password=${database_password}
Cors__AllowedOrigins__0=${origin}
ForwardedHeaders__KnownProxies__0=127.0.0.1
DataProtection__KeyDirectory=/var/lib/onecart/data-protection-keys
ProductMedia__RootPath=/var/lib/onecart/product-images
Setup__BootstrapTokenFile=/etc/onecart/setup-token
EOF
sudo chown root:onecart /etc/onecart/api.env
sudo chmod 0640 /etc/onecart/api.env
printf '%s' "$setup_token" | sudo tee /etc/onecart/setup-token >/dev/null
sudo chown root:onecart /etc/onecart/setup-token
sudo chmod 0640 /etc/onecart/setup-token

dotnet_path="$(command -v dotnet)"
sudo -u onecart env \
  ASPNETCORE_ENVIRONMENT=Production \
  ASPNETCORE_URLS=http://127.0.0.1:8080 \
  Database__Provider=PostgreSql \
  "ConnectionStrings__Store=Host=127.0.0.1;Port=5432;Database=onecart;Username=onecart_test;Password=${database_password}" \
  "Cors__AllowedOrigins__0=${origin}" \
  ForwardedHeaders__KnownProxies__0=127.0.0.1 \
  DataProtection__KeyDirectory=/var/lib/onecart/data-protection-keys \
  ProductMedia__RootPath=/var/lib/onecart/product-images \
  Setup__BootstrapTokenFile=/etc/onecart/setup-token \
  "$dotnet_path" /opt/onecart/api/OneCart.Api.dll --migrate

sudo install -o root -g root -m 0644 deploy/linux/onecart-api.service /etc/systemd/system/onecart-api.service
sudo systemctl daemon-reload
sudo systemctl enable onecart-api.service
sudo systemctl start onecart-api.service

sudo install -d -m 0755 "$certificate_dir"
openssl req -x509 -newkey rsa:2048 -nodes \
  -keyout "$certificate_dir/key.pem" \
  -out "$certificate_dir/fullchain.pem" \
  -subj "/CN=${host}" -addext "subjectAltName=DNS:${host}" \
  -addext 'basicConstraints=critical,CA:TRUE' -days 1
sudo chmod 0644 "$certificate_dir/fullchain.pem"
sudo chmod 0600 "$certificate_dir/key.pem"

sudo sed \
  -e "s#/etc/letsencrypt/live/shop.example.com/fullchain.pem#${certificate_dir}/fullchain.pem#g" \
  -e "s#/etc/letsencrypt/live/shop.example.com/privkey.pem#${certificate_dir}/key.pem#g" \
  -e "s#shop.example.com#${host}#g" \
  deploy/linux/nginx-onecart.conf | sudo tee /etc/nginx/sites-available/onecart >/dev/null
sudo rm -f /etc/nginx/sites-enabled/default
sudo ln -sfn /etc/nginx/sites-available/onecart /etc/nginx/sites-enabled/onecart
sudo nginx -t
sudo systemctl restart nginx

request() {
  curl --silent --show-error --cacert "$certificate_dir/fullchain.pem" \
    --connect-to "${host}:443:127.0.0.1:443" "$@"
}

health=''
for attempt in $(seq 1 30); do
  if health="$(request "$origin/health" 2>/dev/null)"; then break; fi
  sleep 1
done
printf '%s' "$health" | jq -e '.status == "ok" and .service == "OneCart.Api"' >/dev/null
sudo systemctl is-active --quiet onecart-api.service

http_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --connect-to "${host}:80:127.0.0.1:80" "http://${host}/health")"
test "$http_status" = 301
internal_http_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  http://127.0.0.1:8080/health)"
test "$internal_http_status" = 400

install_page="$(request "$origin/install")"
printf '%s' "$install_page" | grep -q 'id="root"'
setup_status="$(request "$origin/api/v1/setup/status")"
printf '%s' "$setup_status" | jq -e '.available == true' >/dev/null

setup_body='{"email":"owner@example.test","password":"OneCart-CI-Bootstrap-2026!","confirmPassword":"OneCart-CI-Bootstrap-2026!"}'
invalid_token_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --cacert "$certificate_dir/fullchain.pem" --connect-to "${host}:443:127.0.0.1:443" \
  -H "Origin: ${origin}" -H 'X-OneCart-Setup-Token: invalid-token' -H 'Content-Type: application/json' \
  --data "$setup_body" "${origin}/api/v1/setup/system-admin")"
test "$invalid_token_status" = 403

setup_result="$(request -H "Origin: ${origin}" -H "X-OneCart-Setup-Token: ${setup_token}" \
  -H 'Content-Type: application/json' --data "$setup_body" "${origin}/api/v1/setup/system-admin")"
printf '%s' "$setup_result" | jq -e '.role == "SystemAdmin"' >/dev/null

setup_status="$(request "$origin/api/v1/setup/status")"
printf '%s' "$setup_status" | jq -e '.available == false' >/dev/null
protection_key="$(sudo find /var/lib/onecart/data-protection-keys -maxdepth 1 -name '*.xml' -print -quit)"
test -n "$protection_key"
sudo -u onecart test -s "$protection_key"
sudo -u onecart test -w /var/lib/onecart/product-images

echo 'Linux systemd/Nginx HTTPS fresh-install rehearsal passed.'
