using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PaymentGateway.Api.Filters;

// Catches exceptions thrown by controller actions and returns a generic 500 ProblemDetails,
// so stack traces and internal details are never leaked to the client.
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ProblemDetailsFactory problemDetailsFactory, ILogger<GlobalExceptionFilter> logger)
    {
        _problemDetailsFactory = problemDetailsFactory;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        // A client disconnecting is not a server error; let the framework handle the aborted request.
        if (context.Exception is OperationCanceledException && context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        _logger.LogError(context.Exception, "Unhandled exception processing {Method} {Path}",
            context.HttpContext.Request.Method, context.HttpContext.Request.Path);

        var problem = _problemDetailsFactory.CreateProblemDetails(
            context.HttpContext,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred");

        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        context.ExceptionHandled = true;
    }
}