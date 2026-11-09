import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GroupHeaderComponent } from '../components/group-header.component';
import { IconComponent } from '../../../layout/component/app.icon';
import { StatusComponent } from '../../../layout/component/app.status';
import { errorMessage } from '../../../services/api.service';
import { Receipt, ValidationIssue } from '../model/payment';
import { PaymentManagementService } from '../services/payment-management.service';

@Component({
    imports: [CommonModule, RouterLink, GroupHeaderComponent, IconComponent, StatusComponent],
    templateUrl: './receipt.component.html'
})
export class ReceiptComponent implements OnInit {
    private readonly payments = inject(PaymentManagementService);
    private readonly route = inject(ActivatedRoute);
    receipt = signal<Receipt | null>(null);
    error = signal('');
    busy = signal(false);
    loading = signal(true);
    ngOnInit() {
        void this.load();
    }
    async load() {
        try {
            this.receipt.set(await this.payments.getFileReceipt(this.route.snapshot.paramMap.get('id') ?? ''));
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.loading.set(false);
        }
    }
    validationErrors(): ValidationIssue[] {
        const validation = this.receipt()?.validationJson;
        if (!validation) return [];

        try {
            const issues = JSON.parse(validation) as {
                Message?: string;
                message?: string;
                Line?: number;
                line?: number;
                Position?: number;
                position?: number;
            }[];
            return issues.map((issue) => ({
                message: issue.Message ?? issue.message ?? '',
                line: issue.Line ?? issue.line,
                position: issue.Position ?? issue.position
            }));
        } catch {
            return [{ message: validation }];
        }
    }

    downloadOriginal(id: string): Promise<void> {
        return this.download(() => this.payments.downloadOriginal(id));
    }

    downloadReport(id: string): Promise<void> {
        return this.download(() => this.payments.downloadReport(id));
    }

    private async download(work: () => Promise<void>): Promise<void> {
        try {
            await work();
        } catch (error) {
            this.error.set(errorMessage(error));
        }
    }
    async retry(id: string) {
        if (this.busy()) return;
        this.busy.set(true);
        try {
            await this.payments.retryPublication(id);
            await this.load();
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.busy.set(false);
        }
    }
}
