using Microsoft.AspNetCore.Mvc;
using TableMint.Application.Tables;
using TableMint.WebApi.Contracts;

namespace TableMint.WebApi.Controllers;

[ApiController]
[Route("api/tables")]
public sealed class TablesController(TableManagement tables) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TableView>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TableView>> Create(
        CreateTableContract request,
        CancellationToken cancellationToken)
    {
        var fields = request.Fields?
            .Select((field, index) =>
                FieldsController.ToApplicationRequest(field, expectedVersion: index + 1))
            .ToArray();
        var table = await tables.CreateAsync(
            new TableMint.Application.Tables.CreateTableRequest(request.Name, fields),
            cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { tableId = table.Id },
            table);
    }

    [HttpGet("{tableId:guid}")]
    [ProducesResponseType<TableView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TableView>> Get(
        Guid tableId,
        CancellationToken cancellationToken)
    {
        var table = await tables.GetAsync(tableId, cancellationToken);

        return table is null ? NotFound() : Ok(table);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TableView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TableView>>> List(
        CancellationToken cancellationToken) =>
        Ok(await tables.ListAsync(cancellationToken));

    [HttpPatch("{tableId:guid}")]
    [ProducesResponseType<TableView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TableView>> Update(
        Guid tableId,
        UpdateTableRequest request,
        CancellationToken cancellationToken) =>
        Ok(await tables.UpdateAsync(tableId, request, cancellationToken));

    [HttpDelete("{tableId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid tableId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        await tables.DeleteAsync(tableId, expectedVersion, cancellationToken);
        return NoContent();
    }
}
