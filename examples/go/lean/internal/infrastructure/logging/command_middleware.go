package logging

import (
	"context"
	"log/slog"

	"delta/internal/shared"
)

// CommandMiddleware gives every log line of a command its context fields and logs the command's start and failure.
func CommandMiddleware(logger *slog.Logger) shared.Middleware {
	logger = logger.With("Category", "Delta.Commands")

	return func(ctx context.Context, command any, next func(ctx context.Context) (any, error)) (any, error) {
		ctx = WithFields(ctx, OfCommand(command)...)
		name := TypeName(command)

		logger.InfoContext(ctx, name+" started")

		result, err := next(ctx)
		if err != nil {
			logger.WarnContext(ctx, name+" failed: "+err.Error())

			return nil, err
		}

		logger.InfoContext(ctx, name+" completed")

		return result, nil
	}
}
