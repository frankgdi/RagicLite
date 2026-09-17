using System.Text.Json;
using TableMint.Application.Records;
using TableMint.Application.Tables;
using TableMint.Domain.Tables;

namespace TableMint.WebApi.Runtime;

public sealed class DemoDataSeeder(
    TableManagement tables,
    RecordManagement records)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await tables.ListAsync(cancellationToken);
        if (existing.Any(table => table.Name == "專案追蹤 Demo"))
        {
            return;
        }

        var table = await tables.CreateAsync(
            new CreateTableRequest(
                "專案追蹤 Demo",
                [
                    AddFieldRequest.Text("name", "專案名稱", true, 1, maxLength: 100),
                    AddFieldRequest.Number(
                        "budget", "預算", false, 2, NumberMode.Decimal, min: 0),
                    AddFieldRequest.Date("due_date", "到期日", false, 3),
                    AddFieldRequest.SingleSelect(
                        "status",
                        "狀態",
                        true,
                        4,
                        [
                            new SelectOption("planned", "規劃中", true),
                            new SelectOption("active", "進行中", true),
                            new SelectOption("done", "已完成", true),
                        ]),
                ]),
            cancellationToken);

        var data = JsonSerializer.SerializeToElement(new
        {
            name = "TableMint MVP",
            budget = 100000m,
            due_date = "2026-12-31",
            status = "active",
        });
        await records.CreateAsync(
            table.Id,
            new CreateRecordRequest(data),
            cancellationToken);
    }
}
