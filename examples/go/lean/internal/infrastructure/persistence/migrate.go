package persistence

import (
	"context"
	"embed"
	"fmt"
	"io/fs"
	"sort"

	"github.com/jackc/pgx/v5/pgxpool"
)

//go:embed migrations/*.sql
var migrations embed.FS

// Migrate applies, in name order, every migration file not applied yet, each in its own transaction.
func Migrate(ctx context.Context, pool *pgxpool.Pool) ([]string, error) {
	if _, err := pool.Exec(ctx, "create table if not exists schema_migrations (name varchar(200) primary key, applied_at timestamptz not null default now())"); err != nil {
		return nil, err
	}

	names, err := fs.Glob(migrations, "migrations/*.sql")
	if err != nil {
		return nil, err
	}

	sort.Strings(names)
	applied := []string{}

	for _, name := range names {
		var exists bool
		if err := pool.QueryRow(ctx, "select exists(select 1 from schema_migrations where name = $1)", name).Scan(&exists); err != nil {
			return applied, err
		}

		if exists {
			continue
		}

		script, err := migrations.ReadFile(name)
		if err != nil {
			return applied, err
		}

		transaction, err := pool.Begin(ctx)
		if err != nil {
			return applied, err
		}

		if _, err := transaction.Exec(ctx, string(script)); err != nil {
			_ = transaction.Rollback(ctx)

			return applied, fmt.Errorf("%s: %w", name, err)
		}

		if _, err := transaction.Exec(ctx, "insert into schema_migrations (name) values ($1)", name); err != nil {
			_ = transaction.Rollback(ctx)

			return applied, err
		}

		if err := transaction.Commit(ctx); err != nil {
			return applied, err
		}

		applied = append(applied, name)
	}

	return applied, nil
}
