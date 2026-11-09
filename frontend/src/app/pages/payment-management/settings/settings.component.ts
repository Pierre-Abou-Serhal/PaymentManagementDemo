import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../layout/component/app.icon';
import { StatusComponent } from '../../../layout/component/app.status';
import { errorMessage } from '../../../services/api.service';
import { Institution, IsoDefinition, ResponseMapping, RoutingRule } from '../model/payment';
import { PaymentManagementService } from '../services/payment-management.service';

@Component({
    imports: [CommonModule, FormsModule, IconComponent, StatusComponent],
    templateUrl: './settings.component.html'
})
export class SettingsComponent implements OnInit {
    private readonly payments = inject(PaymentManagementService);
    institutions = signal<Institution[]>([]);
    rules = signal<RoutingRule[]>([]);
    definitions = signal<IsoDefinition[]>([]);
    mappings = new Map<number, ResponseMapping[]>();
    error = signal('');
    notice = signal('');
    loading = signal(true);
    saving = signal('');
    selectedInput: Record<number, string> = {};
    selectedOutput: Record<number, string> = {};
    ngOnInit() {
        void this.load();
    }
    async load() {
        try {
            const [institutions, rules, definitions] = await Promise.all([
                this.payments.getAllInstitutions(),
                this.payments.getRoutingRules(),
                this.payments.getIsoDefinitions()
            ]);
            this.institutions.set(institutions);
            this.rules.set(rules);
            this.definitions.set(definitions);
            for (const i of institutions) {
                this.mappings.set(i.id, await this.payments.getResponseMappings(i.id));
                this.selectedInput[i.id] ||= 'pain.001.001.13';
                this.pickInput(i.id);
            }
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.loading.set(false);
        }
    }
    inputs() {
        return this.definitions().filter((x) => x.family === 'pain.001' && x.status === 'Verified');
    }
    choices(input: string) {
        return this.definitions().find((x) => x.code === input)?.counterparts || [];
    }
    pickInput(id: number) {
        this.selectedOutput[id] =
            this.mappings.get(id)?.find((x) => x.inputCode === this.selectedInput[id])?.outputCode ||
            this.choices(this.selectedInput[id])[0] ||
            '';
    }
    async setMapping(id: number) {
        if (this.saving()) return;
        this.saving.set('mapping' + id);
        this.error.set('');
        this.notice.set('');
        try {
            await this.payments.saveResponseMapping(id, this.selectedInput[id], this.selectedOutput[id]);
            await this.load();
            this.notice.set('Response mapping saved. New imports will use this selection.');
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.saving.set('');
        }
    }
    async setRule(currency: string, threshold: number | null) {
        if (this.saving() || threshold === null || threshold <= 0) return;
        this.saving.set(currency);
        this.error.set('');
        this.notice.set('');
        try {
            await this.payments.saveRoutingRule(currency, threshold);
            await this.load();
            this.notice.set(currency + ' threshold saved. Existing classifications are retained.');
        } catch (e) {
            this.error.set(errorMessage(e));
        } finally {
            this.saving.set('');
        }
    }
}
