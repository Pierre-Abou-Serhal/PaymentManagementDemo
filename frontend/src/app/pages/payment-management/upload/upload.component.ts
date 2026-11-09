import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { IconComponent } from '../../../layout/component/app.icon';
import { errorMessage } from '../../../services/api.service';
import { Institution } from '../model/payment';
import { PaymentManagementService } from '../services/payment-management.service';

@Component({
    imports: [CommonModule, FormsModule, RouterLink, IconComponent],
    templateUrl: './upload.component.html'
})
export class UploadComponent implements OnInit {
    private readonly payments = inject(PaymentManagementService);
    private readonly router = inject(Router);
    institutions = signal<Institution[]>([]);
    error = signal('');
    busy = signal(false);
    dragging = signal(false);
    institutionId = 0;
    file: File | null = null;
    ngOnInit() {
        this.payments
            .getAllInstitutions()
            .then((institutions) => this.institutions.set(institutions))
            .catch((error: unknown) => this.error.set(errorMessage(error)));
    }
    choose(event: Event) {
        const input = event.target as HTMLInputElement;
        this.useFile(input.files?.[0] || null);
        input.value = '';
    }
    useFile(file: File | null) {
        this.error.set('');
        if (file && !file.name.toLowerCase().endsWith('.xml')) {
            this.error.set('Choose an XML initiation file (.xml).');
            return;
        }
        this.file = file;
    }
    drag(event: DragEvent) {
        event.preventDefault();
        if (!this.busy()) this.dragging.set(true);
    }
    drop(event: DragEvent) {
        event.preventDefault();
        this.dragging.set(false);
        if (this.busy()) return;
        const files = event.dataTransfer?.files;
        if (files && files.length > 1) {
            this.error.set('Upload one file at a time.');
            return;
        }
        this.useFile(files?.[0] || null);
    }
    async submit() {
        if (!this.file || !this.institutionId || this.busy()) return;
        this.busy.set(true);
        this.error.set('');
        try {
            const receiptId = await this.payments.uploadFile(this.institutionId, this.file);
            await this.router.navigate(['/receipts', receiptId]);
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.busy.set(false);
        }
    }
}
