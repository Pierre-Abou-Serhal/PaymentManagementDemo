using System.Text.Json.Serialization;

namespace PaymentManagement.Common;

public sealed record HeaderField(string Path, string Value);

public sealed record GroupHeaderView(
    bool Available,
    string? Definition = null,
    string? MessageId = null,
    string? CreatedAt = null,
    string? NumberOfTransactions = null,
    string? ControlSum = null,
    string? InitiatingParty = null,
    string? ForwardingAgent = null,
    string? BatchBooking = null,
    string? Grouping = null,
    IReadOnlyList<HeaderField>? Fields = null,
    string? Xml = null,
    string? Notice = null);

public sealed record BatchView(
    BatchInfo Batch,
    IEnumerable<BatchCategory> Categories,
    IEnumerable<CurrencyTotal> CurrencyTotals,
    IReadOnlyList<string> Blockers)
{
    public bool CanFinalize => Blockers.Count == 0;
}

public sealed record Remittance(string Kind, string Value, string Xml);

public sealed class InstitutionSummary
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class RoutingRule
{
    public string Currency { get; set; } = string.Empty;
    public string Threshold { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string Version { get; set; } = string.Empty;
}

public sealed class ResponseMapping
{
    public string InputCode { get; set; } = string.Empty;
    public string OutputCode { get; set; } = string.Empty;
    public int Revision { get; set; }
}

public abstract class ReceiptInfo
{
    public Guid Id { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string IntakeInstitution { get; set; } = string.Empty;
    public string EffectiveInstitution { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime ReceivedUtc { get; set; }
    public string ImportState { get; set; } = string.Empty;
    public string LocationState { get; set; } = string.Empty;
    public string? ErrorType { get; set; }
    public Guid? DuplicateOf { get; set; }
    public Guid? BatchId { get; set; }
    public string? DetectedDefinition { get; set; }
    [JsonIgnore]
    public byte[] SourceBytes { get; set; } = [];
    public GroupHeaderView GroupHeader { get; set; } = new(false);
}

public sealed class ReceiptSummary : ReceiptInfo
{
    public string? BatchState { get; set; }
    public string? OutputCode { get; set; }
    public int TransferCount { get; set; }
    public int PendingCount { get; set; }
}

public sealed class ReceiptDetail : ReceiptInfo
{
    public string OriginalPath { get; set; } = string.Empty;
    public string? CurrentPath { get; set; }
    public long ByteLength { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public string? ValidationJson { get; set; }
    public Guid? TechnicalDocumentId { get; set; }
    public string? TechnicalPublicationState { get; set; }
    public string Version { get; set; } = string.Empty;
}

public sealed class BatchInfo
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string InputCode { get; set; } = string.Empty;
    public string? OutputCode { get; set; }
    public int? MappingRevision { get; set; }
    public string? OriginalMessageId { get; set; }
    public string? OriginalCreation { get; set; }
    public string State { get; set; } = string.Empty;
    public int CurrentRevision { get; set; }
    public DateTime? AssignmentLockedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Version { get; set; } = string.Empty;
    public int TransferCount { get; set; }
    public int PendingCount { get; set; }
    public int SystemRejectedCount { get; set; }
    public int OperatorRejectedCount { get; set; }
}

public sealed class BatchCategory
{
    public string Category { get; set; } = string.Empty;
    public int TransferCount { get; set; }
    public string ProcessingState { get; set; } = string.Empty;
    public DateTime? ProcessedUtc { get; set; }
    public string? ProcessedActor { get; set; }
}

public sealed class CurrencyTotal
{
    public string? Currency { get; set; }
    public string Amount { get; set; } = string.Empty;
}

public sealed class PaymentGroup
{
    public Guid Id { get; set; }
    public int SourceOrdinal { get; set; }
    public string? PaymentInfoId { get; set; }
    public string? DebtorName { get; set; }
    public string? DebtorAccount { get; set; }
    public string? DebtorAccountKind { get; set; }
    public string? DebtorCurrency { get; set; }
    public string? DebtorAgent { get; set; }
    public string? ExecutionDate { get; set; }
    public int TransferCount { get; set; }
}

public abstract class TransferInfo
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public int GroupOrdinal { get; set; }
    public int SourceOrdinal { get; set; }
    public string? PaymentInfoId { get; set; }
    public string? DebtorAccount { get; set; }
    public string? InstructionId { get; set; }
    public string? EndToEndId { get; set; }
    public string? CreditorName { get; set; }
    public string? CreditorAccount { get; set; }
    public string? CreditorAccountKind { get; set; }
    public string? TransferCurrency { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string DecisionSource { get; set; } = string.Empty;
    public string? DecisionReason { get; set; }
    public string? RoutingCode { get; set; }
    public string? RoutingExplanation { get; set; }
    public string Version { get; set; } = string.Empty;
}

public sealed class TransferSummary : TransferInfo
{
    public string? Amount { get; set; }
    public string? Currency { get; set; }
    public string AmountBasis { get; set; } = string.Empty;
}

public sealed class TransferDetail : TransferInfo
{
    public Guid BatchId { get; set; }
    public string? DebtorName { get; set; }
    public string? CreditorAgent { get; set; }
    public string? InstructedAmount { get; set; }
    public string? InstructedCurrency { get; set; }
    public string? EquivalentAmount { get; set; }
    public string? EquivalentCurrency { get; set; }
    public string? AccountCurrency { get; set; }
    public string? RoutingAmount { get; set; }
    public string? IbanCountry { get; set; }
    public string? IbanBankCode { get; set; }
    public int? RuleRevision { get; set; }
    public string? DecisionActor { get; set; }
    public DateTime? DecisionUtc { get; set; }
    public string SourceXml { get; set; } = string.Empty;
    public IEnumerable<Remittance> Remittances { get; set; } = [];
    public IEnumerable<AuditEntry> History { get; set; } = [];
}

public sealed class ReportSummary
{
    public Guid Id { get; set; }
    public int Revision { get; set; }
    public string IsoDefinition { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public DateTime GeneratedUtc { get; set; }
    public string PublicationState { get; set; } = string.Empty;
    public DateTime? PublishedUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}

public class AuditEntry
{
    public DateTime OccurredUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
}

public sealed class BatchAuditEntry : AuditEntry
{
    public long Id { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? TransferId { get; set; }
    public Guid? DocumentId { get; set; }
}
