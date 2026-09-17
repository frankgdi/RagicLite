using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TableMint.Application.Abstractions;
using TableMint.Domain.Tables;
using DomainRecord = TableMint.Domain.Tables.Record;

namespace TableMint.WebApi.Tests;

public sealed class TablesApiTests
{
    [Fact]
    public async Task Health_and_swagger_pages_are_available()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var swagger = await client.GetAsync("/swagger");
        var openApi = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("healthy", await health.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Equal("text/html", swagger.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        using var openApiBody = JsonDocument.Parse(await openApi.Content.ReadAsStringAsync());
        var paths = openApiBody.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/tables", out _));
        Assert.True(paths.TryGetProperty("/api/tables/{tableId}", out _));
        Assert.True(paths.TryGetProperty("/api/tables/{tableId}/fields", out _));
        Assert.True(paths.TryGetProperty("/api/tables/{tableId}/fields/{fieldId}", out _));
        Assert.True(paths.TryGetProperty("/api/tables/{tableId}/fields/reorder", out _));
        Assert.True(paths.TryGetProperty("/api/tables/{tableId}/records", out _));
        Assert.True(paths.TryGetProperty("/api/tables/{tableId}/records/{recordId}", out _));
    }

    [Fact]
    public async Task Created_table_can_be_retrieved_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var getResponse = await client.GetAsync(createResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var body = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "專案追蹤",
            body.RootElement.GetProperty("name").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task Table_can_be_created_with_initial_fields()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/tables",
            new
            {
                name = "專案追蹤",
                fields = new object[]
                {
                    new
                    {
                        fieldKey = "name",
                        label = "名稱",
                        type = "text",
                        required = true,
                        maxLength = 100,
                    },
                    new
                    {
                        fieldKey = "due_date",
                        label = "到期日",
                        type = "date",
                        required = false,
                    },
                },
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, body.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(3, body.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("fields").GetArrayLength());
        Assert.Equal(
            "name",
            body.RootElement.GetProperty("fields")[0].GetProperty("fieldKey").GetString());
    }

    [Fact]
    public async Task Invalid_table_request_returns_problem_details()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "table_name_required",
            body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Updating_a_missing_table_returns_not_found_problem_details()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/tables/{Guid.NewGuid()}",
            new { name = "不存在", expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("table_not_found", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Table_can_be_listed_renamed_and_deleted_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "舊名稱" });
        using var createdBody = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var tableId = createdBody.RootElement.GetProperty("id").GetGuid();

        var listResponse = await client.GetAsync("/api/tables");
        using var listBody = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync());
        Assert.Equal("舊名稱", listBody.RootElement[0].GetProperty("name").GetString());

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/tables/{tableId}",
            new { name = "新名稱", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using var updatedBody = JsonDocument.Parse(
            await updateResponse.Content.ReadAsStringAsync());
        Assert.Equal("新名稱", updatedBody.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, updatedBody.RootElement.GetProperty("version").GetInt32());

        var deleteResponse = await client.DeleteAsync(
            $"/api/tables/{tableId}?expectedVersion=2");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/tables/{tableId}")).StatusCode);
    }

    [Fact]
    public async Task Text_field_can_be_added_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/fields",
            new
            {
                fieldKey = "project_name",
                label = "專案名稱",
                type = "text",
                required = true,
                expectedVersion = 1,
                minLength = 2,
                maxLength = 100,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "project_name",
            body.RootElement
                .GetProperty("fields")[0]
                .GetProperty("fieldKey")
                .GetString());
    }

    [Fact]
    public async Task Valid_record_can_be_created_and_retrieved_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/fields",
            new
            {
                fieldKey = "project_name",
                label = "專案名稱",
                type = "text",
                required = true,
                expectedVersion = 1,
            });

        var createResponse = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new
            {
                data = new { project_name = "TableMint" },
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        var getResponse = await client.GetAsync(createResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var body = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "TableMint",
            body.RootElement
                .GetProperty("data")
                .GetProperty("project_name")
                .GetString());
    }

    [Fact]
    public async Task Invalid_record_patch_returns_field_errors_as_problem_details()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        var createRecordResponse = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { } });
        using var recordBody = JsonDocument.Parse(
            await createRecordResponse.Content.ReadAsStringAsync());
        var recordId = recordBody.RootElement.GetProperty("id").GetGuid();

        var response = await client.PatchAsJsonAsync(
            $"/api/tables/{tableId}/records/{recordId}",
            new
            {
                data = new { unexpected = 1 },
                expectedVersion = 1,
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "record_validation_failed",
            body.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "unexpected",
            body.RootElement.GetProperty("errors")[0].GetProperty("fieldKey").GetString());
    }

    [Fact]
    public async Task Stale_record_version_returns_conflict_problem_details()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var tableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "並行測試" });
        using var tableBody = JsonDocument.Parse(await tableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        var recordResponse = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { } });
        using var recordBody = JsonDocument.Parse(await recordResponse.Content.ReadAsStringAsync());
        var recordId = recordBody.RootElement.GetProperty("id").GetGuid();
        var endpoint = $"/api/tables/{tableId}/records/{recordId}";
        await client.PatchAsJsonAsync(endpoint, new { data = new { }, expectedVersion = 1 });

        var response = await client.PatchAsJsonAsync(
            endpoint,
            new { data = new { }, expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("record_version_conflict", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Schema_change_that_invalidates_records_reports_affected_count()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var tableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new
            {
                name = "規則測試",
                fields = new[]
                {
                    new
                    {
                        fieldKey = "name",
                        label = "名稱",
                        type = "text",
                        required = true,
                        maxLength = 100,
                    },
                },
            });
        using var tableBody = JsonDocument.Parse(await tableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        var fieldId = tableBody.RootElement.GetProperty("fields")[0].GetProperty("id").GetGuid();
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { name = "TableMint" } });

        var response = await client.PatchAsJsonAsync(
            $"/api/tables/{tableId}/fields/{fieldId}",
            new { expectedVersion = 2, maxLength = 2 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "schema_change_would_invalidate_records",
            body.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("affectedRecordCount").GetInt32());
    }

    [Fact]
    public async Task Invalid_record_returns_all_recognizable_field_errors()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var tableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new
            {
                name = "驗證測試",
                fields = new object[]
                {
                    new { fieldKey = "name", label = "名稱", type = "text", required = true },
                    new
                    {
                        fieldKey = "score",
                        label = "分數",
                        type = "number",
                        required = true,
                        numberMode = "integer",
                    },
                    new { fieldKey = "due_date", label = "到期日", type = "date", required = true },
                },
            });
        using var tableBody = JsonDocument.Parse(await tableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { name = " ", score = 1.5m, due_date = "2026-02-30" } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var codes = body.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("required", codes);
        Assert.Contains("invalid_integer", codes);
        Assert.Contains("invalid_date", codes);
    }

    [Fact]
    public async Task Deleted_record_is_no_longer_available_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        var createRecordResponse = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { } });
        using var recordBody = JsonDocument.Parse(
            await createRecordResponse.Content.ReadAsStringAsync());
        var recordId = recordBody.RootElement.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync(
            $"/api/tables/{tableId}/records/{recordId}?expectedVersion=1");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var getResponse = await client.GetAsync(
            $"/api/tables/{tableId}/records/{recordId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Records_can_be_listed_with_pagination_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { } });
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { } });

        var response = await client.GetAsync(
            $"/api/tables/{tableId}/records?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, body.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Records_can_be_filtered_by_a_typed_number_field_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "成績" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/fields",
            new
            {
                fieldKey = "score",
                label = "分數",
                type = "number",
                required = true,
                expectedVersion = 1,
                numberMode = "integer",
            });
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { score = 12 } });
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { score = 13 } });

        var response = await client.GetAsync(
            $"/api/tables/{tableId}/records?filterField=score&filterValue=12");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(12, item.GetProperty("data").GetProperty("score").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Records_can_be_filtered_by_an_exact_date_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var tableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new
            {
                name = "日期篩選",
                fields = new[]
                {
                    new
                    {
                        fieldKey = "due_date",
                        label = "到期日",
                        type = "date",
                        required = true,
                    },
                },
            });
        using var tableBody = JsonDocument.Parse(await tableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { due_date = "2026-09-17" } });
        await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { due_date = "2026-09-18" } });

        var response = await client.GetAsync(
            $"/api/tables/{tableId}/records?filterField=due_date&filterValue=2026-09-17");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(
            "2026-09-17",
            item.GetProperty("data").GetProperty("due_date").GetString());
    }

    [Fact]
    public async Task Integer_number_field_is_enforced_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        var fieldResponse = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/fields",
            new
            {
                fieldKey = "quantity",
                label = "數量",
                type = "number",
                required = true,
                expectedVersion = 1,
                numberMode = "integer",
            });
        Assert.Equal(HttpStatusCode.OK, fieldResponse.StatusCode);

        var response = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { quantity = 1.5m } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "invalid_integer",
            body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Date_field_is_enforced_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        var fieldResponse = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/fields",
            new
            {
                fieldKey = "due_date",
                label = "到期日",
                type = "date",
                required = true,
                expectedVersion = 1,
            });
        Assert.Equal(HttpStatusCode.OK, fieldResponse.StatusCode);

        var response = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { due_date = "2026-02-30" } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "invalid_date",
            body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Single_select_field_is_enforced_over_http()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createTableResponse = await client.PostAsJsonAsync(
            "/api/tables",
            new { name = "專案追蹤" });
        using var tableBody = JsonDocument.Parse(
            await createTableResponse.Content.ReadAsStringAsync());
        var tableId = tableBody.RootElement.GetProperty("id").GetGuid();
        var fieldResponse = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/fields",
            new
            {
                fieldKey = "status",
                label = "狀態",
                type = "singleSelect",
                required = true,
                expectedVersion = 1,
                options = new[]
                {
                    new { value = "open", label = "開啟", isActive = true },
                },
            });
        Assert.Equal(HttpStatusCode.OK, fieldResponse.StatusCode);

        var response = await client.PostAsJsonAsync(
            $"/api/tables/{tableId}/records",
            new { data = new { status = "closed" } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "invalid_option",
            body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITableRepository>();
                services.RemoveAll<IRecordRepository>();
                services.RemoveAll<ITenantContext>();
                services.RemoveAll<IClock>();
                services.AddSingleton<ITableRepository, InMemoryTableRepository>();
                services.AddSingleton<IRecordRepository, InMemoryRecordRepository>();
                services.AddSingleton<ITenantContext>(
                    new FixedTenantContext(Guid.Parse(
                        "11111111-1111-1111-1111-111111111111")));
                services.AddSingleton<IClock>(
                    new FixedClock(new DateTimeOffset(
                        2026,
                        9,
                        17,
                        5,
                        0,
                        0,
                        TimeSpan.Zero)));
            });
        }
    }

    private sealed class InMemoryTableRepository : ITableRepository
    {
        private readonly Dictionary<(Guid TenantId, Guid TableId), Table> _tables = [];

        public Task AddAsync(Table table, CancellationToken cancellationToken)
        {
            _tables.Add((table.TenantId, table.Id), table);
            return Task.CompletedTask;
        }

        public Task<Table?> GetAsync(
            Guid tenantId,
            Guid tableId,
            CancellationToken cancellationToken)
        {
            _tables.TryGetValue((tenantId, tableId), out var table);
            return Task.FromResult(table);
        }

        public Task<IReadOnlyList<Table>> ListAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Table>>(
                _tables.Values
                    .Where(table => table.TenantId == tenantId && table.DeletedAt is null)
                    .OrderBy(table => table.Name)
                    .ToArray());

        public Task UpdateAsync(Table table, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FixedTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class InMemoryRecordRepository : IRecordRepository
    {
        private readonly Dictionary<(Guid TenantId, Guid RecordId), DomainRecord> _records = [];

        public Task AddAsync(DomainRecord record, CancellationToken cancellationToken)
        {
            _records.Add((record.TenantId, record.Id), record);
            return Task.CompletedTask;
        }

        public Task<DomainRecord?> GetAsync(
            Guid tenantId,
            Guid tableId,
            Guid recordId,
            CancellationToken cancellationToken)
        {
            _records.TryGetValue((tenantId, recordId), out var record);
            return Task.FromResult(record?.TableId == tableId ? record : null);
        }

        public Task<IReadOnlyList<DomainRecord>> ListAsync(
            Guid tenantId,
            Guid tableId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DomainRecord>>(
                _records.Values
                    .Where(record =>
                        record.TenantId == tenantId &&
                        record.TableId == tableId &&
                        record.DeletedAt is null)
                    .ToArray());

        public Task<RecordPage> QueryAsync(
            Guid tenantId,
            Guid tableId,
            RecordQuery query,
            CancellationToken cancellationToken)
        {
            IEnumerable<DomainRecord> matching = _records.Values
                .Where(record =>
                    record.TenantId == tenantId &&
                    record.TableId == tableId &&
                    record.DeletedAt is null);
            if (query.FilterFieldKey is not null && query.FilterValue is not null)
            {
                matching = matching.Where(record =>
                    record.Data.TryGetProperty(query.FilterFieldKey, out var value) &&
                    JsonElement.DeepEquals(value, query.FilterValue.Value));
            }
            matching = (query.SortBy, query.SortDirection) switch
            {
                (RecordSortBy.CreatedAt, SortDirection.Ascending) => matching.OrderBy(x => x.CreatedAt),
                (RecordSortBy.UpdatedAt, SortDirection.Ascending) => matching.OrderBy(x => x.UpdatedAt),
                (RecordSortBy.UpdatedAt, SortDirection.Descending) => matching.OrderByDescending(x => x.UpdatedAt),
                _ => matching.OrderByDescending(x => x.CreatedAt),
            };
            var matched = matching.ToArray();
            return Task.FromResult(new RecordPage(
                matched.Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToArray(),
                matched.Length));
        }

        public Task UpdateAsync(
            DomainRecord record,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
