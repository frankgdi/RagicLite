namespace TableMint.Application.Tables;

public sealed record UpdateTableRequest(string Name, int ExpectedVersion);
