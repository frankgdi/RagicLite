using System.Text.Json;

namespace TableMint.Application.Records;

public sealed record UpdateRecordRequest(
    JsonElement Data,
    int ExpectedVersion);
