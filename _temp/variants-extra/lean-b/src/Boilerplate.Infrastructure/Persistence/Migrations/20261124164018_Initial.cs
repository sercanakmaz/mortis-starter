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
            name: "Categories",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UpdatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Categories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                CompletedOrderCount = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UpdatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Stock = table.Column<int>(type: "integer", nullable: false),
                CategoryId = table.Column<int>(type: "integer", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UpdatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.UniqueConstraint("AK_Products_Sku", x => x.Sku);
                table.CheckConstraint("CK_Products_StockNotNegative", "\"Stock\" >= 0");
                table.ForeignKey(
                    name: "FK_Products_Categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "Categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrderNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CustomerId = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UpdatedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
                table.ForeignKey(
                    name: "FK_Orders_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OrderLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<int>(type: "integer", nullable: false),
                Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                PriceAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                PriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrderLines_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_OrderLines_Products_Sku",
                    column: x => x.Sku,
                    principalTable: "Products",
                    principalColumn: "Sku",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Categories_Name",
            table: "Categories",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Email",
            table: "Customers",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrderLines_OrderId",
            table: "OrderLines",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "IX_OrderLines_Sku",
            table: "OrderLines",
            column: "Sku");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_CustomerId",
            table: "Orders",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_OrderNumber",
            table: "Orders",
            column: "OrderNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Products_CategoryId",
            table: "Products",
            column: "CategoryId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrderLines");

        migrationBuilder.DropTable(
            name: "Orders");

        migrationBuilder.DropTable(
            name: "Products");

        migrationBuilder.DropTable(
            name: "Customers");

        migrationBuilder.DropTable(
            name: "Categories");
    }
}
