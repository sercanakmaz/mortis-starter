package persistence

import "testing"

func TestConnectionString(t *testing.T) {
	cases := map[string]string{
		"Host=db;Port=5432;Database=shop;Username=app;Password=secret": "host='db' port='5432' dbname='shop' user='app' password='secret'",
		"postgres://app:secret@db:5432/shop":                           "postgres://app:secret@db:5432/shop",
	}

	for given, expected := range cases {
		if actual := ConnectionString(given); actual != expected {
			t.Fatalf("%s: got %s", given, actual)
		}
	}
}
