namespace Cratebase.Api.Http;

public static class EndpointErrors
{
    private const string DeleteConfirmationRequiredCode = "delete.confirmation_required";
    private const string DeleteConfirmationRequiredMessage = "Delete confirmation is required";

    public static IResult BadRequest(string code, string message)
    {
        return Results.BadRequest(new ErrorResponse(code, message));
    }

    public static IResult Conflict(string code, string message)
    {
        return Results.Conflict(new ErrorResponse(code, message));
    }

    public static IResult DeleteConfirmationRequired()
    {
        return BadRequest(DeleteConfirmationRequiredCode, DeleteConfirmationRequiredMessage);
    }

    public static IResult NotFound(string code, string message)
    {
        return Results.NotFound(new ErrorResponse(code, message));
    }

    public static IResult Unauthorized(string code, string message)
    {
        return Results.Json(new ErrorResponse(code, message), statusCode: StatusCodes.Status401Unauthorized);
    }
}
