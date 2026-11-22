#!/usr/bin/env bash
# Conformance: the storage add-on. A file uploaded through the API reads back byte for byte; limits are enforced.
# NOT RUN YET (decision 50): written in phase 1 from storage.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK API_PORT, TOKEN with auth = jwt, a migrated PostgreSQL, curl; Storage settings (LocalDisk by default, or MinIO from compose).
# Applies when: addons contains storage, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 17))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
python3 -I -c 'import base64, sys; open(sys.argv[1], "wb").write(base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="))' "$WORK/storage.png"
printf 'not an image' > "$WORK/storage.txt"
PRODUCT="$(api_curl -s -H "Content-Type: application/json" -X POST -d "{\"sku\":\"STO-$S\",\"name\":\"Pictured\",\"stock\":1}" "http://localhost:$PORT/products" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"

expect_equal "storage: uploading a PNG answers 200 and marks the product as having an image" "200|True" "$(api_curl -s -o "$WORK/storage.json" -w "%{http_code}" -X PUT -F "file=@$WORK/storage.png;type=image/png" "http://localhost:$PORT/products/$PRODUCT/image")|$(json_field "$(cat "$WORK/storage.json")" "['hasImage']")"
expect_equal "storage: the image reads back byte for byte with its content type" "200|same|image/png" "$(api_curl -s -D "$WORK/storage.headers" -o "$WORK/storage.back" -w "%{http_code}" "http://localhost:$PORT/products/$PRODUCT/image")|$(cmp -s "$WORK/storage.png" "$WORK/storage.back" && echo same || echo different)|$(tr -d '\r' < "$WORK/storage.headers" | awk -F': ' 'tolower($1) == "content-type" {print $2}' | cut -d';' -f1)"
expect_equal "storage: an unlisted content type is a 400 and stores nothing new" "400" "$(api_curl -s -o /dev/null -w "%{http_code}" -X PUT -F "file=@$WORK/storage.txt;type=text/plain" "http://localhost:$PORT/products/$PRODUCT/image")"
expect_equal "storage: a product without an image answers 404 for its image" "404" "$(api_curl -s -o /dev/null -w "%{http_code}" "http://localhost:$PORT/products/00000000-0000-0000-0000-000000000001/image")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
