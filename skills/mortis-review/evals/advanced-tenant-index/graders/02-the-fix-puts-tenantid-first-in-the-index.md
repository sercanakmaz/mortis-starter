---
type: llm
focus: trace
weight: 1
---

The fix puts TenantId first in the index: HasIndex(warehouse => new { warehouse.TenantId, warehouse.Code }).IsUnique()
