using System.Net;
using Microsoft.Extensions.Options;
using PaymentManagement.Common;
#if TEAM_CORRELATION_LOGGING
using static NLog.NLogUtil;
#endif

namespace PaymentManagement.Filters;

public sealed class CorrelationLog(ILogger<CorrelationLog> logger, IOptionsMonitor<GlobalSettings> settings)
{
    public IDisposable? BeginScope(string action, string correlationId)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Action"] = action,
            ["UserName"] = settings.CurrentValue.OperatorName
        });
    }

    public void Request(string action, string correlationId)
    {
#if TEAM_CORRELATION_LOGGING
        LogInfo("Payment action received", Info(action, correlationId, RequestDirection.Request));
#else
        logger.LogInformation("Payment action {Action} received ({CorrelationId}) for {Operator}",
            action, correlationId, settings.CurrentValue.OperatorName);
#endif
    }

    public void Response(string action, string correlationId, HttpStatusCode status, Exception? exception = null)
    {
#if TEAM_CORRELATION_LOGGING
        var info = Info(action, correlationId, RequestDirection.Response);
        var message = $"Payment action completed with status {(int)status}";
        if (status == HttpStatusCode.OK)
        {
            LogInfo(message, info);
        }
        else
        {
            LogError(message, info);
        }
#else
        var level = status >= HttpStatusCode.InternalServerError ? LogLevel.Error : LogLevel.Information;
        logger.Log(level, "Payment action {Action} completed with status {StatusCode} ({CorrelationId})",
            action, (int)status, correlationId);
#endif
        if (status >= HttpStatusCode.InternalServerError && exception is not null)
        {
            logger.LogError(exception, "Unhandled payment action error in {Action} ({CorrelationId})", action, correlationId);
        }
    }

#if TEAM_CORRELATION_LOGGING
    private CorrelationInfo Info(string action, string correlationId, RequestDirection direction)
    {
        return new CorrelationInfo
        {
            CorrelationId = correlationId,
            RDirection = direction,
            RequestURL = action,
            UserName = settings.CurrentValue.OperatorName
        };
    }
#endif
}
