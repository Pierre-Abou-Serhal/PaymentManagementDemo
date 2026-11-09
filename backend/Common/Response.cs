using System.Net;
using System.Text.Json;

namespace PaymentManagement.Common;

public static class Response
{
    public sealed class BaseResponse
    {
        public string CorrelationId { get; init; } = string.Empty;
        public string Ticket { get; init; } = string.Empty;
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public IReadOnlyList<string> Errors { get; init; } = [];
        public string ReturnCode => StatusCode == HttpStatusCode.OK ? "0x0000" : $"0x{(int)StatusCode:X4}";
        public string ReturnDescription => Errors.Count == 0 ? string.Empty : Errors[0];
    }

    public class ApiResponse
    {
        public object? Req { get; set; }
        public BaseResponse BaseResp { get; set; } = new();
    }

    public sealed class ErrorResponse : ApiResponse
    {
        public object? Details { get; init; }
    }

    public sealed record ValidationResult(string? ErrorType, string? ValidationJson, Guid? DuplicateOf);

    public sealed class GetAllInstitutionsResponse : ApiResponse
    {
        public IEnumerable<InstitutionSummary> Institutions { get; init; } = [];
    }

    public sealed class GetIsoDefinitionsResponse : ApiResponse
    {
        public JsonElement Definitions { get; init; }
    }

    public sealed class GetRoutingRulesResponse : ApiResponse
    {
        public IEnumerable<RoutingRule> Rules { get; init; } = [];
    }

    public sealed class GetResponseMappingsResponse : ApiResponse
    {
        public IEnumerable<ResponseMapping> Mappings { get; init; } = [];
    }

    public sealed class UploadFileResponse : ApiResponse
    {
        public Guid ReceiptId { get; init; }
    }

    public sealed class GetFileReceiptsResponse : ApiResponse
    {
        public IEnumerable<ReceiptSummary> Receipts { get; init; } = [];
    }

    public sealed class GetFileReceiptResponse : ApiResponse
    {
        public ReceiptDetail Receipt { get; init; } = null!;
    }

    public sealed class GetValidationErrorsResponse : ApiResponse
    {
        public ValidationResult ValidationErrors { get; init; } = null!;
    }

    public sealed class GetBatchResponse : ApiResponse
    {
        public BatchView BatchView { get; init; } = null!;
    }

    public sealed class GetPaymentGroupsResponse : ApiResponse
    {
        public IEnumerable<PaymentGroup> PaymentGroups { get; init; } = [];
    }

    public sealed class GetTransfersResponse : ApiResponse
    {
        public IEnumerable<TransferSummary> Transfers { get; init; } = [];
    }

    public sealed class GetTransferResponse : ApiResponse
    {
        public TransferDetail Transfer { get; init; } = null!;
    }

    public sealed class FinalizeBatchResponse : ApiResponse
    {
        public Guid ReportId { get; init; }
    }

    public sealed class GetReportsResponse : ApiResponse
    {
        public IEnumerable<ReportSummary> Reports { get; init; } = [];
    }

    public sealed class GetAuditResponse : ApiResponse
    {
        public IEnumerable<BatchAuditEntry> Audit { get; init; } = [];
    }
}
