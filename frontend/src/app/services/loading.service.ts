import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
    private readonly pending = signal(0);
    readonly busy = computed(() => this.pending() > 0);

    start(): void {
        this.pending.update((count) => count + 1);
    }

    finish(): void {
        this.pending.update((count) => count - 1);
    }
}
