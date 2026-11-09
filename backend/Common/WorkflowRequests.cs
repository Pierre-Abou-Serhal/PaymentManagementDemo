namespace PaymentManagement.Common;

public sealed record DecisionItem(Guid Id, string Version);
public sealed record DecisionRequest(List<DecisionItem> Items, string Decision, string? Reason);
public sealed record MappingRequest(string InputCode, string OutputCode);
public sealed record RuleRequest(string Currency, decimal Threshold);
public sealed record InstitutionRequest(int InstitutionId, string ExpectedBatchVersion);
public sealed record FinalizeRequest(Guid OperationId, string ExpectedBatchVersion);
