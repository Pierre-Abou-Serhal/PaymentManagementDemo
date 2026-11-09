using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using PaymentManagement.BLL.Iso20022;
using PaymentManagement.Common;
using static PaymentManagement.Common.Request;
using static PaymentManagement.Common.Response;

namespace PaymentManagement.Filters;

public sealed class PaymentActionFilter(CorrelationLog log) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values.OfType<ApiRequest>().SingleOrDefault();
        var suppliedId = request?.BaseReq?.CorrelationId;
        var correlationId = !string.IsNullOrWhiteSpace(suppliedId) && suppliedId.Length <= 128 &&
            !suppliedId.Any(char.IsControl)
            ? suppliedId
            : context.HttpContext.TraceIdentifier;
        var action = ((ControllerActionDescriptor)context.ActionDescriptor).ActionName;

        using var scope = log.BeginScope(action, correlationId);
        log.Request(action, correlationId);

        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "Invalid request value." : error.ErrorMessage)
                .ToArray();
            context.Result = Error(request, correlationId, HttpStatusCode.BadRequest, errors);
            log.Response(action, correlationId, HttpStatusCode.BadRequest);
            return;
        }

        var executed = await next();
        if (executed.Exception is OperationCanceledException && context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        if (executed.Exception is { } exception)
        {
            executed.Result = exception switch
            {
                WorkflowException workflow => Error(request, correlationId, (HttpStatusCode)workflow.Status,
                    [workflow.Message], workflow.Details),
                IsoException iso => Error(request, correlationId, HttpStatusCode.UnprocessableEntity,
                    [iso.Kind], iso.Errors),
                _ => Error(request, correlationId, HttpStatusCode.InternalServerError,
                    ["The payment operation could not be completed. Use the correlation ID when reporting this error."])
            };
            executed.ExceptionHandled = true;
        }

        var status = HttpStatusCode.OK;
        if (executed.Result is ObjectResult { Value: ApiResponse response })
        {
            if (response is ErrorResponse)
            {
                status = response.BaseResp.StatusCode;
            }
            else
            {
                response.Req = request;
                response.BaseResp = new BaseResponse { CorrelationId = correlationId };
            }
        }

        log.Response(action, correlationId, status, executed.Exception);
    }

    private static OkObjectResult Error(
        ApiRequest? request, string correlationId, HttpStatusCode status, string[] errors, object? details = null)
    {
        return new OkObjectResult(new ErrorResponse
        {
            Req = request,
            BaseResp = new BaseResponse { CorrelationId = correlationId, StatusCode = status, Errors = errors },
            Details = details
        });
    }
}
