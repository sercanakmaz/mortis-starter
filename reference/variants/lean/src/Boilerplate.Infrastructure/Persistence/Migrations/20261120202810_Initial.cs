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
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                completed_order_count = table.Column<int>(type: "integer", nullable: false),
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
            });

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                stock = table.Column<int>(type: "integer", nullable: false),
                category_id = table.Column<int>(type: "integer", nullable: true),
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
                table.UniqueConstraint("ak_product_sku", x => x.sku);
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
                    name: "fk_orders_customers_customer_id",
                    column: x => x.customer_id,
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_lines",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    name: "fk_order_lines_product_sku",
                    column: x => x.sku,
                    principalTable: "products",
                    principalColumn: "sku",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_categories_name",
            table: "categories",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_customers_email",
            table: "customers",
            column: "email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_order_lines_order_id",
            table: "order_lines",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_lines_sku",
            table: "order_lines",
            column: "sku");

        migrationBuilder.CreateIndex(
            name: "ix_orders_customer_id",
            table: "orders",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "ix_orders_order_number",
            table: "orders",
            column: "order_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_products_category_id",
            table: "products",
            column: "category_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_lines");

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
