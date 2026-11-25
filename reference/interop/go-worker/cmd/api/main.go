package main

import (
	"context"
	"fmt"
	"net/http"
	"os"

	"boilerplate/internal/api"
	"boilerplate/internal/bootstrap"
)

// AddressVariable names the environment variable that holds the address to listen on.
const AddressVariable = "MORTIS_API_ADDRESS"

func main() {
	app, err := bootstrap.New(context.Background())
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	defer app.Database.Pool.Close()

	address := os.Getenv(AddressVariable)
	if address == "" {
		address = "localhost:5000"
	}

	app.Logger.Info("Listening on " + address)

	if err := http.ListenAndServe(address, api.Routes(app.Dispatcher, app.Logger)); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}
