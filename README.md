# TableMint

TableMint 是以中繼資料驅動（Metadata-Driven）的輕量級無程式碼資料庫 API。使用者透過 API 定義 Table 與 Fields，不需要為每個 Table 執行資料庫 DDL；Records 的動態內容保存於 PostgreSQL `jsonb`，並依目前 Table Schema 嚴格驗證。

## MVP 功能

- Table 建立、清單、查詢、改名與軟刪除
- 建立 Table 時一併定義初始 Fields
- Text、Integer／Decimal Number、Date、SingleSelect Fields
- Field label、驗證規則、順序與啟用狀態管理
- Schema Version 與 Table／Record 樂觀並行控制
- Schema 收緊前掃描既有 Records
- 新增 Required Field 時以 Default Value 回填既有 Records
- Record CRUD、軟刪除與完整 PATCH 後驗證
- Record 分頁、建立／修改時間排序與單一 Field 型態化等值篩選
- 固定 Tenant Context 資料隔離
- RFC Problem Details 錯誤回應
- PostgreSQL migrations、Docker Compose、Demo Seed 與 Swagger UI

一般使用者前端不屬於本次 MVP；Swagger UI 是正式操作與展示介面。

## 快速啟動

需要 Docker 與 Docker Compose：

```bash
docker compose up --build
```

服務啟動後：

- Swagger UI：<http://localhost:8080/swagger>
- OpenAPI JSON：<http://localhost:8080/openapi/v1.json>
- Health：<http://localhost:8080/health>

Compose 會：

1. 啟動 PostgreSQL。
2. 等待資料庫健康。
3. 啟動 TableMint API。
4. 自動套用 EF Core migrations。
5. 建立「專案追蹤 Demo」Table 與一筆示範 Record。

PostgreSQL 資料保存在 `tablemint-postgres` named volume；一般的 `docker compose down` 與重新啟動不會刪除資料。若確定要清除全部本機資料，使用：

```bash
docker compose down --volumes
```

## 本機開發

需要 .NET 10 SDK 與 PostgreSQL。預設連線設定位於 `TableMint.WebApi/appsettings.json`：

```text
Host=localhost;Port=5432;Database=tablemint;Username=tablemint;Password=tablemint
```

套用 migration 並啟動：

```bash
./.tools/dotnet-ef database update \
  --project TableMint.Infrastructure \
  --startup-project TableMint.WebApi

dotnet run --project TableMint.WebApi
```

若要在本機啟動時自動 migration／seed，可使用環境變數：

```bash
Database__ApplyMigrationsOnStartup=true \
SeedData__Enabled=true \
dotnet run --project TableMint.WebApi
```

## 核心操作流程

### 1. 建立 Table 與初始 Fields

```http
POST /api/tables
Content-Type: application/json

{
  "name": "專案追蹤",
  "fields": [
    {
      "fieldKey": "name",
      "label": "專案名稱",
      "type": "text",
      "required": true,
      "maxLength": 100
    },
    {
      "fieldKey": "budget",
      "label": "預算",
      "type": "number",
      "required": false,
      "numberMode": "decimal",
      "min": 0
    },
    {
      "fieldKey": "due_date",
      "label": "到期日",
      "type": "date",
      "required": false
    },
    {
      "fieldKey": "status",
      "label": "狀態",
      "type": "singleSelect",
      "required": true,
      "options": [
        { "value": "planned", "label": "規劃中", "isActive": true },
        { "value": "active", "label": "進行中", "isActive": true },
        { "value": "done", "label": "已完成", "isActive": true }
      ]
    }
  ]
}
```

### 2. 新增 Record

```http
POST /api/tables/{tableId}/records
Content-Type: application/json

{
  "data": {
    "name": "TableMint MVP",
    "budget": 100000,
    "due_date": "2026-12-31",
    "status": "active"
  }
}
```

### 3. 查詢、排序與篩選

```http
GET /api/tables/{tableId}/records?page=1&pageSize=20&sortBy=updatedAt&sortDirection=descending&filterField=status&filterValue=active
```

`filterValue` 會依 Field Type 解析。Number 使用 JSON number 比對；Date 只接受 `YYYY-MM-DD`。

### 4. 使用 Version 更新

```http
PATCH /api/tables/{tableId}/records/{recordId}
Content-Type: application/json

{
  "data": { "status": "done" },
  "expectedVersion": 1
}
```

若版本已被其他請求更新，回傳 `409 Conflict`。

## HTTP 狀態與錯誤格式

| 狀態 | 用途 |
|---|---|
| `400` | 請求、分頁、排序或篩選參數不合法 |
| `404` | Table、Field 或 Record 不存在 |
| `409` | Version 衝突，或 Schema 變更會使既有資料失效 |
| `422` | Record 不符合 Table Schema |

錯誤使用 `application/problem+json`，並包含穩定的 `code`。Record 驗證錯誤另包含所有可辨識的欄位錯誤；Schema 變更被拒絕時包含 `affectedRecordCount`。

## 測試

```bash
dotnet test TableMint.slnx
```

測試分為：

- Domain／Application 行為測試
- PostgreSQL `jsonb`、型態化查詢與並行控制整合測試
- Web API、狀態碼與 Problem Details 測試

PostgreSQL 測試預設使用：

```text
Host=localhost;Database=tablemint_test;Username=hui
```

可用環境變數覆寫，不需要修改測試原始碼：

```bash
TABLEMINT_TEST_CONNECTION='Host=localhost;Database=tablemint_test;Username=tablemint;Password=tablemint' \
dotnet test TableMint.Infrastructure.Tests
```

## 專案結構

```text
TableMint.Domain/          核心領域模型與規則
TableMint.Application/     Use cases、DTO、DynamicValidator 與 seams
TableMint.Infrastructure/  EF Core、PostgreSQL、jsonb 與 migrations
TableMint.WebApi/          HTTP endpoints、OpenAPI、Problem Details
```

更完整的決策與驗收條件請參閱 [TableMint-MVP-SPEC.md](TableMint-MVP-SPEC.md) 與 [CONTEXT.md](CONTEXT.md)。
