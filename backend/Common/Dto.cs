namespace PaymentManagement.Common;
public sealed class InstitutionRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
}

public sealed class RuleRow
{
    public string Currency { get; set; } = "";
    public decimal Threshold { get; set; }
    public int Revision { get; set; }
}

public sealed class MappingRow
{
    public string OutputCode { get; set; } = "";
    public int Revision { get; set; }
}

public sealed class ReceiptState
{
    public string ImportState { get; set; } = "";
    public string LocationState { get; set; } = "";
    public int IntakeInstitutionId { get; set; }
}

public sealed class PendingMove
{
    public Guid Id { get; set; }
    public string? CurrentPath { get; set; }
    public int InstitutionId { get; set; }
    public string ImportState { get; set; } = "";
}

public sealed class ReportDocument
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public Guid ReceiptId { get; set; }
    public Guid? BatchId { get; set; }
    public string Filename { get; set; } = "";
    public string MimeType { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
    public byte[] Hash { get; set; } = [];
    public string PublicationState { get; set; } = "";
    public string InstitutionCode { get; set; } = "";
}
