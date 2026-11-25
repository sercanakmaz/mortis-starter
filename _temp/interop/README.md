# Interoperability tests (temporary)

An xunit project that references MassTransit 8.5.12 (the only place in the repository that does) and the advanced variant's
Infrastructure project. Against a real RabbitMQ it proves, both serializers where they apply:

1. and 2. a MassTransit consumer receives an event our `RabbitMqPublisher` published, with its `MessageId` and `CorrelationId`;
3. our `MessageEnvelope` reads an event MassTransit published;
4. our receive endpoint and a MassTransit endpoint declare the same queue and exchanges without `PRECONDITION_FAILED`.

Run with `bash tests/check_interop.sh`. Check 5 (the error queue and its fault headers) runs in the conformance suite.

`GoInteropTests.cs` is check 6, the Go bus (`go-worker/internal/infrastructure/messaging/rabbitmq`, driven through `go-worker/cmd/gobus`): Go receives what our C# publisher and MassTransit (both serializers) publish, with the context; our C# reader and MassTransit read what Go publishes; Go and MassTransit declare the same endpoint without `PRECONDITION_FAILED`; a failing Go consumer leaves the message in `<queue>_error` with the fault headers.

`go-worker/` is the Go reference project plus the Go side of both interop proofs: `cmd/worker` runs Go consumers on the rows of a C# outbox (`tests/check_interop_outbox.sh`), `cmd/gobus` and the `rabbitmq` package speak the bus (`tests/check_interop_bus.sh` runs it against the real C# Console and Worker).
