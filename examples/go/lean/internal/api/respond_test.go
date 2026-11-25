package api

import (
	"net/http/httptest"
	"strings"
	"testing"

	"delta/internal/shared"
)

func TestWriteJSON_WritesCamelCaseMembers(t *testing.T) {
	recorder := httptest.NewRecorder()

	WriteJSON(recorder, 200, struct {
		OrderNumber string
		Lines       []struct{ Sku string }
	}{OrderNumber: "A-1", Lines: []struct{ Sku string }{{Sku: "S"}}})

	if body := strings.TrimSpace(recorder.Body.String()); body != `{"lines":[{"sku":"S"}],"orderNumber":"A-1"}` {
		t.Fatalf("got %s", body)
	}
}

func TestWriteError_KeepsTheFieldNamesOfAValidationFailure(t *testing.T) {
	recorder := httptest.NewRecorder()

	WriteError(recorder, nil, shared.ValidationFailed(map[string][]string{"Lines": {"'Lines' must not be empty."}}))

	if recorder.Code != 422 || !strings.Contains(recorder.Body.String(), `"Lines"`) {
		t.Fatalf("got %d %s", recorder.Code, recorder.Body.String())
	}
}

func TestWriteError_WithAnUnexpectedError_Answers500WithAReference(t *testing.T) {
	recorder := httptest.NewRecorder()

	WriteError(recorder, nil, errTest("broken"))

	if recorder.Code != 500 || !strings.Contains(recorder.Body.String(), UnexpectedErrorCode) || !strings.Contains(recorder.Body.String(), "Reference ") {
		t.Fatalf("got %d %s", recorder.Code, recorder.Body.String())
	}
}

type errTest string

func (e errTest) Error() string { return string(e) }
