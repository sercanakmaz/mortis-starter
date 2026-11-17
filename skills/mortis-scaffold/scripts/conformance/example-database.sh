#!/usr/bin/env bash
# Conformance: the example domain's schema in PostgreSQL
# Needs: PROJECT NAME LANG_SH WORK DATABASE (and API_PORT for http.sh), a reachable PostgreSQL, psql, curl.
# Applies when: example = true, database = postgres
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

psql -d postgres -qc "DROP DATABASE IF EXISTS $DATABASE" -c "CREATE DATABASE $DATABASE" >/dev/null 2>&1
MIGRATE_LOG="$("$LANG_SH" migrate "$PROJECT" "$NAME" 2>&1 || true)"
case "$MIGRATE_LOG" in *"Done."*) pass "the migrations apply to a fresh database" ;; *) fail "the migrations apply to a fresh database" "$MIGRATE_LOG" ;; esac
expect_equal "tables exist with snake_case names" "customers,order_lines,orders,products" "$(sql "select string_agg(table_name, ',' order by table_name) from information_schema.tables where table_schema = 'public' and table_name <> '__EFMigrationsHistory'")"
expect_equal "orders.customer_id is a real foreign key" "1" "$(sql "select count(*) from information_schema.table_constraints where table_name = 'orders' and constraint_type = 'FOREIGN KEY'")"
expect_equal "order_lines references both orders and products by foreign key" "2" "$(sql "select count(*) from information_schema.table_constraints where table_name = 'order_lines' and constraint_type = 'FOREIGN KEY'")"
expect_equal "products.stock cannot go negative in the database either" "1" "$(sql "select count(*) from information_schema.table_constraints where table_name = 'products' and constraint_name = 'ck_products_stock_not_negative'")"

report
[ "$FAILED" -eq 0 ]
