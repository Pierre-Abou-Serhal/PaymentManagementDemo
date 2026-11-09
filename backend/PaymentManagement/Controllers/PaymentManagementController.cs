using Microsoft.AspNetCore.Mvc;
using PaymentManagement.Common;
using PaymentManagement.Filters;
using static PaymentManagement.Common.Request;
using static PaymentManagement.Common.Response;
using PaymentService = PaymentManagement.BLL.BLL;

namespace PaymentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[ServiceFilter(typeof(PaymentActionFilter), Order = -2100)]
public sealed class PaymentManagementController : ControllerBase
{
    private readonly PaymentService _bll;

    public PaymentManagementController(PaymentService bll)
    {
        _bll = bll;
    }

    [HttpPost("GetAllInstitutions")]
    public async Task<GetAllInstitutionsResponse> GetAllInstitutions(EmptyRequest request)
    {
        return new GetAllInstitutionsResponse
        {
            Institutions = await _bll.GetAllInstitutions()
        };
    }

    [HttpPost("GetIsoDefinitions")]
    public GetIsoDefinitionsResponse GetIsoDefinitions(EmptyRequest request)
    {
        return new GetIsoDefinitionsResponse
        {
            Definitions = _bll.GetIsoDefinitions()
        };
    }

    [HttpPost("GetRoutingRules")]
    public async Task<GetRoutingRulesResponse> GetRoutingRules(EmptyRequest request)
    {
        return new GetRoutingRulesResponse
        {
            Rules = await _bll.GetRoutingRules()
        };
    }

    [HttpPost("SaveRoutingRule")]
    public async Task<ApiResponse> SaveRoutingRule(SaveRoutingRuleRequest request)
    {
        await _bll.SaveRoutingRule(request.Currency, request.Threshold);
        return new ApiResponse();
    }

    [HttpPost("GetResponseMappings")]
    public async Task<GetResponseMappingsResponse> GetResponseMappings(InstitutionIdRequest request)
    {
        return new GetResponseMappingsResponse
        {
            Mappings = await _bll.GetResponseMappings(request.InstitutionId)
        };
    }

    [HttpPost("SaveResponseMapping")]
    public async Task<ApiResponse> SaveResponseMapping(SaveResponseMappingRequest request)
    {
        await _bll.SaveResponseMapping(request.InstitutionId, request.InputCode, request.OutputCode);
        return new ApiResponse();
    }

    [HttpPost("UploadFile")]
    [Consumes("multipart/form-data")]
    public async Task<UploadFileResponse> UploadFile([FromForm] InstitutionIdRequest request, IFormFile file, CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        return new UploadFileResponse
        {
            ReceiptId = await _bll.UploadFile(request.InstitutionId, file.FileName, source, cancellationToken)
        };
    }

    [HttpPost("GetFileReceipts")]
    public async Task<GetFileReceiptsResponse> GetFileReceipts(GetFileReceiptsRequest request)
    {
        return new GetFileReceiptsResponse
        {
            Receipts = await _bll.GetFileReceipts(request.InstitutionId, request.State, request.Skip, request.Take, request.Search)
        };
    }

    [HttpPost("GetFileReceipt")]
    public async Task<GetFileReceiptResponse> GetFileReceipt(ReceiptIdRequest request)
    {
        var receipt = await _bll.GetFileReceipt(request.ReceiptId!.Value)
            ?? throw new WorkflowException(404, "File receipt not found.");
        return new GetFileReceiptResponse { Receipt = receipt };
    }

    [HttpPost("GetValidationErrors")]
    public async Task<GetValidationErrorsResponse> GetValidationErrors(ReceiptIdRequest request)
    {
        var receipt = await _bll.GetFileReceipt(request.ReceiptId!.Value)
            ?? throw new WorkflowException(404, "File receipt not found.");
        return new GetValidationErrorsResponse
        {
            ValidationErrors = new ValidationResult(receipt.ErrorType, receipt.ValidationJson, receipt.DuplicateOf)
        };
    }

    [HttpPost("DownloadOriginal")]
    public async Task<IActionResult> DownloadOriginal(ReceiptIdRequest request)
    {
        var document = await _bll.GetOriginalFile(request.ReceiptId!.Value)
            ?? throw new WorkflowException(404, "Original file not found.");
        return File(document.Bytes, "application/xml", document.Filename);
    }

    [HttpPost("CorrectInstitution")]
    public async Task<ApiResponse> CorrectInstitution(CorrectInstitutionRequest request)
    {
        await _bll.CorrectInstitutionAssignment(request.ReceiptId!.Value,
            new InstitutionRequest(request.InstitutionId, request.ExpectedBatchVersion));
        return new ApiResponse();
    }

    [HttpPost("GetBatch")]
    public async Task<GetBatchResponse> GetBatch(BatchIdRequest request)
    {
        var batch = await _bll.GetBatch(request.BatchId!.Value)
            ?? throw new WorkflowException(404, "Batch not found.");
        return new GetBatchResponse { BatchView = batch };
    }

    [HttpPost("GetPaymentGroups")]
    public async Task<GetPaymentGroupsResponse> GetPaymentGroups(BatchIdRequest request)
    {
        return new GetPaymentGroupsResponse
        {
            PaymentGroups = await _bll.GetPaymentGroups(request.BatchId!.Value)
        };
    }

    [HttpPost("GetTransfers")]
    public async Task<GetTransfersResponse> GetTransfers(GetTransfersRequest request)
    {
        return new GetTransfersResponse
        {
            Transfers = await _bll.GetTransfers(request.BatchId!.Value, request.Category, request.Decision,
            request.GroupId, request.Skip, request.Take)
        };
    }

    [HttpPost("GetTransfer")]
    public async Task<GetTransferResponse> GetTransfer(TransferIdRequest request)
    {
        var transfer = await _bll.GetTransfer(request.TransferId!.Value)
            ?? throw new WorkflowException(404, "Transfer not found.");
        return new GetTransferResponse { Transfer = transfer };
    }

    [HttpPost("ProcessCategory")]
    public async Task<ApiResponse> ProcessCategory(ProcessCategoryRequest request)
    {
        await _bll.ProcessCategory(request.BatchId!.Value, request.Category);
        return new ApiResponse();
    }

    [HttpPost("SaveTransferDecisions")]
    public async Task<ApiResponse> SaveTransferDecisions(SaveTransferDecisionsRequest request)
    {
        await _bll.SaveTransferDecisions(request.BatchId!.Value, new DecisionRequest(request.Items, request.Decision, request.Reason));
        return new ApiResponse();
    }

    [HttpPost("FinalizeBatch")]
    public async Task<FinalizeBatchResponse> FinalizeBatch(FinalizeBatchRequest request)
    {
        return new FinalizeBatchResponse
        {
            ReportId = await _bll.FinalizeBatch(request.BatchId!.Value, new FinalizeRequest(request.OperationId, request.ExpectedBatchVersion))
        };
    }

    [HttpPost("ReopenBatch")]
    public async Task<ApiResponse> ReopenBatch(BatchIdRequest request)
    {
        await _bll.ReopenBatch(request.BatchId!.Value);
        return new ApiResponse();
    }

    [HttpPost("GetReports")]
    public async Task<GetReportsResponse> GetReports(BatchIdRequest request)
    {
        return new GetReportsResponse
        {
            Reports = await _bll.GetReports(request.BatchId!.Value)
        };
    }

    [HttpPost("GetAudit")]
    public async Task<GetAuditResponse> GetAudit(BatchIdRequest request)
    {
        return new GetAuditResponse
        {
            Audit = await _bll.GetAudit(request.BatchId!.Value)
        };
    }

    [HttpPost("DownloadReport")]
    public async Task<IActionResult> DownloadReport(ReportIdRequest request)
    {
        var document = await _bll.GetReportDocument(request.ReportId!.Value)
            ?? throw new WorkflowException(404, "Report not found.");
        return File(document.Bytes, document.MimeType, document.Filename);
    }

    [HttpPost("RetryPublication")]
    public async Task<ApiResponse> RetryPublication(ReportIdRequest request)
    {
        await _bll.RetryPublication(request.ReportId!.Value);
        return new ApiResponse();
    }
}
