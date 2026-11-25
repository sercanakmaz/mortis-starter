package rabbitmq

// The wire format of docs/specs/messaging-wire-format.md, the same values as the C# RabbitMqConstants.
const (
	ContentType          = "application/vnd.masstransit+json"
	URNPrefix            = "urn:message:"
	AddressPrefix        = "rabbitmq://localhost/"
	ErrorSuffix          = "_error"
	QueueTypeArgument    = "x-queue-type"
	ClassicQueueType     = "classic"
	PrefetchCount        = 16
	EventBaseName        = "Boilerplate.Shared.Domain:EventBase"
	QueueEventName       = "Boilerplate.Shared.Domain:IQueueEvent"
	TenantHeader         = "Mortis-Tenant-Id"
	UserHeader           = "Mortis-User-Id"
	UserEmailHeader      = "Mortis-User-Email"
	LanguageHeader       = "Mortis-Language"
	FaultReason          = "fault"
	ReasonHeader         = "MT-Reason"
	FaultExceptionType   = "MT-Fault-ExceptionType"
	FaultMessage         = "MT-Fault-Message"
	FaultStackTrace      = "MT-Fault-StackTrace"
	FaultTimestamp       = "MT-Fault-Timestamp"
	FaultConsumerType    = "MT-Fault-ConsumerType"
	FaultInputAddress    = "MT-Fault-InputAddress"
	FaultMessageType     = "MT-Fault-MessageType"
	HostMachineName      = "MT-Host-MachineName"
	GoErrorExceptionType = "Go.error"
)
