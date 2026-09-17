# TableMint MVP 規格整理

更新日期：2026-09-17

## 文件狀態

- Q1–Q43：已確認
- 測試 seams：DynamicValidator、Application use cases、Persistence、HTTP API
- 專案已開始以 TDD 實作

## 1. 專案定位

TableMint 是一個以中繼資料驅動（Metadata-Driven）的輕量級無代碼表單與資料庫管理系統。使用者不需要執行資料庫 DDL，即可透過 API 建立 Table、定義 Fields，並管理符合動態結構的 Records。

目前先完成可展示的 MVP，未來再演進成可實際部署的產品。

MVP 的核心流程：

```text
建立 Table
→ 定義 Fields
→ 取得 Table Schema
→ 寫入 Record
→ 動態驗證
→ 查詢、修改與刪除 Record
```

MVP 以 REST API 與 Swagger 作為正式操作及展示介面。一般使用者 UI 暫不列入核心時程。

## 2. 統一領域語言

### Table

使用者建立的邏輯資料集合，擁有一組有順序的 Field Definitions 及其 Records。這裡的 Table 不代表 PostgreSQL 中會建立一張對應的實體 table。

避免使用：Sheet、form、database。

### Table Schema

Table 目前有效且有順序的 Field Definitions，決定 Records 可以包含哪些值。

### Schema Version

Table Schema 的遞增版本。當 Record 的解讀或驗證方式改變時增加。

### Field Definition

Table 結構的一部分，定義一個值的名稱、型態、驗證規則、選項與顯示順序。每個 Field Definition 只屬於一個 Table。

### Field Key

Field Definition 的不可變、可讀識別碼，用來定位 Record JSON 中的值，例如 `customer_name`。

### Record

Table 中的一筆資料，包含以該 Table 的 Field Keys 定位的動態值。

### Select Option

SingleSelect Field 的合法選項。每個選項具有不可變的 `value` 與可修改的 `label`。

### Tenant

資料彼此隔離的組織。每個 Tenant 擁有自己的 Tables 與 Records。

### Schema Suggestion

根據欄位名稱和少量樣本值推測 Field Definitions 的選用功能。建議內容必須經使用者確認，不能自動修改 Table。

## 3. 領域模型

### Table

預計包含：

- `Id`
- `TenantId`
- `Name`
- `SchemaVersion`
- `Version`
- `CreatedAt`
- `UpdatedAt`
- `DeletedAt`
- 一組有順序的 Field Definitions
- 一組 Records

Table 採軟刪除。

### Field Definition

預計包含：

- 系統 GUID `Id`
- 不可變的 `FieldKey`
- 可修改的 `Label`
- `FieldType`
- 型態專屬驗證規則
- `Position`
- 啟用或停用狀態

Field Definition 不跨 Table 共用。已有資料時，Field 原則上停用而非實體刪除。

### Record

系統資料使用關聯式欄位：

```text
Id
TenantId
TableId
Data
SchemaVersion
Version
CreatedAt
UpdatedAt
DeletedAt
```

只有使用者定義的 Field 值放入 PostgreSQL `jsonb`：

```json
{
  "customer_name": "Amy",
  "amount": 1250,
  "status": "in_progress"
}
```

## 4. 支援的欄位型態

### Text

- JSON string
- 支援 `minLength`
- 支援 `maxLength`
- 預定義格式：`Plain`、`Email`
- MVP 不開放任意 Regex

### Number

Number Field 具有 `numberMode`：

- `Integer`
- `Decimal`

可設定 `min` 與 `max`。小數在 .NET 中使用 `decimal`，不使用 `double`，避免金額等資料出現精度誤差。

### Date

- 使用 `YYYY-MM-DD`
- 是純日期，不包含時間或時區
- 不接受日期時間字串

### SingleSelect

每個選項包含：

```json
{
  "value": "in_progress",
  "label": "處理中"
}
```

Record 儲存固定的 `value`。修改 label 不需改寫既有 Record。已被使用的選項只能停用，不能直接刪除。

## 5. 動態驗證規則

API 採嚴格驗證，不進行隱性型態轉換：

- Number 不接受 `"12.5"` 字串
- Date 僅接受指定日期格式
- SingleSelect 必須是合法且有效的 option value
- 未定義的 Field 一律拒絕
- 必填欄位拒絕 missing、JSON `null` 和空白文字
- 非必填欄位允許 missing 或 JSON `null`

PATCH Record 時的處理流程：

```text
現有資料 + PATCH 資料
→ 合併
→ 對完整 Record 重新驗證
→ 成功後儲存
```

驗證時一次回傳全部可辨識的欄位錯誤：

```json
{
  "errors": [
    {
      "fieldId": "...",
      "fieldLabel": "金額",
      "code": "number_out_of_range",
      "message": "金額不得小於 0"
    }
  ]
}
```

Record 驗證失敗使用 HTTP `422 Unprocessable Content`。

## 6. Schema 管理與演進

MVP 允許：

- 建立 Table
- 一次建立 Table 與初始 Fields
- 在既有 Table 新增 Field
- 修改 Table 名稱
- 修改 Field label
- 修改驗證規則
- 調整 Field 順序
- 停用 Field 或 Select Option

限制：

- `FieldKey` 建立後不可修改
- Field Type 不可直接修改
- Schema 修改前必須檢查既有 Records
- 若新規則會使既有資料失效，拒絕修改並回報受影響筆數
- Table 已有 Records 時，新增必填 Field 必須提供預設值
- 影響 Record 解讀或驗證的修改會增加 Schema Version
- 單純修改 Table 名稱不增加 Schema Version
- Record 保存寫入時所使用的 Schema Version
- Field 重排由系統接收完整 Field ID 順序後重新編號

## 7. 查詢能力

MVP 支援：

- 分頁
- 每頁最多 100 筆
- 依建立時間或修改時間排序
- 單一 Field 等值篩選
- 根據 Field Type 進行正確的 JSONB 型態比較

若查詢指定不存在、已停用或型態不符的 Field，回傳 `400 Bad Request`，不以空集合掩蓋錯誤。

MVP 預期每個 Table 最多約 10,000 筆 Records。大量資料的非同步 Schema 遷移與進階索引留待部署版本處理。

## 8. 並行控制與租戶隔離

- Table 與 Record 使用整數 `Version` 做樂觀並行控制
- 使用舊 Version 修改資料時回傳 `409 Conflict`
- MVP 使用一個固定的 Default Tenant
- Default Tenant ID 由應用程式設定提供
- Application 模組只能透過 `TenantContext` 取得 Tenant ID
- 未來接入 JWT 時，更換 Tenant Context adapter
- 不同 Tenant Context 不得存取彼此的 Tables 或 Records

## 9. 技術架構

```text
TableMint/
├── TableMint.Domain/          # 核心領域模型與規則
├── TableMint.Application/     # 使用案例、DTO、DynamicValidator
├── TableMint.Infrastructure/  # EF Core、PostgreSQL、jsonb、migration
└── TableMint.WebApi/          # HTTP endpoints、Swagger、Problem Details
```

技術選擇：

- ASP.NET Core
- 目前受支援的 .NET LTS
- EF Core
- PostgreSQL
- `jsonb`
- Docker Compose 啟動本機 PostgreSQL
- EF Core migrations 納入版本控制

程式碼、類別名稱、API JSON 欄位與錯誤碼使用英文。README、架構文件及 Demo 指引使用繁體中文，重要術語附英文。

## 10. 錯誤回應

API 統一採用 RFC Problem Details，並包含穩定的錯誤 `code`。

| HTTP 狀態碼 | 用途 |
|---|---|
| `400` | 請求或查詢參數不合法 |
| `404` | Table、Field 或 Record 不存在 |
| `409` | Version 或唯一性衝突 |
| `422` | Record 不符合 Table Schema |

## 11. AI Schema Suggestion

不建立可以對任意 Table 訓練模型的通用 AI 平台。

選用的 Schema Suggestion 流程：

```text
欄位名稱 + 少量樣本值
→ AI 建議 Field Type、Required 與 SingleSelect options
→ 使用者確認
→ 才能修改 Table Schema
```

Schema Suggestion 是 stretch goal，不阻擋核心 MVP 驗收。沒有設定 AI provider 時，核心系統仍必須完整運作。

## 12. 核心 MVP 非目標

- JWT 登入、使用者及權限管理
- 完整多租戶管理介面
- 一般使用者前端、Grid 與拖拉設計器
- CSV／Excel 匯入匯出
- 公式計算
- Table 之間的關聯參照
- 任意 Regex
- 全文搜尋及複合篩選
- 批次寫入
- 非同步 Schema 遷移
- 資料還原介面
- SQL Server 支援
- 通用 AI／機器學習訓練平台

這些功能只列入 roadmap。目前不預先建立空介面或假模組，等真正需求出現後再設計適當的 module interface 與 seam。

## 13. 測試與完成門檻

- Domain/Application 單元測試涵蓋四種 Field Type、必填、範圍、格式及選項驗證
- Application 整合測試涵蓋 Schema 變更、Record CRUD、軟刪除及版本衝突
- PostgreSQL 整合測試涵蓋 `jsonb` 儲存及型態化篩選
- Web API 測試涵蓋 HTTP 狀態碼及 Problem Details
- 所有測試及 build 必須通過
- PostgreSQL 重啟後資料必須保留
- Migration 必須能在全新資料庫重建資料結構
- Swagger 必須能展示完整核心流程

## 14. 已確認的 API

```text
Tables
POST   /api/tables
GET    /api/tables
GET    /api/tables/{tableId}
PATCH  /api/tables/{tableId}
DELETE /api/tables/{tableId}

Fields
POST   /api/tables/{tableId}/fields
PATCH  /api/tables/{tableId}/fields/{fieldId}
POST   /api/tables/{tableId}/fields/reorder

Records
POST   /api/tables/{tableId}/records
GET    /api/tables/{tableId}/records
GET    /api/tables/{tableId}/records/{recordId}
PATCH  /api/tables/{tableId}/records/{recordId}
DELETE /api/tables/{tableId}/records/{recordId}
```

不提供批次寫入與還原 API。

## 15. 已確認的核心驗收情境

1. 建立名為「專案追蹤」的 Table。
2. 定義 Text、Integer Number、Decimal Number、Date 與 SingleSelect Fields。
3. 取得 Table Schema，確認 Field 順序、規則及 Schema Version。
4. 成功新增合法 Record，並以 `fieldKey` 儲存動態資料。
5. 一次送入多個錯誤值，收到包含全部欄位錯誤的 `422` 回應。
6. PATCH Record 後，系統以合併完成的完整資料重新驗證。
7. 使用 Field 等值條件查詢 Record，Number 與 Date 依正確型態比較。
8. 使用舊 Version 修改 Record，收到 `409 Conflict`。
9. 修改 Field label，不影響既有 JSON 資料。
10. 停用正在使用的 SingleSelect 選項，但保留既有 Record 的可解讀性。
11. 嘗試收緊 Schema 規則，使既有 Record 失效；系統拒絕變更並回報受影響筆數。
12. 軟刪除 Record 與 Table 後，一般查詢不再回傳它們。
13. 不同 Tenant Context 無法讀取彼此資料。
14. PostgreSQL 重啟後資料仍存在，migration 可在全新資料庫重建結構。
15. Swagger 能完整重現上述主要流程。

## 16. 已確認的實作順序

1. 建立 solution、專案參考與測試基礎。
2. 建立 Table、Field Definition、Record 領域模型。
3. 以測試驅動完成 DynamicValidator。
4. 建立 PostgreSQL／EF Core 儲存與 migrations。
5. 完成 Table 與 Field 操作。
6. 完成 Record CRUD、並行控制與錯誤格式。
7. 完成分頁、排序及單欄等值篩選。
8. 完成 Docker Compose、種子資料、Swagger demo 與繁中 README。
9. 核心驗收通過後，再評估薄 UI。
10. 最後才接入可替換的 Schema Suggestion adapter。
