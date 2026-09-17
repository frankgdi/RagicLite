using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TableMint.Application.Abstractions;
using TableMint.Application.Errors;
using TableMint.Infrastructure.Persistence;
using TableMint.Domain.Tables;
using DomainRecord = TableMint.Domain.Tables.Record;

namespace TableMint.Infrastructure.Tests.Persistence;

public sealed class PostgresRecordRepositoryTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TABLEMINT_TEST_CONNECTION") ??
        "Host=localhost;Database=tablemint_test;Username=hui";

    [Fact]
    public async Task Json_record_can_be_written_and_read_through_the_repository()
    {
        var tenantId = Guid.NewGuid();
        var table = Table.Create(
            Guid.NewGuid(),
            tenantId,
            "專案追蹤",
            DateTimeOffset.UtcNow);
        using var document = JsonDocument.Parse(
            """{"project_name":"TableMint","score":98.5}""");
        var record = DomainRecord.Create(
            Guid.NewGuid(),
            tenantId,
            table.Id,
            document.RootElement,
            schemaVersion: 1,
            DateTimeOffset.UtcNow);

        await using (var writeContext = CreateContext())
        {
            await writeContext.Database.MigrateAsync();
            await new PostgresTableRepository(writeContext).AddAsync(table, default);
            await new PostgresRecordRepository(writeContext).AddAsync(record, default);
        }

        await using var readContext = CreateContext();
        var retrieved = await new PostgresRecordRepository(readContext).GetAsync(
            tenantId,
            table.Id,
            record.Id,
            default);

        Assert.NotNull(retrieved);
        Assert.Equal(
            "TableMint",
            retrieved.Data.GetProperty("project_name").GetString());
        Assert.Equal(98.5m, retrieved.Data.GetProperty("score").GetDecimal());
    }

    [Fact]
    public async Task Table_schema_can_be_written_and_read_through_the_repository()
    {
        var tenantId = Guid.NewGuid();
        var table = Table.Create(
            Guid.NewGuid(),
            tenantId,
            "專案追蹤",
            DateTimeOffset.UtcNow);
        table.AddField(
            FieldDefinition.CreateText(
                Guid.NewGuid(),
                "project_name",
                "專案名稱",
                required: true,
                minLength: 2,
                maxLength: 100,
                position: 0),
            DateTimeOffset.UtcNow);

        await using (var writeContext = CreateContext())
        {
            await writeContext.Database.MigrateAsync();
            await new PostgresTableRepository(writeContext).AddAsync(table, default);
        }

        await using var readContext = CreateContext();
        var retrieved = await new PostgresTableRepository(readContext).GetAsync(
            tenantId,
            table.Id,
            default);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.SchemaVersion);
        Assert.Equal(2, retrieved.Version);
        var field = Assert.Single(retrieved.Fields);
        Assert.Equal("project_name", field.FieldKey);
        Assert.Equal(2, field.MinLength);
        Assert.Equal(100, field.MaxLength);
    }

    [Fact]
    public async Task Records_can_be_sorted_by_creation_time()
    {
        var tenantId = Guid.NewGuid();
        var table = Table.Create(Guid.NewGuid(), tenantId, "排序測試", DateTimeOffset.UtcNow);
        using var data = JsonDocument.Parse("{}");
        var older = DomainRecord.Create(
            Guid.NewGuid(), tenantId, table.Id, data.RootElement, 1,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = DomainRecord.Create(
            Guid.NewGuid(), tenantId, table.Id, data.RootElement, 1,
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

        await using (var writeContext = CreateContext())
        {
            await writeContext.Database.MigrateAsync();
            var repository = new PostgresRecordRepository(writeContext);
            await new PostgresTableRepository(writeContext).AddAsync(table, default);
            await repository.AddAsync(newer, default);
            await repository.AddAsync(older, default);
        }

        await using var readContext = CreateContext();
        var page = await new PostgresRecordRepository(readContext).QueryAsync(
            tenantId,
            table.Id,
            new RecordQuery(1, 10, RecordSortBy.CreatedAt, SortDirection.Ascending),
            default);

        Assert.Equal([older.Id, newer.Id], page.Items.Select(record => record.Id));
    }

    [Fact]
    public async Task Number_filter_uses_json_number_equality_not_text_equality()
    {
        var tenantId = Guid.NewGuid();
        var table = Table.Create(Guid.NewGuid(), tenantId, "篩選測試", DateTimeOffset.UtcNow);
        using var numberData = JsonDocument.Parse("""{"score":12}""");
        using var textData = JsonDocument.Parse("""{"score":"12"}""");
        var numberRecord = DomainRecord.Create(
            Guid.NewGuid(), tenantId, table.Id, numberData.RootElement, 1, DateTimeOffset.UtcNow);
        var textRecord = DomainRecord.Create(
            Guid.NewGuid(), tenantId, table.Id, textData.RootElement, 1, DateTimeOffset.UtcNow);

        await using (var writeContext = CreateContext())
        {
            await writeContext.Database.MigrateAsync();
            var repository = new PostgresRecordRepository(writeContext);
            await new PostgresTableRepository(writeContext).AddAsync(table, default);
            await repository.AddAsync(numberRecord, default);
            await repository.AddAsync(textRecord, default);
        }

        await using var readContext = CreateContext();
        var page = await new PostgresRecordRepository(readContext).QueryAsync(
            tenantId,
            table.Id,
            new RecordQuery(
                1,
                10,
                FilterFieldKey: "score",
                FilterValue: JsonSerializer.SerializeToElement(12m)),
            default);

        Assert.Equal(numberRecord.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Stale_record_update_is_rejected_by_postgres_concurrency_control()
    {
        var tenantId = Guid.NewGuid();
        var table = Table.Create(Guid.NewGuid(), tenantId, "並行測試", DateTimeOffset.UtcNow);
        using var initialData = JsonDocument.Parse("""{"value":"initial"}""");
        var record = DomainRecord.Create(
            Guid.NewGuid(), tenantId, table.Id, initialData.RootElement, 1, DateTimeOffset.UtcNow);
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
            await new PostgresTableRepository(setup).AddAsync(table, default);
            await new PostgresRecordRepository(setup).AddAsync(record, default);
        }

        await using var staleContext = CreateContext();
        var staleRepository = new PostgresRecordRepository(staleContext);
        var stale = (await staleRepository.GetAsync(tenantId, table.Id, record.Id, default))!;
        await using (var winningContext = CreateContext())
        {
            var winningRepository = new PostgresRecordRepository(winningContext);
            var winning = (await winningRepository.GetAsync(
                tenantId, table.Id, record.Id, default))!;
            using var winningData = JsonDocument.Parse("""{"value":"winner"}""");
            winning.Update(winningData.RootElement, 1, DateTimeOffset.UtcNow);
            await winningRepository.UpdateAsync(winning, default);
        }
        using var staleData = JsonDocument.Parse("""{"value":"stale"}""");
        stale.Update(staleData.RootElement, 1, DateTimeOffset.UtcNow);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => staleRepository.UpdateAsync(stale, default));

        Assert.Equal("record_version_conflict", error.Code);
    }

    [Fact]
    public async Task Stale_table_update_is_rejected_by_postgres_concurrency_control()
    {
        var tenantId = Guid.NewGuid();
        var table = Table.Create(Guid.NewGuid(), tenantId, "原名稱", DateTimeOffset.UtcNow);
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
            await new PostgresTableRepository(setup).AddAsync(table, default);
        }

        await using var staleContext = CreateContext();
        var staleRepository = new PostgresTableRepository(staleContext);
        var stale = (await staleRepository.GetAsync(tenantId, table.Id, default))!;
        await using (var winningContext = CreateContext())
        {
            var winningRepository = new PostgresTableRepository(winningContext);
            var winning = (await winningRepository.GetAsync(tenantId, table.Id, default))!;
            winning.Rename("winner", DateTimeOffset.UtcNow);
            await winningRepository.UpdateAsync(winning, default);
        }
        stale.Rename("stale", DateTimeOffset.UtcNow);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => staleRepository.UpdateAsync(stale, default));

        Assert.Equal("table_version_conflict", error.Code);
    }

#pragma warning disable EF1002 // Schema name is generated from Guid and cannot contain SQL syntax.
    [Fact]
    public async Task Migration_rebuilds_an_empty_postgres_schema()
    {
        var schema = $"migration_{Guid.NewGuid():N}";
        await using var administration = CreateContext();
        await administration.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql($"{ConnectionString};Search Path={schema}")
                .Options;
            await using var isolated = new AppDbContext(options);
            await isolated.Database.MigrateAsync();
            var tenantId = Guid.NewGuid();
            var table = Table.Create(
                Guid.NewGuid(), tenantId, "Migration 驗收", DateTimeOffset.UtcNow);

            var repository = new PostgresTableRepository(isolated);
            await repository.AddAsync(table, default);
            var retrieved = await repository.GetAsync(tenantId, table.Id, default);

            Assert.NotNull(retrieved);
            Assert.Equal("Migration 驗收", retrieved.Name);
        }
        finally
        {
            await administration.Database.ExecuteSqlRawAsync(
                $"DROP SCHEMA \"{schema}\" CASCADE");
        }
    }
#pragma warning restore EF1002

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
