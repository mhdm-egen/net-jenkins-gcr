using FluentValidation;

namespace Publisher.Api.Endpoints;

internal static class EndpointHelpers
{
    /// <summary>
    /// Validates the command with FluentValidation, then runs it — mapping validation failures to
    /// a problem-details 400 and domain <see cref="InvalidOperationException"/>s to 409.
    /// </summary>
    internal static async Task<IResult> ValidateAndRun<TCommand>(
        IValidator<TCommand> validator,
        TCommand cmd,
        CancellationToken ct,
        Func<Task<IResult>> run)
    {
        var result = await validator.ValidateAsync(cmd, ct);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());
            return Results.ValidationProblem(errors);
        }

        try
        {
            return await run();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(title: "Invalid operation", detail: ex.Message, statusCode: 409);
        }
    }
}
