package main

import "testing"

func TestParse_ReadsTheOptionsBeforeTheNameAndJSON(t *testing.T) {
	arguments, err := Parse([]string{"--", "--user", "u-1", "--email", "u-1@localhost", "CreateProduct", `{"sku":"A"}`})

	if err != nil || arguments.UserID != "u-1" || arguments.Email != "u-1@localhost" || arguments.Name != "CreateProduct" || arguments.JSON != `{"sku":"A"}` {
		t.Fatalf("got %+v, %v", arguments, err)
	}
}

func TestParse_WithAnOptionLackingItsValue_Fails(t *testing.T) {
	if _, err := Parse([]string{"--user"}); err == nil {
		t.Fatal("no error")
	}
}
