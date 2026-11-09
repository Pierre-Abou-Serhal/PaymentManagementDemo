import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DrawerDirective } from '../../../layout/component/drawer.directive';
import { GroupHeaderComponent } from '../components/group-header.component';
import { IconComponent } from '../../../layout/component/app.icon';
import { StatusComponent } from '../../../layout/component/app.status';
import { errorMessage } from '../../../services/api.service';
import {
    BatchAuditEntry,
    BatchView,
    Institution,
    PaymentGroup,
    Receipt,
    Report,
    TransferDetail,
    TransferSummary
} from '../model/payment';
import { PaymentManagementService } from '../services/payment-management.service';

function categoryLabel(value: string): string {
    const labels: Record<string, string> = {
        LOCAL: 'Local',
        ACH: 'ACH',
        RTGS: 'RTGS',
        SWIFT: 'SWIFT',
        INVALID: 'Invalid'
    };
    return labels[value] ?? value;
}

function displayAmount(value: string | null): string {
    if (value === null) return '—';

    const [whole, fraction = ''] = value.split('.');
    const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return grouped + '.' + fraction.replace(/0+$/, '').padEnd(2, '0');
}

@Component({
    imports: [
        CommonModule,
        FormsModule,
        RouterLink,
        DrawerDirective,
        GroupHeaderComponent,
        IconComponent,
        StatusComponent
    ],
    templateUrl: './batch.component.html'
})
export class BatchComponent implements OnInit {
    private readonly payments = inject(PaymentManagementService);
    private readonly route = inject(ActivatedRoute);
    id = '';
    view = signal<BatchView | null>(null);
    receipt = signal<Receipt | null>(null);
    groups = signal<PaymentGroup[]>([]);
    transfers = signal<TransferSummary[]>([]);
    reports = signal<Report[]>([]);
    audit = signal<BatchAuditEntry[]>([]);
    institutions = signal<Institution[]>([]);
    selectedTransfer = signal<TransferDetail | null>(null);
    error = signal('');
    notice = signal('');
    busy = signal(false);
    loading = signal(true);
    tab = 'transfers';
    category = '';
    decision = '';
    groupId = '';
    skip = 0;
    reason = '';
    newInstitutionId = 0;
    selected = new Set<string>();
    Math = Math;
    categoryLabel = categoryLabel;
    actionLabel(value: string) {
        return value.replace(/([a-z])([A-Z])/g, '$1 $2');
    }
    formatAmount = displayAmount;
    ngOnInit() {
        this.id = this.route.snapshot.paramMap.get('id') || '';
        void this.refresh();
    }
    async refresh() {
        try {
            const view = await this.payments.getBatch(this.id);
            const [receipt, groups, reports, audit, institutions] = await Promise.all([
                this.payments.getFileReceipt(view.batch.receiptId),
                this.payments.getPaymentGroups(this.id),
                this.payments.getReports(this.id),
                this.payments.getAudit(this.id),
                this.payments.getAllInstitutions()
            ]);
            this.view.set(view);
            this.receipt.set(receipt);
            this.groups.set(groups);
            this.reports.set(reports);
            this.audit.set(audit);
            this.institutions.set(institutions);
            this.newInstitutionId =
                institutions.find((institution) => institution.code === view.batch.institution)?.id ?? 0;
            await this.loadTransfers();
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.loading.set(false);
        }
    }
    async loadTransfers() {
        try {
            this.transfers.set(
                await this.payments.getTransfers(this.id, {
                    category: this.category || undefined,
                    decision: this.decision || undefined,
                    groupId: this.groupId || undefined,
                    skip: this.skip,
                    take: 50
                })
            );
            this.selected.clear();
        } catch (e) {
            this.error.set(errorMessage(e));
        }
    }
    filterCategory(category: string) {
        this.category = this.category === category ? '' : category;
        this.skip = 0;
        this.tab = 'transfers';
        void this.loadTransfers();
    }
    processed(category: string) {
        return this.view()?.categories?.find((x) => x.category === category)?.processingState === 'Processed';
    }
    eligible(t: TransferSummary) {
        return t.category !== 'INVALID' && this.view()?.batch?.state === 'Editable' && this.processed(t.category);
    }
    eligibleTransfers() {
        return this.transfers().filter((t) => this.eligible(t));
    }
    allSelected() {
        const eligible = this.eligibleTransfers();
        return eligible.length > 0 && eligible.every((t) => this.selected.has(t.id));
    }
    toggleAll() {
        const all = this.allSelected();
        for (const t of this.eligibleTransfers()) all ? this.selected.delete(t.id) : this.selected.add(t.id);
    }
    toggle(id: string) {
        this.selected.has(id) ? this.selected.delete(id) : this.selected.add(id);
    }
    approvedCount() {
        const b = this.view()?.batch;
        return b ? b.transferCount - b.pendingCount - b.systemRejectedCount - b.operatorRejectedCount : 0;
    }
    completion() {
        const b = this.view()?.batch;
        return b?.transferCount ? Math.round(((b.transferCount - b.pendingCount) / b.transferCount) * 100) : 0;
    }
    showReports() {
        this.tab = 'reports';
        document.getElementById('batch-review')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    routeDescription(category: string) {
        return (
            {
                LOCAL: 'Own-bank transfers',
                ACH: 'Domestic · below threshold',
                RTGS: 'Domestic · at or above threshold',
                SWIFT: 'International transfers',
                INVALID: 'Automatically rejected by System'
            } as Record<string, string>
        )[category];
    }
    async action(work: () => Promise<unknown>, success: string) {
        if (this.busy()) return;
        this.busy.set(true);
        this.error.set('');
        this.notice.set('');
        try {
            await work();
            this.notice.set(success);
            await this.refresh();
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.busy.set(false);
        }
    }
    process(category: string) {
        void this.action(
            () => this.payments.processCategory(this.id, category),
            categoryLabel(category) + ' marked processed. You can now record outcomes.'
        );
    }
    decide(decision: string) {
        const items = this.transfers()
            .filter((t) => this.selected.has(t.id))
            .map((t) => ({ id: t.id, version: t.version }));
        void this.action(
            () => this.payments.saveTransferDecisions(this.id, items, decision, this.reason),
            'Transfer outcomes saved.'
        );
    }
    correctInstitution() {
        const batch = this.view()?.batch;
        if (!batch) return;

        void this.action(
            () => this.payments.correctInstitution(batch.receiptId, this.newInstitutionId, batch.version),
            'Institution assignment updated.'
        );
    }
    finalize() {
        const view = this.view();
        if (!view?.canFinalize) return;

        void this.action(
            () => this.payments.finalizeBatch(this.id, crypto.randomUUID(), view.batch.version),
            'Report generated. Download it from Reports.'
        );
    }
    reopen() {
        void this.action(
            () => this.payments.reopenBatch(this.id),
            'Batch reopened. Previous reports remain available.'
        );
    }
    retry(id: string) {
        void this.action(() => this.payments.retryPublication(id), 'Publication queued for retry.');
    }
    async detail(id: string) {
        try {
            this.selectedTransfer.set(await this.payments.getTransfer(id));
        } catch (e) {
            this.error.set(errorMessage(e));
        }
    }
    async downloadReport(id: string): Promise<void> {
        try {
            await this.payments.downloadReport(id);
        } catch (error) {
            this.error.set(errorMessage(error));
        }
    }
    @HostListener('document:keydown.escape') closeDetails() {
        this.selectedTransfer.set(null);
    }
}
