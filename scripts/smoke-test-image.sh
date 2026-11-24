#!/usr/bin/env bash
# Smoke-test the nginx image: it serves the site, every response type carries the security
# headers (nginx drops server-level add_header in locations that set their own), and missing
# pages return 404. Usage: scripts/smoke-test-image.sh [image]  (expects ./output to be built)
set -euo pipefail
image=${1:-staticwgen:smoke}
port=${PORT:-8080}

docker build -q -t "$image" . >/dev/null
cid=$(docker run -d -p "$port:80" "$image")
trap 'docker rm -f "$cid" >/dev/null' EXIT

for _ in $(seq 1 20); do curl -fs "localhost:$port/" >/dev/null && break; sleep 1; done

css=$(cd output && find . -name '*.css' | head -1 | sed 's#^\.##')
for path in / "$css" /feed.xml; do
  headers=$(curl -fsSI "localhost:$port$path") || { echo "FAIL: GET $path"; exit 1; }
  for want in Content-Security-Policy X-Content-Type-Options X-Frame-Options Referrer-Policy; do
    grep -qi "^$want:" <<<"$headers" || { echo "FAIL: $path is missing $want"; exit 1; }
  done
  echo "ok: $path"
done

code=$(curl -s -o /dev/null -w '%{http_code}' "localhost:$port/does-not-exist")
[ "$code" = 404 ] || { echo "FAIL: missing page returned $code, expected 404"; exit 1; }
echo "ok: 404"
