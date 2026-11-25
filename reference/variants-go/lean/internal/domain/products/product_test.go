package products_test

import (
	"testing"

	"boilerplate/internal/domain/products"
	"boilerplate/internal/shared"
)

func TestNew_WithNegativeStock_FailsInvalidParameter(t *testing.T) {
	_, err := products.New(shared.NewID(), "SKU-1", "Bolt", -1, shared.AuditUser{}, nil)

	if failure := shared.AsError(err); failure == nil || failure.Code != "invalid_parameter" {
		t.Fatalf("got %v", err)
	}
}

func TestNew_RaisesCreated(t *testing.T) {
	product, _ := products.New(shared.NewID(), "SKU-1", "Bolt", 1, shared.AuditUser{ID: "user-1"}, nil)

	if created, ok := product.Events()[0].(*products.Created); !ok || created.SKU != "SKU-1" || product.CreatedUserID != "user-1" {
		t.Fatalf("got %+v", product.Events())
	}
}

func TestDecreaseStock(t *testing.T) {
	cases := []struct {
		name     string
		quantity int
		code     string
		stock    int
	}{
		{"takes the units", 3, "", 7},
		{"refuses more than the stock", 11, products.InsufficientStockCode, 10},
		{"refuses zero", 0, "invalid_parameter", 10},
	}

	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			product, _ := products.New(shared.NewID(), "SKU-1", "Bolt", 10, shared.AuditUser{}, nil)
			product.ClearEvents()

			err := product.DecreaseStock(testCase.quantity)

			code := ""
			if failure := shared.AsError(err); failure != nil {
				code = failure.Code
			}

			if code != testCase.code || product.Stock != testCase.stock {
				t.Fatalf("got %q and %d", code, product.Stock)
			}
		})
	}
}
