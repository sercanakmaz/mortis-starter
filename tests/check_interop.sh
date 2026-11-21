#!/usr/bin/env bash
# Proves the RabbitMQ bus of _temp/variants/advanced is wire-compatible with MassTransit 8 (docs/specs/messaging-wire-format.md,
# checks 1 to 4; check 5, the error queue, is in scripts/conformance/transport-rabbitmq.sh).
# Needs: the .NET SDK, a RabbitMQ at localhost:5672 (guest/guest), e.g. docker run -d -p 5672:5672 -p 15672:15672 rabbitmq:4-management
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../_temp/interop"
dotnet test --nologo 2>&1 | tail -3
