using Microsoft.AspNetCore.Mvc;
using TableMint.Application.Records;

namespace TableMint.WebApi.Controllers;

[ApiController]
[Route("api/tables/{tableId:guid}/records")]
public sealed class RecordsController(RecordManagement records) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<RecordPageView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecordPageView>> List(
        Guid tableId,
        [FromQuery] RecordListRequest request,
        CancellationToken cancellationToken) =>
        Ok(await records.ListAsync(tableId, request, cancellationToken));

    [HttpPost]
    [ProducesResponseType<RecordView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecordView>> Create(
        Guid tableId,
        CreateRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = await records.CreateAsync(
            tableId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { tableId, recordId = record.Id },
            record);
    }

    [HttpGet("{recordId:guid}")]
    [ProducesResponseType<RecordView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordView>> Get(
        Guid tableId,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var record = await records.GetAsync(
            tableId,
            recordId,
            cancellationToken);

        return record is null ? NotFound() : Ok(record);
    }

    [HttpPatch("{recordId:guid}")]
    [ProducesResponseType<RecordView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecordView>> Update(
        Guid tableId,
        Guid recordId,
        UpdateRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = await records.UpdateAsync(
            tableId,
            recordId,
            request,
            cancellationToken);

        return Ok(record);
    }

    [HttpDelete("{recordId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid tableId,
        Guid recordId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        await records.DeleteAsync(
            tableId,
            recordId,
            expectedVersion,
            cancellationToken);

        return NoContent();
    }
}
