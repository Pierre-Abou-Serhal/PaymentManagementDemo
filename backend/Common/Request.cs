using System.ComponentModel.DataAnnotations;

namespace PaymentManagement.Common;

public static class Request
{
    public sealed class BaseRequest
    {
        [Required]
        [StringLength(128)]
        [RegularExpression(@"[^\p{Cc}]+")]
        public string CorrelationId { get; set; } = string.Empty;

        [Required]
        public UserData UserData { get; set; } = null!;
    }

    public sealed class UserData
    {
        [Required]
        [StringLength(128)]
        public string UserName { get; set; } = string.Empty;
    }

    public abstract class ApiRequest
    {
        [Required]
        public BaseRequest BaseReq { get; set; } = null!;
    }

    public sealed class EmptyRequest : ApiRequest
    {

    }

    public sealed class SaveRoutingRuleRequest : ApiRequest
    {
        [Required]
        public string Currency { get; set; } = string.Empty;
        public decimal Threshold { get; set; }
    }

    public class InstitutionIdRequest : ApiRequest
    {
        [Range(1, int.MaxValue)]
        public int InstitutionId { get; set; }
    }

    public sealed class SaveResponseMappingRequest : InstitutionIdRequest
    {
        [Required]
        public string InputCode { get; set; } = string.Empty;
        [Required]
        public string OutputCode { get; set; } = string.Empty;
    }

    public sealed class GetFileReceiptsRequest : ApiRequest
    {
        public int? InstitutionId { get; set; }
        public string? State { get; set; }
        public string? Search { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    public class ReceiptIdRequest : ApiRequest
    {
        [Required]
        public Guid? ReceiptId { get; set; }
    }

    public sealed class CorrectInstitutionRequest : ReceiptIdRequest
    {
        [Range(1, int.MaxValue)]
        public int InstitutionId { get; set; }
        [Required]
        public string ExpectedBatchVersion { get; set; } = string.Empty;
    }

    public class BatchIdRequest : ApiRequest
    {
        [Required]
        public Guid? BatchId { get; set; }
    }

    public sealed class GetTransfersRequest : BatchIdRequest
    {
        public string? Category { get; set; }
        public string? Decision { get; set; }
        public Guid? GroupId { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    public sealed class TransferIdRequest : ApiRequest
    {
        [Required]
        public Guid? TransferId { get; set; }
    }

    public sealed class ProcessCategoryRequest : BatchIdRequest
    {
        [Required]
        public string Category { get; set; } = string.Empty;
    }

    public sealed class SaveTransferDecisionsRequest : BatchIdRequest
    {
        [Required]
        public List<DecisionItem> Items { get; set; } = [];
        [Required]
        public string Decision { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public sealed class FinalizeBatchRequest : BatchIdRequest
    {
        public Guid OperationId { get; set; }
        [Required]
        public string ExpectedBatchVersion { get; set; } = string.Empty;
    }

    public sealed class ReportIdRequest : ApiRequest
    {
        [Required]
        public Guid? ReportId { get; set; }
    }
}
