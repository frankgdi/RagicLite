using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TableMint.Application.Errors;

namespace TableMint.WebApi.Runtime;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            RequestValidationException error => CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                error.Message,
                error.Code),
            NotFoundException error => CreateProblem(
                StatusCodes.Status404NotFound,
                "Not found",
                error.Message,
                error.Code),
            ConflictException error => CreateProblem(
                StatusCodes.Status409Conflict,
                "Conflict",
                error.Message,
                error.Code),
            RecordValidationException error => CreateRecordValidationProblem(error),
            SchemaChangeRejectedException error => CreateSchemaChangeProblem(error),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
            });
    }

    private static ProblemDetails CreateProblem(
        int status,
        string title,
        string detail,
        string code)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    private static ProblemDetails CreateRecordValidationProblem(
        RecordValidationException exception)
    {
        var problem = CreateProblem(
            StatusCodes.Status422UnprocessableEntity,
            "Record validation failed",
            exception.Message,
            "record_validation_failed");
        problem.Extensions["errors"] = exception.Errors;
        return problem;
    }

    private static ProblemDetails CreateSchemaChangeProblem(
        SchemaChangeRejectedException exception)
    {
        var problem = CreateProblem(
            StatusCodes.Status409Conflict,
            "Schema change rejected",
            exception.Message,
            "schema_change_would_invalidate_records");
        problem.Extensions["affectedRecordCount"] = exception.AffectedRecordCount;
        return problem;
    }
}
