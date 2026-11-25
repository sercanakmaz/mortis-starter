#!/usr/bin/env bash
# Proves the RabbitMQ bus of reference/variants/advanced and the Go bus of reference/interop/go-worker are wire-compatible with
# MassTransit 8 and with each other (docs/specs/messaging-wire-format.md: checks 1 to 4 for C#, check 6 = checks 1 to 5 for Go;
# C#'s check 5, the error queue, is in scripts/conformance/transport-rabbitmq.sh). Needs Go too.
# Needs: the .NET SDK, a RabbitMQ at localhost:5672 (guest/guest), e.g. docker run -d -p 5672:5672 -p 15672:15672 rabbitmq:4-management
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../reference/interop"
(cd go-worker && go build -o "${GOBUS:=/tmp/gobus}" ./cmd/gobus)
export GOBUS MORTIS_LINE_ADDED_WIRE_NAME="Boilerplate.Contracts.Orders:LineAdded"
dotnet test --nologo 2>&1 | tail -3
