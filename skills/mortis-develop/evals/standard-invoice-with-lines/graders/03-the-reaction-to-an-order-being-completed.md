---
type: llm
focus: trace
weight: 1
---

The reaction to an order being completed is an event consumer (BaseEventConsumer of an IQueueEvent) registered in the host with AddEventConsumer, not a line added to CompleteOrderCommand
