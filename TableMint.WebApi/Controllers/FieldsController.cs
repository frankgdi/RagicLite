using Microsoft.AspNetCore.Mvc;
using TableMint.Application.Errors;
using TableMint.Application.Tables;
using TableMint.Domain.Tables;
using TableMint.WebApi.Contracts;

namespace TableMint.WebApi.Controllers;

[ApiController]
[Route("api/tables/{tableId:guid}/fields")]
public sealed class FieldsController(TableManagement tables) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TableView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TableView>> Create(
        Guid tableId,
        CreateFieldRequest request,
        CancellationToken cancellationToken)
    {
        var table = await tables.AddFieldAsync(
            tableId,
            ToApplicationRequest(request),
            cancellationToken);

        return Ok(table);
    }

    [HttpPatch("{fieldId:guid}")]
    [ProducesResponseType<TableView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TableView>> Update(
        Guid tableId,
        Guid fieldId,
        UpdateFieldRequest request,
        CancellationToken cancellationToken)
    {
        var table = await tables.UpdateFieldAsync(
            tableId,
            fieldId,
            request,
            cancellationToken);

        return Ok(table);
    }

    [HttpPost("reorder")]
    [ProducesResponseType<TableView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TableView>> Reorder(
        Guid tableId,
        ReorderFieldsRequest request,
        CancellationToken cancellationToken)
    {
        var table = await tables.ReorderFieldsAsync(
            tableId,
            request,
            cancellationToken);

        return Ok(table);
    }

    internal static AddFieldRequest ToApplicationRequest(
        CreateFieldRequest request,
        int? expectedVersion = null) =>
        request.Type switch
        {
            FieldType.Text => AddFieldRequest.Text(
                request.FieldKey,
                request.Label,
                request.Required,
                expectedVersion ?? request.ExpectedVersion,
                request.MinLength,
                request.MaxLength,
                request.Format,
                request.DefaultValue),
            FieldType.Number => AddFieldRequest.Number(
                request.FieldKey,
                request.Label,
                request.Required,
                expectedVersion ?? request.ExpectedVersion,
                request.NumberMode ?? NumberMode.Decimal,
                request.Min,
                request.Max,
                request.DefaultValue),
            FieldType.Date => AddFieldRequest.Date(
                request.FieldKey,
                request.Label,
                request.Required,
                expectedVersion ?? request.ExpectedVersion,
                request.DefaultValue),
            FieldType.SingleSelect => AddFieldRequest.SingleSelect(
                request.FieldKey,
                request.Label,
                request.Required,
                expectedVersion ?? request.ExpectedVersion,
                request.Options ?? [],
                request.DefaultValue),
            _ => throw new RequestValidationException(
                "unsupported_field_type",
                "This field type is not available through the endpoint yet."),
        };
}
