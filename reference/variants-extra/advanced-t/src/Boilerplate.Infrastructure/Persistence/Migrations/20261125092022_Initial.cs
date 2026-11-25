using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Boilerplate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                updated_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_categories", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                completed_order_count = table.Column<int>(type: "integer", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                tax_number = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                updated_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customers", x => x.id);
                table.UniqueConstraint("ak_customer_tenant_id_id", x => new { x.tenant_id, x.id });
            });

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            columns: table => new
            {
                scope = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status_code = table.Column<int>(type: "integer", nullable: true),
                location = table.Column<string>(type: "text", nullable: true),
                response_body = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_records", x => new { x.scope, x.key });
            });

        migrationBuilder.CreateTable(
            name: "order_summaries",
            columns: table => new
            {
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                order_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                total_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                line_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_summaries", x => x.order_id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_error = table.Column<string>(type: "text", nullable: true),
                trace_parent = table.Column<string>(type: "character varying(55)", maxLength: 55, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "scheduled_job_runs",
            columns: table => new
            {
                job = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                tick = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_scheduled_job_runs", x => new { x.job, x.tick });
            });

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "jsonb", nullable: false),
                stock = table.Column<int>(type: "integer", nullable: false),
                category_id = table.Column<int>(type: "integer", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_user_id = table.Column<string>(type: "text", nullable: true),
                name_search = table.Column<string>(type: "text", nullable: true, computedColumnSql: "name::text", stored: true),
                name_sort = table.Column<string>(type: "text", nullable: true, computedColumnSql: "name ->> 'en'", stored: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                updated_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_products", x => x.id);
                table.UniqueConstraint("ak_product_tenant_id_sku", x => new { x.tenant_id, x.sku });
                table.CheckConstraint("ck_products_stock_not_negative", "stock >= 0");
                table.ForeignKey(
                    name: "fk_products_categories_category_id",
                    column: x => x.category_id,
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                order_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                updated_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_orders", x => x.id);
                table.ForeignKey(
                    name: "fk_orders_customers_tenant_id_customer_id",
                    columns: x => new { x.tenant_id, x.customer_id },
                    principalTable: "customers",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_lines",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                price_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_lines", x => x.id);
                table.ForeignKey(
                    name: "fk_order_lines_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_order_lines_product_tenant_id_sku",
                    columns: x => new { x.tenant_id, x.sku },
                    principalTable: "products",
                    principalColumns: new[] { "tenant_id", "sku" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_categories_name",
            table: "categories",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_customers_tenant_id_email",
            table: "customers",
            columns: new[] { "tenant_id", "email" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_started_at",
            table: "idempotency_records",
            column: "started_at");

        migrationBuilder.CreateIndex(
            name: "ix_order_lines_order_id",
            table: "order_lines",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_lines_tenant_id_sku",
            table: "order_lines",
            columns: new[] { "tenant_id", "sku" });

        migrationBuilder.CreateIndex(
            name: "ix_order_summaries_tenant_id_status",
            table: "order_summaries",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_orders_tenant_id_customer_id",
            table: "orders",
            columns: new[] { "tenant_id", "customer_id" });

        migrationBuilder.CreateIndex(
            name: "ix_orders_tenant_id_order_number",
            table: "orders",
            columns: new[] { "tenant_id", "order_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_next_attempt_at",
            table: "outbox_messages",
            column: "next_attempt_at");

        migrationBuilder.CreateIndex(
            name: "ix_products_category_id",
            table: "products",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "ix_products_is_deleted",
            table: "products",
            column: "is_deleted");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "idempotency_records");

        migrationBuilder.DropTable(
            name: "order_lines");

        migrationBuilder.DropTable(
            name: "order_summaries");

        migrationBuilder.DropTable(
            name: "outbox_messages");

        migrationBuilder.DropTable(
            name: "scheduled_job_runs");

        migrationBuilder.DropTable(
            name: "orders");

        migrationBuilder.DropTable(
            name: "products");

        migrationBuilder.DropTable(
            name: "customers");

        migrationBuilder.DropTable(
            name: "categories");
    }
}
