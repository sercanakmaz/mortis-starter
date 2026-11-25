package persistence

import "strings"

var keywords = map[string]string{"host": "host", "port": "port", "database": "dbname", "username": "user", "user id": "user", "password": "password", "timeout": "connect_timeout"}

// ConnectionString accepts the URL or keyword form, and the semicolon form the C# projects use (Host=...;Database=...).
func ConnectionString(value string) string {
	if !strings.Contains(value, ";") && !strings.HasPrefix(strings.ToLower(value), "host=") {
		return value
	}

	parts := []string{}

	for _, pair := range strings.Split(value, ";") {
		key, setting, found := strings.Cut(pair, "=")
		if !found {
			continue
		}

		if keyword, ok := keywords[strings.ToLower(strings.TrimSpace(key))]; ok {
			parts = append(parts, keyword+"='"+strings.ReplaceAll(strings.TrimSpace(setting), "'", `\'`)+"'")
		}
	}

	return strings.Join(parts, " ")
}
