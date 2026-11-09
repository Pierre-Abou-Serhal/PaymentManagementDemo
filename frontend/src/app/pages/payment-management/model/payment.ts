export interface GroupHeader {
    available: boolean;
    definition?: string;
    messageId?: string;
    createdAt?: string;
    numberOfTransactions?: string;
    controlSum?: string;
    initiatingParty?: string;
    forwardingAgent?: string;
    batchBooking?: string;
    grouping?: string;
    fields?: { path: string; value: string }[];
    xml?: string;
    notice?: string;
}

export interface Institution {
    id: number;
    code: string;
    displayName: string;
    isActive: boolean;
}

export interface Health {
    database: string;
    storage: string;
    missingFolders?: string[];
    operatorIdentity?: string;
}

export interface ReceiptSummary {
    id: string;
    originalFilename: string;
    intakeInstitution: string;
    effectiveInstitution: string;
    channel: string;
    receivedUtc: string;
    importState: string;
    locationState: string;
    detectedDefinition: string | null;
    batchId: string | null;
    batchState: string | null;
    transferCount: number;
    pendingCount: number;
    groupHeader: GroupHeader;
}

export interface Receipt extends Omit<ReceiptSummary, 'batchState' | 'transferCount' | 'pendingCount'> {
    byteLength: number;
    checksum: string;
    originalPath: string;
    currentPath: string | null;
    errorType: string | null;
    validationJson: string | null;
    duplicateOf: string | null;
    technicalDocumentId: string | null;
    technicalPublicationState: string | null;
}

export interface ValidationIssue {
    message: string;
    line?: number;
    position?: number;
}

export interface BatchView {
    batch: {
        receiptId: string;
        state: string;
        institution: string;
        inputCode: string;
        outputCode: string | null;
        mappingRevision: number | null;
        assignmentLockedUtc: string | null;
        currentRevision: number;
        version: string;
        transferCount: number;
        pendingCount: number;
        systemRejectedCount: number;
        operatorRejectedCount: number;
    };
    categories: {
        category: string;
        transferCount: number;
        processingState: string;
        processedActor: string | null;
        processedUtc: string | null;
    }[];
    currencyTotals: { currency: string | null; amount: string }[];
    blockers: string[];
    canFinalize: boolean;
}

export interface PaymentGroup {
    id: string;
    sourceOrdinal: number;
    paymentInfoId: string | null;
}

export interface TransferSummary {
    id: string;
    version: string;
    groupOrdinal: number;
    sourceOrdinal: number;
    paymentInfoId: string | null;
    endToEndId: string | null;
    instructionId: string | null;
    creditorName: string | null;
    creditorAccount: string | null;
    creditorAccountKind: string | null;
    debtorAccount: string | null;
    amount: string | null;
    amountBasis: string | null;
    transferCurrency: string | null;
    currency: string | null;
    category: string;
    decision: string;
    decisionReason: string | null;
}

export interface TransferDetail {
    id: string;
    groupOrdinal: number;
    sourceOrdinal: number;
    paymentInfoId: string | null;
    endToEndId: string | null;
    instructionId: string | null;
    debtorName: string | null;
    debtorAccount: string | null;
    creditorName: string | null;
    creditorAccount: string | null;
    creditorAccountKind: string | null;
    instructedAmount: string | null;
    instructedCurrency: string | null;
    equivalentAmount: string | null;
    equivalentCurrency: string | null;
    transferCurrency: string | null;
    accountCurrency: string | null;
    routingAmount: string | null;
    routingExplanation: string | null;
    ruleRevision: number | null;
    category: string;
    decision: string;
    decisionSource: string;
    decisionReason: string | null;
    remittances: { kind: string; value: string; xml: string }[];
    sourceXml: string;
    history: AuditEntry[];
}

export interface Report {
    id: string;
    revision: number;
    isoDefinition: string;
    messageId: string;
    recipient: string;
    publicationState: string;
    generatedUtc: string;
    publishedUtc: string | null;
    lastError: string | null;
}

export interface AuditEntry {
    occurredUtc: string;
    actor: string;
    action: string;
    detailsJson: string | null;
}

export interface BatchAuditEntry extends AuditEntry {
    id: number;
}

export interface RoutingRule {
    currency: string;
    threshold: number | null;
    revision: number;
}

export interface ResponseMapping {
    inputCode: string;
    outputCode: string;
    revision: number;
}

export interface IsoDefinition {
    code: string;
    family: string;
    status: string;
    sha256: string;
    counterparts?: string[];
}

export interface ReceiptFilters {
    institutionId?: number;
    state?: string;
    search?: string;
    skip: number;
    take: number;
}

export interface TransferFilters {
    category?: string;
    decision?: string;
    groupId?: string;
    skip: number;
    take: number;
}

export interface TransferDecisionItem {
    id: string;
    version: string;
}
