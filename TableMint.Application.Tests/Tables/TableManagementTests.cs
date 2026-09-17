using System.Text.Json;
using TableMint.Application.Abstractions;
using TableMint.Application.Errors;
using TableMint.Application.Records;
using TableMint.Application.Tables;
using TableMint.Application.Validation;
using TableMint.Domain.Tables;
using DomainRecord = TableMint.Domain.Tables.Record;

namespace TableMint.Application.Tests.Tables;

public sealed class TableManagementTests
{
    [Fact]
    public async Task Created_table_can_be_retrieved_for_the_current_tenant()
    {
        var tenantId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 17, 1, 2, 3, TimeSpan.Zero);
        var tables = new InMemoryTableRepository();
        var management = new TableManagement(
            tables,
            new FixedTenantContext(tenantId),
            new FixedClock(now));

        var created = await management.CreateAsync(
            new CreateTableRequest("專案追蹤"));
        var retrieved = await management.GetAsync(created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(tenantId, retrieved.TenantId);
        Assert.Equal("專案追蹤", retrieved.Name);
        Assert.Equal(1, retrieved.SchemaVersion);
        Assert.Equal(1, retrieved.Version);
        Assert.Equal(now, retrieved.CreatedAt);
    }

    [Fact]
    public async Task Table_name_cannot_be_blank()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => management.CreateAsync(new CreateTableRequest("   ")));

        Assert.Equal("table_name_required", error.Code);
    }

    [Fact]
    public async Task Tables_are_listed_only_for_the_current_tenant()
    {
        var currentTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var current = new TableManagement(tables, new FixedTenantContext(currentTenant), clock);
        var other = new TableManagement(tables, new FixedTenantContext(otherTenant), clock);
        await current.CreateAsync(new CreateTableRequest("專案追蹤"));
        await other.CreateAsync(new CreateTableRequest("其他租戶"));

        var result = await current.ListAsync();

        var table = Assert.Single(result);
        Assert.Equal("專案追蹤", table.Name);
        Assert.Equal(currentTenant, table.TenantId);
    }

    [Fact]
    public async Task Table_can_be_renamed_with_its_current_version()
    {
        var now = new DateTimeOffset(2026, 9, 17, 5, 0, 0, TimeSpan.Zero);
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(now));
        var table = await management.CreateAsync(new CreateTableRequest("舊名稱"));

        var renamed = await management.UpdateAsync(
            table.Id,
            new UpdateTableRequest("新名稱", ExpectedVersion: 1));

        Assert.Equal("新名稱", renamed.Name);
        Assert.Equal(2, renamed.Version);
        Assert.Equal(1, renamed.SchemaVersion);
        Assert.Equal(now, renamed.UpdatedAt);
    }

    [Fact]
    public async Task Deleted_table_is_hidden_from_reads_and_lists()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));

        await management.DeleteAsync(table.Id, expectedVersion: 1);

        Assert.Null(await management.GetAsync(table.Id));
        Assert.Empty(await management.ListAsync());
    }

    [Fact]
    public async Task Adding_a_text_field_updates_the_table_schema()
    {
        var now = new DateTimeOffset(2026, 9, 17, 2, 0, 0, TimeSpan.Zero);
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(now));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));

        var updated = await management.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text(
                fieldKey: "project_name",
                label: "專案名稱",
                required: true,
                expectedVersion: 1,
                minLength: 2,
                maxLength: 100));

        Assert.Equal(2, updated.SchemaVersion);
        Assert.Equal(2, updated.Version);
        var field = Assert.Single(updated.Fields);
        Assert.Equal("project_name", field.FieldKey);
        Assert.Equal(FieldType.Text, field.Type);
        Assert.Equal(0, field.Position);
        Assert.True(field.IsActive);
        Assert.Equal(2, field.MinLength);
        Assert.Equal(100, field.MaxLength);
    }

    [Fact]
    public async Task Adding_a_field_with_a_stale_table_version_is_rejected()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => management.AddFieldAsync(
                table.Id,
                AddFieldRequest.Text(
                    fieldKey: "project_name",
                    label: "專案名稱",
                    required: true,
                    expectedVersion: 0)));

        Assert.Equal("table_version_conflict", error.Code);
        Assert.Empty((await management.GetAsync(table.Id))!.Fields);
    }

    [Fact]
    public async Task Required_field_with_a_default_value_backfills_existing_records()
    {
        var tenantId = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var tableManagement = new TableManagement(tables, records, tenant, clock);
        var recordManagement = new RecordManagement(
            records, tables, tenant, clock, new DynamicValidator());
        var table = await tableManagement.CreateAsync(new CreateTableRequest("專案"));
        using var emptyData = JsonDocument.Parse("{}");
        var record = await recordManagement.CreateAsync(
            table.Id,
            new CreateRecordRequest(emptyData.RootElement));
        using var defaultValue = JsonDocument.Parse("\"未命名\"");

        var updatedTable = await tableManagement.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text(
                "name",
                "名稱",
                required: true,
                expectedVersion: 1,
                defaultValue: defaultValue.RootElement));
        var updatedRecord = await recordManagement.GetAsync(table.Id, record.Id);

        Assert.Equal(2, updatedTable.SchemaVersion);
        Assert.NotNull(updatedRecord);
        Assert.Equal("未命名", updatedRecord.Data.GetProperty("name").GetString());
        Assert.Equal(2, updatedRecord.SchemaVersion);
        Assert.Equal(2, updatedRecord.Version);
    }

    [Fact]
    public async Task Field_key_must_be_unique_within_a_table()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));
        await management.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text(
                "project_name",
                "專案名稱",
                required: true,
                expectedVersion: 1));

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => management.AddFieldAsync(
                table.Id,
                AddFieldRequest.Text(
                    "project_name",
                    "另一個名稱",
                    required: false,
                    expectedVersion: 2)));

        Assert.Equal("duplicate_field_key", error.Code);
        Assert.Single((await management.GetAsync(table.Id))!.Fields);
    }

    [Fact]
    public async Task Field_rules_reject_an_inverted_range()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案"));

        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            management.AddFieldAsync(
                table.Id,
                AddFieldRequest.Text(
                    "code",
                    "代碼",
                    false,
                    expectedVersion: 1,
                    minLength: 10,
                    maxLength: 2)));

        Assert.Equal("invalid_field_rules", error.Code);
    }

    [Fact]
    public async Task Updating_one_end_of_a_field_range_cannot_invert_the_existing_range()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest(
            "專案",
            [AddFieldRequest.Number(
                "budget", "預算", false, expectedVersion: 1,
                NumberMode.Decimal, min: 0, max: 100)]));
        var field = Assert.Single(table.Fields);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            management.UpdateFieldAsync(
                table.Id,
                field.Id,
                new UpdateFieldRequest(null, table.Version, Min: 200)));

        Assert.Equal("invalid_field_rules", error.Code);
    }

    [Fact]
    public async Task Field_label_can_change_without_changing_its_key_or_schema_version()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));
        table = await management.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("project_name", "專案名稱", true, expectedVersion: 1));
        var fieldId = Assert.Single(table.Fields).Id;

        var updated = await management.UpdateFieldAsync(
            table.Id,
            fieldId,
            new UpdateFieldRequest("計畫名稱", ExpectedVersion: 2));

        var field = Assert.Single(updated.Fields);
        Assert.Equal("project_name", field.FieldKey);
        Assert.Equal("計畫名稱", field.Label);
        Assert.Equal(2, updated.SchemaVersion);
        Assert.Equal(3, updated.Version);
    }

    [Fact]
    public async Task Field_can_be_deactivated_and_advances_the_schema_version()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));
        table = await management.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("legacy_code", "舊代碼", false, expectedVersion: 1));
        var fieldId = Assert.Single(table.Fields).Id;

        var updated = await management.UpdateFieldAsync(
            table.Id,
            fieldId,
            new UpdateFieldRequest(Label: null, ExpectedVersion: 2, IsActive: false));

        Assert.False(Assert.Single(updated.Fields).IsActive);
        Assert.Equal(3, updated.SchemaVersion);
        Assert.Equal(3, updated.Version);
    }

    [Fact]
    public async Task Tightening_a_text_rule_is_rejected_when_existing_records_would_be_invalid()
    {
        var tenantId = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var tableManagement = new TableManagement(tables, records, tenant, clock);
        var recordManagement = new RecordManagement(
            records,
            tables,
            tenant,
            clock,
            new DynamicValidator());
        var table = await tableManagement.CreateAsync(new CreateTableRequest("專案追蹤"));
        table = await tableManagement.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("name", "名稱", true, expectedVersion: 1));
        using var data = JsonDocument.Parse("""{"name":"TableMint"}""");
        await recordManagement.CreateAsync(table.Id, new CreateRecordRequest(data.RootElement));
        var fieldId = Assert.Single(table.Fields).Id;

        var error = await Assert.ThrowsAsync<SchemaChangeRejectedException>(
            () => tableManagement.UpdateFieldAsync(
                table.Id,
                fieldId,
                new UpdateFieldRequest(
                    Label: null,
                    ExpectedVersion: 2,
                    MaxLength: 3)));

        Assert.Equal(1, error.AffectedRecordCount);
        Assert.Equal(2, (await tableManagement.GetAsync(table.Id))!.SchemaVersion);
    }

    [Fact]
    public async Task Used_select_option_can_be_deactivated_without_breaking_existing_record_updates()
    {
        var tenantId = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var tableManagement = new TableManagement(tables, records, tenant, clock);
        var recordManagement = new RecordManagement(
            records, tables, tenant, clock, new DynamicValidator());
        var table = await tableManagement.CreateAsync(new CreateTableRequest("案件"));
        table = await tableManagement.AddFieldAsync(
            table.Id,
            AddFieldRequest.SingleSelect(
                "status",
                "狀態",
                true,
                expectedVersion: 1,
                [new SelectOption("open", "開啟", true)]));
        using var data = JsonDocument.Parse("""{"status":"open"}""");
        var record = await recordManagement.CreateAsync(
            table.Id,
            new CreateRecordRequest(data.RootElement));
        var fieldId = Assert.Single(table.Fields).Id;

        var updatedTable = await tableManagement.UpdateFieldAsync(
            table.Id,
            fieldId,
            new UpdateFieldRequest(
                Label: null,
                ExpectedVersion: 2,
                Options: [new SelectOption("open", "開啟", false)]));
        using var emptyPatch = JsonDocument.Parse("{}");
        var updatedRecord = await recordManagement.UpdateAsync(
            table.Id,
            record.Id,
            new UpdateRecordRequest(emptyPatch.RootElement, ExpectedVersion: 1));

        Assert.False(Assert.Single(updatedTable.Fields).Options.Single().IsActive);
        Assert.Equal("open", updatedRecord.Data.GetProperty("status").GetString());
        await Assert.ThrowsAsync<RecordValidationException>(() =>
            recordManagement.CreateAsync(
                table.Id,
                new CreateRecordRequest(data.RootElement)));
    }

    [Fact]
    public async Task Optional_field_cannot_become_required_when_existing_record_has_no_value()
    {
        var tenantId = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var tableManagement = new TableManagement(tables, records, tenant, clock);
        var recordManagement = new RecordManagement(
            records, tables, tenant, clock, new DynamicValidator());
        var table = await tableManagement.CreateAsync(new CreateTableRequest("客戶"));
        table = await tableManagement.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("email", "信箱", false, expectedVersion: 1));
        using var data = JsonDocument.Parse("{}");
        await recordManagement.CreateAsync(table.Id, new CreateRecordRequest(data.RootElement));
        var fieldId = Assert.Single(table.Fields).Id;

        var error = await Assert.ThrowsAsync<SchemaChangeRejectedException>(() =>
            tableManagement.UpdateFieldAsync(
                table.Id,
                fieldId,
                new UpdateFieldRequest(
                    Label: null,
                    ExpectedVersion: 2,
                    Required: true)));

        Assert.Equal(1, error.AffectedRecordCount);
        Assert.False(Assert.Single((await tableManagement.GetAsync(table.Id))!.Fields).Required);
    }

    [Fact]
    public async Task Fields_can_be_reordered_by_providing_the_complete_id_order()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));
        table = await management.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("name", "名稱", true, expectedVersion: 1));
        table = await management.AddFieldAsync(
            table.Id,
            AddFieldRequest.Date("due_date", "到期日", false, expectedVersion: 2));
        var nameId = table.Fields.Single(field => field.FieldKey == "name").Id;
        var dueDateId = table.Fields.Single(field => field.FieldKey == "due_date").Id;

        var reordered = await management.ReorderFieldsAsync(
            table.Id,
            new ReorderFieldsRequest([dueDateId, nameId], ExpectedVersion: 3));

        Assert.Equal(["due_date", "name"], reordered.Fields.Select(field => field.FieldKey));
        Assert.Equal([0, 1], reordered.Fields.Select(field => field.Position));
        Assert.Equal(3, reordered.SchemaVersion);
        Assert.Equal(4, reordered.Version);
    }

    [Fact]
    public async Task Field_reorder_rejects_an_incomplete_id_order()
    {
        var management = new TableManagement(
            new InMemoryTableRepository(),
            new FixedTenantContext(Guid.NewGuid()),
            new FixedClock(DateTimeOffset.UtcNow));
        var table = await management.CreateAsync(new CreateTableRequest("專案追蹤"));
        table = await management.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("name", "名稱", true, expectedVersion: 1));

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => management.ReorderFieldsAsync(
                table.Id,
                new ReorderFieldsRequest([], ExpectedVersion: 2)));

        Assert.Equal("field_order_must_be_complete", error.Code);
    }

    [Fact]
    public async Task Valid_record_can_be_created_and_retrieved_from_its_table()
    {
        var tenantId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 17, 3, 0, 0, TimeSpan.Zero);
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(now);
        var tableManagement = new TableManagement(tables, tenant, clock);
        var recordManagement = new RecordManagement(
            records,
            tables,
            tenant,
            clock,
            new DynamicValidator());
        var table = await tableManagement.CreateAsync(
            new CreateTableRequest("專案追蹤"));
        table = await tableManagement.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text(
                "project_name",
                "專案名稱",
                required: true,
                expectedVersion: 1));
        using var document = JsonDocument.Parse(
            """{"project_name":"TableMint"}""");

        var created = await recordManagement.CreateAsync(
            table.Id,
            new CreateRecordRequest(document.RootElement));
        var retrieved = await recordManagement.GetAsync(table.Id, created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(tenantId, retrieved.TenantId);
        Assert.Equal(table.Id, retrieved.TableId);
        Assert.Equal(table.SchemaVersion, retrieved.SchemaVersion);
        Assert.Equal(1, retrieved.Version);
        Assert.Equal(now, retrieved.CreatedAt);
        Assert.Equal(
            "TableMint",
            retrieved.Data.GetProperty("project_name").GetString());
    }

    [Fact]
    public async Task Record_cannot_be_read_or_listed_from_another_tenant_context()
    {
        var ownerId = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = new FixedTenantContext(ownerId);
        var ownerTables = new TableManagement(tables, owner, clock);
        var ownerRecords = new RecordManagement(
            records, tables, owner, clock, new DynamicValidator());
        var table = await ownerTables.CreateAsync(new CreateTableRequest("私人資料"));
        using var data = JsonDocument.Parse("{}");
        var record = await ownerRecords.CreateAsync(
            table.Id,
            new CreateRecordRequest(data.RootElement));
        var outsiderRecords = new RecordManagement(
            records,
            tables,
            new FixedTenantContext(Guid.NewGuid()),
            clock,
            new DynamicValidator());

        Assert.Null(await outsiderRecords.GetAsync(table.Id, record.Id));
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            outsiderRecords.ListAsync(table.Id, new RecordListRequest()));
        Assert.Equal("table_not_found", error.Code);
    }

    [Fact]
    public async Task Record_patch_merges_with_existing_data_before_validation()
    {
        var tenantId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 17, 4, 0, 0, TimeSpan.Zero);
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(now);
        var tableManagement = new TableManagement(tables, tenant, clock);
        var recordManagement = new RecordManagement(
            records,
            tables,
            tenant,
            clock,
            new DynamicValidator());
        var table = await tableManagement.CreateAsync(new CreateTableRequest("專案追蹤"));
        table = await tableManagement.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("code", "代碼", true, expectedVersion: 1));
        table = await tableManagement.AddFieldAsync(
            table.Id,
            AddFieldRequest.Text("name", "名稱", true, expectedVersion: 2));
        using var original = JsonDocument.Parse("""{"code":"RL","name":"舊名稱"}""");
        var record = await recordManagement.CreateAsync(
            table.Id,
            new CreateRecordRequest(original.RootElement));
        using var patch = JsonDocument.Parse("""{"name":"新名稱"}""");

        var updated = await recordManagement.UpdateAsync(
            table.Id,
            record.Id,
            new UpdateRecordRequest(patch.RootElement, ExpectedVersion: 1));

        Assert.Equal(2, updated.Version);
        Assert.Equal("RL", updated.Data.GetProperty("code").GetString());
        Assert.Equal("新名稱", updated.Data.GetProperty("name").GetString());
        Assert.Equal(now, updated.UpdatedAt);
    }

    [Fact]
    public async Task Record_patch_rejects_non_object_data_as_validation_error()
    {
        var tenantId = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var tableManagement = new TableManagement(tables, tenant, clock);
        var recordManagement = new RecordManagement(
            records, tables, tenant, clock, new DynamicValidator());
        var table = await tableManagement.CreateAsync(new CreateTableRequest("專案"));
        using var original = JsonDocument.Parse("{}");
        var record = await recordManagement.CreateAsync(
            table.Id,
            new CreateRecordRequest(original.RootElement));
        using var invalidPatch = JsonDocument.Parse("[]");

        var error = await Assert.ThrowsAsync<RecordValidationException>(() =>
            recordManagement.UpdateAsync(
                table.Id,
                record.Id,
                new UpdateRecordRequest(invalidPatch.RootElement, record.Version)));

        Assert.Equal("invalid_record_data", Assert.Single(error.Errors).Code);
    }

    [Fact]
    public async Task Deleted_record_is_hidden_from_normal_reads()
    {
        var tenantId = Guid.NewGuid();
        var tables = new InMemoryTableRepository();
        var records = new InMemoryRecordRepository();
        var tenant = new FixedTenantContext(tenantId);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var tableManagement = new TableManagement(tables, tenant, clock);
        var recordManagement = new RecordManagement(
            records,
            tables,
            tenant,
            clock,
            new DynamicValidator());
        var table = await tableManagement.CreateAsync(new CreateTableRequest("專案追蹤"));
        using var document = JsonDocument.Parse("{}");
        var record = await recordManagement.CreateAsync(
            table.Id,
            new CreateRecordRequest(document.RootElement));

        await recordManagement.DeleteAsync(
            table.Id,
            record.Id,
            expectedVersion: 1);

        Assert.Null(await recordManagement.GetAsync(table.Id, record.Id));
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
            return Task.FromResult(
                record?.TableId == tableId ? record : null);
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
