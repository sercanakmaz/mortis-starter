# Interoperability tests (temporary)

An xunit project that references MassTransit 8.5.12 (the only place in the repository that does) and the advanced variant's
Infrastructure project. Against a real RabbitMQ it proves, both serializers where they apply:

1. and 2. a MassTransit consumer receives an event our `RabbitMqPublisher` published, with its `MessageId` and `CorrelationId`;
3. our `MessageEnvelope` reads an event MassTransit published;
4. our receive endpoint and a MassTransit endpoint declare the same queue and exchanges without `PRECONDITION_FAILED`.

Run with `bash tests/check_interop.sh`. Check 5 (the error queue and its fault headers) runs in the conformance suite.
