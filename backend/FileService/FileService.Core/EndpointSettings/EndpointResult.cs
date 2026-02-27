using System.Reflection;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using SharedService.SharedKernel;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace FileService.Core.EndpointSettings;

public class EndpointResult<TValue> : IResult, IEndpointMetadataProvider
{
    private readonly IResult _result;

    public EndpointResult(Result<TValue, Error> result)
    {
        _result = result.IsSuccess
            ? new SuccessResult<TValue>(result.Value)
            : new ErrorResult(result.Error);
    }

    public EndpointResult(Result<TValue, Failure> result)
    {
        _result = result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToIResult();
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        return _result.ExecuteAsync(httpContext);
    }

    public static implicit operator EndpointResult<TValue>(Result<TValue, Failure> result) => new(result);
    public static implicit operator EndpointResult<TValue>(Result<TValue, Error> result) => new(result);

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(200, typeof(Envelope<TValue>), ["application/json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(400, typeof(Envelope), ["application/json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(500, typeof(Envelope), ["application/json"]));
    }
}

public class EndpointResult : IResult, IEndpointMetadataProvider
{
    private readonly IResult _result;

    public EndpointResult(UnitResult<Error> result)
    {
        _result = result.IsSuccess
            ? new SuccessResult()
            : new ErrorResult(result.Error);
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        return _result.ExecuteAsync(httpContext);
    }

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(200, typeof(Envelope), ["application/json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(400, typeof(Envelope), ["application/json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(500, typeof(Envelope), ["application/json"]));
    }

    public static implicit operator EndpointResult(UnitResult<Error> result) => new(result);
}

public static class FailureExtensions
{
    public static IResult ToIResult(this Failure failure)
    {
        if (failure == null || !failure.Any())
        {
            return Results.StatusCode(500);
        }

        var distinctErrorTypes = failure
            .Select(x => x.Type)
            .Distinct()
            .ToList();

        if (distinctErrorTypes.Count == 0)
        {
            return Results.Json(failure, statusCode: 500);
        }

        int statusCode = distinctErrorTypes.Count > 1
            ? 500
            : GetStatusCodeFromErrorType((ErrorType)distinctErrorTypes.First()!);

        return Results.Json(failure, statusCode: statusCode);
    }

    private static int GetStatusCodeFromErrorType(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.VALIDATION => 400,
            ErrorType.NOT_FOUND => 404,
            ErrorType.CONFLICT => 409,
            _ => 500
        };
}