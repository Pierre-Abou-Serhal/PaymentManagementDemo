import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DrawerDirective } from '../../../layout/component/drawer.directive';
import { GroupHeaderComponent } from '../components/group-header.component';
import { IconComponent } from '../../../layout/component/app.icon';
import { StatusComponent } from '../../../layout/component/app.status';
import { errorMessage } from '../../../services/api.service';
import { Health, Institution, ReceiptSummary } from '../model/payment';
import { PaymentManagementService } from '../services/payment-management.service';

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
    templateUrl: './queue.component.html'
})
export class QueueComponent implements OnInit, OnDestroy {
    private readonly payments = inject(PaymentManagementService);
    receipts = signal<ReceiptSummary[]>([]);
    institutions = signal<Institution[]>([]);
    health = signal<Health | null>(null);
    selectedReceipt = signal<ReceiptSummary | null>(null);
    error = signal('');
    loading = signal(true);
    updatedAt = signal<Date | null>(null);
    institution = '';
    state = '';
    search = '';
    skip = 0;
    pageSize = 25;
    Math = Math;
    private poll?: ReturnType<typeof setInterval>;
    private searchTimer?: ReturnType<typeof setTimeout>;
    private sequence = 0;
    ngOnInit() {
        void this.init();
        this.poll = setInterval(() => {
            if (!document.hidden) void this.load(false);
        }, 15000);
    }
    ngOnDestroy() {
        clearInterval(this.poll);
        clearTimeout(this.searchTimer);
        this.sequence++;
    }
    async init() {
        await Promise.all([this.checkHealth(), this.load(), this.loadInstitutions()]);
    }
    private async loadInstitutions() {
        try {
            this.institutions.set(await this.payments.getAllInstitutions());
        } catch (e) {
            this.error.set(errorMessage(e));
        }
    }
    async checkHealth() {
        try {
            this.health.set(await this.payments.getHealth());
        } catch (e) {
            this.health.set(
                e instanceof HttpErrorResponse && e.error?.database
                    ? e.error
                    : { database: 'unavailable', storage: 'unknown' }
            );
        }
    }
    healthy() {
        return this.health()?.database === 'available' && this.health()?.storage === 'available';
    }
    async load(showLoading = true) {
        const current = ++this.sequence;
        if (showLoading) this.loading.set(true);
        try {
            const rows = await this.payments.getFileReceipts(
                {
                    institutionId: this.institution ? Number(this.institution) : undefined,
                    state: this.state || undefined,
                    search: this.search || undefined,
                    skip: this.skip,
                    take: this.pageSize
                },
                showLoading
            );
            if (current !== this.sequence) return;
            this.receipts.set(rows);
            this.updatedAt.set(new Date());
            this.error.set('');
            const selected = this.selectedReceipt();
            if (selected) this.selectedReceipt.set(rows.find((r) => r.id === selected.id) || selected);
        } catch (e) {
            if (current === this.sequence) this.error.set(errorMessage(e));
        } finally {
            if (current === this.sequence) this.loading.set(false);
        }
    }
    filter(state = this.state) {
        this.state = state;
        this.skip = 0;
        this.selectedReceipt.set(null);
        void this.load();
    }
    searchChanged() {
        clearTimeout(this.searchTimer);
        this.searchTimer = setTimeout(() => this.filter(), 300);
    }
    page(direction: number) {
        this.skip = Math.max(0, this.skip + direction * this.pageSize);
        void this.load();
    }
    count(kind: string) {
        return this.receipts().filter((r) =>
            kind === 'review'
                ? r.batchState === 'Editable'
                : kind === 'submitted'
                  ? r.batchState === 'Finalized'
                  : ['ImportFailed', 'Duplicate'].includes(r.importState)
        ).length;
    }
    async downloadOriginal(id: string): Promise<void> {
        try {
            await this.payments.downloadOriginal(id);
        } catch (error) {
            this.error.set(errorMessage(error));
        }
    }
    @HostListener('document:keydown.escape') closeDetails() {
        this.selectedReceipt.set(null);
    }
}
