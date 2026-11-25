# Names in a Go project

| Thing | Name | Where |
|---|---|---|
| Module | `{module}`: the name in lower case | `go.mod` |
| Package of an aggregate | plural, lower case: `products`, `orders` | `internal/domain/{plural}`, `internal/application/{plural}`, `internal/infrastructure/persistence/{plural}` |
| Importing the same aggregate's domain package from application | alias `domain` | `domain "{module}/internal/domain/products"` |
| Aggregate | singular, exported: `Product`; constructor `New` | `internal/domain/{plural}/<aggregate>.go` |
| Child entity | `Line` (named inside its aggregate's package) | `internal/domain/{plural}/line.go` |
| Event | past tense: `Created`, `StockDecreased`; wire name `{Name}.Domain.Aggregates.<Plural>.Events:<Event>`, the same as the C# projects | `internal/domain/{plural}/events.go` |
| Repository interface | `Repository`; reads that lock end in `ForUpdate` | `internal/domain/{plural}/repository.go` |
| Rule codes and lengths | `<Rule>Code`, `<Field>MaxLength` constants | `internal/domain/{plural}/constants.go` |
| Command | `<Verb><Noun>Command`, one file per command: `create_product.go` | `internal/application/{plural}/` |
| Handler | `<Verb><Noun>Handler`, beside its command | same file |
| DTO | `<Aggregate>Dto`, JSON members in PascalCase by tags (`json:"Sku"`), mapping function `Map<Aggregate>` | `internal/application/{plural}/dto.go` |
| Sync handler | `<Action>When<Event>Handler` | `internal/application/{plural}/<action>_when_<event>.go` |
| Consumer | `<Action>When<Event>Consumer` | same |
| Persistence repository | `Repository` struct | `internal/infrastructure/persistence/{plural}/repository.go` |
| Tables and columns | snake_case plural: `order_lines.price_amount` | migrations |
| Migration | `NNNN_<what>.sql`, applied in name order | `internal/infrastructure/persistence/migrations/` |
| Test | `Test<Unit>_<Situation>_<Outcome>`, or `Test<Unit>` holding a table | `<file>_test.go` beside the code |
| JSON | PascalCase in commands and DTOs (tags), camelCase over HTTP (`WriteJSON` converts), the Console prints PascalCase | |
