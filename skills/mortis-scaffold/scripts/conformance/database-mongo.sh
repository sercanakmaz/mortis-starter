#!/usr/bin/env bash
# Conformance: database = mongo. The example's flows give the same results as on PostgreSQL, read back from the documents.
# NOT RUN YET (decision 45): written in phase 1 from infrastructure-mongo.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK MONGO_URL (a single-node replica set), mongosh; POSTGRES_TWIN (optional) = a project generated
# from the same MORTIS.md with database = postgres.
# Applies when: database = mongo, example = true
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

mongo() { mongosh "$MONGO_URL" --quiet --eval "$1"; }
eventually_mongo() { local expected="$2" seconds="${3:-10}" value=""; for _ in $(seq 1 $((seconds * 2))); do value="$(mongo "$1")"; [ "$value" = "$expected" ] && break; sleep 0.5; done; printf '%s' "$value"; }
run() { console_run "$@" 2>>"$WORK/console.log"; }
id_of() { json_field "$1" "['Id']"; }
S="$(python3 -I -c 'import uuid; print(uuid.uuid4().hex[:8])')"
: > "$WORK/console.log"

expect_equal "mongo: the server is a replica set (a standalone server fails at the first write in a transaction)" "1" "$(mongo 'try { rs.status().ok } catch (e) { 0 }')"
console_run ensure-indexes >/dev/null 2>"$WORK/indexes.err"
expect_equal "mongo: ensure-indexes creates the unique indexes, and running it again changes nothing" "0|0|ux_products_sku" "$?|$(console_run ensure-indexes >/dev/null 2>&1; echo $?)|$(mongo 'db.products.getIndexes().map(i => i.name).filter(n => n === "ux_products_sku").join()')"

CUSTOMER="$(id_of "$(run CreateCustomer "{\"name\":\"Mongo\",\"email\":\"mongo-$S@example.com\"}")")"
run CreateProduct "{\"sku\":\"MG-$S\",\"name\":\"Mongo bolt\",\"stock\":10}" >/dev/null
ORDER="$(id_of "$(run CreateOrder "{\"orderNumber\":\"MG-$S\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"MG-$S\",\"price\":5,\"currency\":\"EUR\",\"quantity\":3}]}")")"
run CompleteOrder "{\"id\":\"$ORDER\"}" >/dev/null
expect_equal "order-flow: the order document is Completed with its line embedded, and the version moved on" "Completed|1|true" "$(mongo "const o = db.orders.findOne({ orderNumber: 'MG-$S' }); [o.status, o.lines.length, o.version > 1].join('|')")"
expect_equal "order-flow: the sync handler counted the completed order on the customer document" "1" "$(mongo "db.customers.findOne({ email: 'mongo-$S@example.com' }).completedOrderCount")"
expect_equal "stock-flow: the event consumer moved the stock (10 - 3)" "7" "$(eventually_mongo "db.products.findOne({ sku: 'MG-$S' }).stock" "7")"
expect_equal "keys: ids are standard UUIDs (binary subtype 4)" "4" "$(mongo "db.orders.findOne({ orderNumber: 'MG-$S' })._id.sub_type")"
run CreateCustomer "{\"name\":\"Twice\",\"email\":\"mongo-$S@example.com\"}" >/dev/null
expect_equal "duplicates: a second customer with the same email is a duplicate (11000), as on PostgreSQL" "duplicate" "$(error_code "$WORK/console.log")"

BLOCKED="$(id_of "$(run CreateCustomer "{\"name\":\"Blocked\",\"email\":\"mongo-blocked-$S@example.com\"}")")"
BLOCKED_ORDER="$(id_of "$(run CreateOrder "{\"orderNumber\":\"MGB-$S\",\"customerId\":\"$BLOCKED\",\"lines\":[{\"sku\":\"MG-$S\",\"price\":5,\"currency\":\"EUR\",\"quantity\":1}]}")")"
run BlockCustomer "{\"id\":\"$BLOCKED\"}" >/dev/null
console_run CompleteOrder "{\"id\":\"$BLOCKED_ORDER\"}" >/dev/null 2>"$WORK/blocked.err"
expect_equal "blocked-customer: a rolled-back command leaves the order Open, the count unchanged, and no outbox document for Completed" "2|customer_blocked|Open|0|0" "$?|$(error_code "$WORK/blocked.err")|$(mongo "[db.orders.findOne({ orderNumber: 'MGB-$S' }).status, db.customers.findOne({ email: 'mongo-blocked-$S@example.com' }).completedOrderCount, db.outbox_messages.countDocuments({ payload: { \$regex: '$BLOCKED_ORDER' }, type: { \$regex: 'Completed' } })].join('|')")"

run CreateProduct "{\"sku\":\"MGC-$S\",\"name\":\"Contended\",\"stock\":20}" >/dev/null
for i in 1 2 3 4 5 6 7 8; do
  console_run CreateOrder "{\"orderNumber\":\"MGC-$i-$S\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"MGC-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}" >/dev/null 2>&1 &
done
wait
expect_equal "concurrency: eight concurrent stock updates on one document all land (version conflicts retried), 20 - 8" "12" "$(eventually_mongo "db.products.findOne({ sku: 'MGC-$S' }).stock" "12" 60)"

if [ -n "${POSTGRES_TWIN:-}" ]; then
  expect_equal "layers: Domain and Application are identical, file for file, to the PostgreSQL project" "" "$(for layer in Domain Application; do diff -rq -x bin -x obj "$PROJECT/src/$NAME.$layer" "$POSTGRES_TWIN/src/$NAME.$layer"; done)"
else
  echo "SKIP  layers: Domain and Application are identical to the PostgreSQL project (POSTGRES_TWIN is not set)"
fi

MONGO_INT="$WORK/MORTIS-int.md"
sed 's/^| keys.default | guid |/| keys.default | int |/' "$PROJECT/MORTIS.md" > "$MONGO_INT"
python3 -I "$(dirname "${BASH_SOURCE[0]}")/../../../mortis-architect/scripts/validate_decisions.py" "$MONGO_INT" >"$WORK/validate.out" 2>&1
REFUSED=$?
expect_equal "keys: keys.default = int with mongo is refused by the validator (R12)" "refused|yes" "$([ "$REFUSED" -ne 0 ] && echo refused || echo accepted)|$(grep -q 'R12' "$WORK/validate.out" && echo yes || echo no)"

report
[ "$FAILED" -eq 0 ]
