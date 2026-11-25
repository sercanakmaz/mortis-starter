package main

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"strings"

	"delta/internal/bootstrap"
	"delta/internal/infrastructure/persistence"
	"delta/internal/shared"
)

const usage = "Usage: go run ./cmd/console -- --user <id> [--email <email>] <Command> [json | @file.json]   |   migrate"

func run(ctx context.Context, args []string, stdout io.Writer, stderr io.Writer, newApp func(context.Context) (*bootstrap.App, error)) int {
	arguments, err := Parse(args)
	if err != nil {
		writeError(stderr, err)

		return 2
	}

	if arguments.Name == "" {
		fmt.Fprintln(stderr, usage)

		return 1
	}

	app, err := newApp(ctx)
	if err != nil {
		fmt.Fprintln(stderr, err)

		return 1
	}
	defer app.Database.Pool.Close()

	if strings.EqualFold(arguments.Name, MigrateCommand) {
		applied, err := persistence.Migrate(ctx, app.Database.Pool)
		if err != nil {
			fmt.Fprintln(stderr, err)

			return 1
		}

		fmt.Fprintf(stdout, "Applied %d migration(s).\nDone.\n", len(applied))

		return 0
	}

	command, ok := app.Dispatcher.New(arguments.Name)
	if !ok {
		writeError(stderr, shared.InvalidParameter("command", fmt.Sprintf("no command is named %s", arguments.Name)))

		return 2
	}

	if arguments.UserID == "" {
		writeError(stderr, shared.InvalidParameter("--user", "every run names its user"))

		return 2
	}

	if err := decode(arguments.JSON, command); err != nil {
		writeError(stderr, shared.InvalidParameter("json", err.Error()))

		return 2
	}

	if contextual, ok := command.(shared.Contextual); ok {
		*contextual.CommandContext() = shared.CommandContext{User: shared.AuditUser{ID: arguments.UserID, Email: arguments.Email}, TenantID: arguments.TenantID}
	}

	result, err := app.Dispatcher.Dispatch(ctx, command)
	if err != nil {
		writeError(stderr, err)

		if shared.AsError(err) != nil {
			return 2
		}

		return 1
	}

	output, _ := json.Marshal(result)
	fmt.Fprintln(stdout, string(output))

	return 0
}

func decode(text string, command any) error {
	if strings.HasPrefix(text, "@") {
		content, err := os.ReadFile(text[1:])
		if err != nil {
			return err
		}

		text = string(content)
	}

	if strings.TrimSpace(text) == "" {
		text = "{}"
	}

	return json.Unmarshal([]byte(text), command)
}

func writeError(stderr io.Writer, err error) {
	failure := shared.AsError(err)
	if failure == nil {
		failure = &shared.Error{Code: "unexpected_error", Message: err.Error()}
	}

	line, _ := json.Marshal(map[string]any{"code": failure.Code, "message": failure.Message, "errors": failure.Fields})
	fmt.Fprintln(stderr, string(line))
}
