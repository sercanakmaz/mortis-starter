package main

import "delta/internal/shared"

// MigrateCommand applies the migrations instead of running a command.
const MigrateCommand = "migrate"

// Arguments are a Console run's options, the command's name and its JSON.
type Arguments struct {
	UserID   string
	Email    string
	TenantID string
	Name     string
	JSON     string
}

// Parse reads --user, --email and --tenant before the command's name, then the name and its JSON.
func Parse(args []string) (Arguments, error) {
	arguments := Arguments{}
	positional := []string{}

	for index := 0; index < len(args); index++ {
		switch args[index] {
		case "--":
			continue
		case "--user", "--email", "--tenant":
			if index+1 >= len(args) {
				return arguments, shared.InvalidParameter(args[index], "a value must follow it")
			}

			value := args[index+1]
			index++

			switch args[index-1] {
			case "--user":
				arguments.UserID = value
			case "--email":
				arguments.Email = value
			default:
				arguments.TenantID = value
			}
		default:
			positional = append(positional, args[index])
		}
	}

	if len(positional) > 0 {
		arguments.Name = positional[0]
	}

	if len(positional) > 1 {
		arguments.JSON = positional[1]
	}

	return arguments, nil
}
