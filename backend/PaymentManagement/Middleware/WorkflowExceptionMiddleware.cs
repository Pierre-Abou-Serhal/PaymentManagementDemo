using PaymentManagement.BLL.Iso20022;
using PaymentManagement.Common;

namespace PaymentManagement.Middleware;
public sealed class WorkflowExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (WorkflowException ex)
        {
            context.Response.StatusCode = ex.Status;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message, details = ex.Details });
        }
        catch (IsoException ex)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await context.Response.WriteAsJsonAsync(new { error = ex.Kind, details = ex.Errors });
        }
    }
}
