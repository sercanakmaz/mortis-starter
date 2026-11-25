package main

import (
	"context"
	"os"

	"delta/internal/bootstrap"
)

func main() {
	os.Exit(run(context.Background(), os.Args[1:], os.Stdout, os.Stderr, bootstrap.New))
}
