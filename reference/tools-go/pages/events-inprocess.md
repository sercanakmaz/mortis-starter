# Consumers in the same process: internal/infrastructure/messaging

With `events.outbox` = `none`, the unit of work hands the committed events to `EventPublisher`, which runs their consumers at once, in the process that ran the command. A consumer's failure is logged with `Exception`, never returned: the command has committed. A crash between the commit and the consumer loses the reaction; that is the price of this option (`docs/` of the architecture skill).

@@go internal/infrastructure/messaging/event_publisher.go@@
