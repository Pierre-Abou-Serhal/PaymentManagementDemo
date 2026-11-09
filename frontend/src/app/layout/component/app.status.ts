import { Component, Input } from '@angular/core';

@Component({
    selector: 'app-status',
    template: `<span class="status" [class]="'status ' + tone"><span class="status-dot"></span>{{ label }}</span>`
})
export class StatusComponent {
    @Input({ transform: (value: string | null) => value ?? '' }) value = '';
    get label() {
        return (
            (
                {
                    Editable: 'In review',
                    Finalized: 'Submitted',
                    ImportFailed: 'Import failed',
                    PublishedToOut: 'Published to OUT',
                    SupersededUnpublished: 'Superseded',
                    NotProcessed: 'Awaiting processing',
                    NotApplicable: 'No transfers',
                    NotRequired: 'Automatic',
                    Processed: 'Processed',
                    Publishing: 'Publishing',
                    Pending: 'Pending',
                    Approved: 'Approved',
                    Rejected: 'Rejected',
                    Imported: 'Imported',
                    Duplicate: 'Duplicate',
                    ArchivePending: 'Archive pending',
                    ErrorMovePending: 'Error move pending'
                } as Record<string, string>
            )[this.value] || this.value
        );
    }
    get tone() {
        return ['Finalized', 'Imported', 'Approved', 'PublishedToOut', 'Archived', 'Processed', 'Ready'].includes(
            this.value
        )
            ? 'positive'
            : ['ImportFailed', 'Rejected', 'Failed', 'Error'].includes(this.value)
              ? 'negative'
              : ['Pending', 'NotProcessed', 'Duplicate', 'Publishing', 'ArchivePending', 'ErrorMovePending'].includes(
                      this.value
                  )
                ? 'warning'
                : 'neutral';
    }
}
