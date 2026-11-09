namespace PaymentManagement.Common;

public sealed class WorkflowException(int status, string message, object? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public object? Details { get; } = details;
}
