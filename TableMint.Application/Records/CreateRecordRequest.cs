using System.Text.Json;

namespace TableMint.Application.Records;

public sealed record CreateRecordRequest(JsonElement Data);
