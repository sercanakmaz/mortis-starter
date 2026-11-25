package shared

import "strconv"

// FieldErrors collects validation failures per field.
type FieldErrors map[string][]string

// Add records a failure of one field.
func (f FieldErrors) Add(field string, message string) {
	f[field] = append(f[field], message)
}

// Check records the failure when the condition does not hold.
func (f FieldErrors) Check(ok bool, field string, message string) {
	if !ok {
		f.Add(field, message)
	}
}

// Validatable is a command that checks its own shape before it runs.
type Validatable interface {
	Validate() FieldErrors
}

// NotEmpty records that a text field is required.
func (f FieldErrors) NotEmpty(field string, value string) {
	f.Check(value != "", field, "'"+field+"' must not be empty.")
}

// MaxLength records that a text field is too long.
func (f FieldErrors) MaxLength(field string, value string, maximum int) {
	f.Check(len([]rune(value)) <= maximum, field, "'"+field+"' must be "+strconv.Itoa(maximum)+" characters or fewer.")
}
