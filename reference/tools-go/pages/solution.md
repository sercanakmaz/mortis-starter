# The module and the root files

Go 1.24 or later (the reference solution's `go.mod` asks for 1.25.0 because `pgx` 5.13 does; `GOTOOLCHAIN=auto` fetches it). Three dependencies:

```bash
go mod init {module}
go get github.com/google/uuid@v1.6.0 github.com/jackc/pgx/v5@v5.13.0 github.com/shopspring/decimal@v1.6.0
go mod tidy
```

Until the code imports them, `go get` records them as indirect and leaves `go.sum` incomplete; `go mod tidy` at step 7 settles both. After step 7, `go.mod` reads:

@@go go.mod@@

The root files, written as shown.

`.gitignore`:

@@go .gitignore@@

`docker-compose.yml`:

@@go docker-compose.yml@@

`README.md`:

@@go README.md@@

`AGENTS.md`:

@@go AGENTS.md@@

Copy `MORTIS.md` into the root unchanged. `go.sum` is written by `go mod tidy`, never by hand.
