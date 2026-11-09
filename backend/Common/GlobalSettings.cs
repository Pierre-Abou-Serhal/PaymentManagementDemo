namespace PaymentManagement.Common;

public sealed class GlobalSettings
{
    public const string Section = "Demo";
    public string? Root { get; set; }
    public string OperatorName { get; set; } = "LocalDemoOperator";
    public string OwnBankCode { get; set; } = "0019";
    public int PollSeconds { get; set; } = 3;
    public int PublishSeconds { get; set; } = 3;
    public int MaxSourceBytes { get; set; } = 20_000_000;
    public bool RunWorkers { get; set; } = true;
}
