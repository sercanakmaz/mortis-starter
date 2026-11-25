package orders

// Status is where an order is in its life.
type Status string

// The statuses of an order.
const (
	StatusOpen      Status = "Open"
	StatusCompleted Status = "Completed"
	StatusCancelled Status = "Cancelled"
)
