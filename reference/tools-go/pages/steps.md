# Generating a Go project

**Built** for the lean preset in the Go reference solution (decision 54): compiled, tested and verified there by `verify.sh` with `scripts/lang/go.sh`. Go supports only the lean preset's values today: `queries` = `commands`, `events.outbox` = `none`, `auth` = `none`, `exceptions` = `minimal`, `testing` = `unit`, `hosts.worker` = `none`, `keys` = `guid`, `database` = `postgres`, no add-ons, no tenancy, no translated messages. If `MORTIS.md` asks for anything else with `language` = `go`, stop and say which value Go does not support yet.

`{Name}` is the `name` row; `{module}` is the Go module path, the name in lower case (`Acme` gives `acme`). Every Go package path starts with `{module}/`.

Follow these steps in order; each ends with its check. Write every file with its absolute path under the project folder: a relative path after a failed `cd` lands elsewhere. Imports are grouped as `gofmt` and the snippets show: the standard library, then the project's packages, then third-party ones, each group separated by a blank line. Write files from the pages' code: each page shows every file of the reference solution in full, with the names replaced. Without the example (`example` = `false`), leave out every file under `internal/domain/<aggregate>`, `internal/application/<aggregate>`, `internal/infrastructure/persistence/<aggregate>`, the first migration, `internal/testsupport/repositories.go` and `internal/application/flow_test.go`, and register nothing in `internal/application/module.go`'s `Register` but keep the function.

| Step | Do | Page | Check |
|---|---|---|---|
| 1 | `go mod init {module}`, the root files, `go get` the three dependencies | `solution.md` | `go.mod` lists them |
| 2 | The kernel, `internal/shared` | `shared-kernel.md` | `go build ./...` |
| 3 | The example's aggregates, `internal/domain` | `domain.md` | `go build ./...` |
| 4 | The example's commands and handlers, `internal/application` | `application.md` | `go build ./...` |
| 5 | Persistence, logging and the in-process publisher | `persistence.md`, `logging.md`, `events-inprocess.md` | `go build ./... && go vet ./...` |
| 6 | The hosts: `internal/bootstrap`, `internal/api`, `cmd/api`, `cmd/console`, `.vscode/launch.json` | `hosts.md` | `go build ./cmd/...` |
| 7 | Test support and the tests; then `go mod tidy`, which completes `go.sum` now that the code imports every package | `testing.md` | `go mod tidy && go test ./...` |
| 8 | `gofmt -w .`, then `bash <skill>/scripts/verify.sh <project>` | | every check passes |

`naming.md` has every name. `manifest.json` is the file plan: a file it does not list fails the structure check.
