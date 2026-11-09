import { Injectable, inject } from '@angular/core';
import { ApiResponse } from '../../../models/Response';
import { ApiService } from '../../../services/api.service';
import {
    BatchAuditEntry,
    BatchView,
    Health,
    Institution,
    IsoDefinition,
    PaymentGroup,
    Receipt,
    ReceiptFilters,
    ReceiptSummary,
    Report,
    ResponseMapping,
    RoutingRule,
    TransferDecisionItem,
    TransferDetail,
    TransferFilters,
    TransferSummary
} from '../model/payment';

@Injectable({ providedIn: 'root' })
export class PaymentManagementService {
    private readonly api = inject(ApiService);

    getHealth(): Promise<Health> {
        return this.api.getHealth<Health>();
    }

    async getAllInstitutions(): Promise<Institution[]> {
        const response = await this.api.postData<ApiResponse & { institutions: Institution[] }>('GetAllInstitutions');
        return response.institutions;
    }

    async getFileReceipts(filters: ReceiptFilters, showLoading: boolean): Promise<ReceiptSummary[]> {
        const response = await this.api.postData<ApiResponse & { receipts: ReceiptSummary[] }>(
            'GetFileReceipts',
            filters,
            showLoading
        );
        return response.receipts;
    }

    async getFileReceipt(receiptId: string): Promise<Receipt> {
        const response = await this.api.postData<ApiResponse & { receipt: Receipt }>('GetFileReceipt', { receiptId });
        return response.receipt;
    }

    async uploadFile(institutionId: number, file: File): Promise<string> {
        const form = new FormData();
        form.set('institutionId', String(institutionId));
        form.set('file', file);
        const response = await this.api.postForm<ApiResponse & { receiptId: string }>('UploadFile', form);
        return response.receiptId;
    }

    downloadOriginal(receiptId: string): Promise<void> {
        return this.api.postBlob('DownloadOriginal', { receiptId });
    }

    downloadReport(reportId: string): Promise<void> {
        return this.api.postBlob('DownloadReport', { reportId });
    }

    async getBatch(batchId: string): Promise<BatchView> {
        const response = await this.api.postData<ApiResponse & { batchView: BatchView }>('GetBatch', { batchId });
        return response.batchView;
    }

    async getPaymentGroups(batchId: string): Promise<PaymentGroup[]> {
        const response = await this.api.postData<ApiResponse & { paymentGroups: PaymentGroup[] }>('GetPaymentGroups', {
            batchId
        });
        return response.paymentGroups;
    }

    async getTransfers(batchId: string, filters: TransferFilters): Promise<TransferSummary[]> {
        const response = await this.api.postData<ApiResponse & { transfers: TransferSummary[] }>('GetTransfers', {
            batchId,
            ...filters
        });
        return response.transfers;
    }

    async getTransfer(transferId: string): Promise<TransferDetail> {
        const response = await this.api.postData<ApiResponse & { transfer: TransferDetail }>('GetTransfer', {
            transferId
        });
        return response.transfer;
    }

    async getReports(batchId: string): Promise<Report[]> {
        const response = await this.api.postData<ApiResponse & { reports: Report[] }>('GetReports', { batchId });
        return response.reports;
    }

    async getAudit(batchId: string): Promise<BatchAuditEntry[]> {
        const response = await this.api.postData<ApiResponse & { audit: BatchAuditEntry[] }>('GetAudit', { batchId });
        return response.audit;
    }

    processCategory(batchId: string, category: string): Promise<ApiResponse> {
        return this.api.postData('ProcessCategory', { batchId, category });
    }

    saveTransferDecisions(
        batchId: string,
        items: TransferDecisionItem[],
        decision: string,
        reason: string
    ): Promise<ApiResponse> {
        return this.api.postData('SaveTransferDecisions', { batchId, items, decision, reason });
    }

    correctInstitution(receiptId: string, institutionId: number, expectedBatchVersion: string): Promise<ApiResponse> {
        return this.api.postData('CorrectInstitution', { receiptId, institutionId, expectedBatchVersion });
    }

    finalizeBatch(batchId: string, operationId: string, expectedBatchVersion: string): Promise<ApiResponse> {
        return this.api.postData('FinalizeBatch', { batchId, operationId, expectedBatchVersion });
    }

    reopenBatch(batchId: string): Promise<ApiResponse> {
        return this.api.postData('ReopenBatch', { batchId });
    }

    retryPublication(reportId: string): Promise<ApiResponse> {
        return this.api.postData('RetryPublication', { reportId });
    }

    async getRoutingRules(): Promise<RoutingRule[]> {
        type RulePayload = Omit<RoutingRule, 'threshold'> & { threshold: string };
        const response = await this.api.postData<ApiResponse & { rules: RulePayload[] }>('GetRoutingRules');
        return response.rules.map((rule) => ({ ...rule, threshold: Number(rule.threshold) }));
    }

    async getIsoDefinitions(): Promise<IsoDefinition[]> {
        const response = await this.api.postData<ApiResponse & { definitions: IsoDefinition[] }>('GetIsoDefinitions');
        return response.definitions;
    }

    async getResponseMappings(institutionId: number): Promise<ResponseMapping[]> {
        const response = await this.api.postData<ApiResponse & { mappings: ResponseMapping[] }>('GetResponseMappings', {
            institutionId
        });
        return response.mappings;
    }

    saveResponseMapping(institutionId: number, inputCode: string, outputCode: string): Promise<ApiResponse> {
        return this.api.postData('SaveResponseMapping', { institutionId, inputCode, outputCode });
    }

    saveRoutingRule(currency: string, threshold: number): Promise<ApiResponse> {
        return this.api.postData('SaveRoutingRule', { currency, threshold });
    }
}
