import { ErrorHandler, Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class GlobalErrorHandler implements ErrorHandler {
    readonly message = signal('');

    handleError(error: unknown): void {
        console.error(error);
        this.message.set('An unexpected error occurred. Reload the workspace and try again.');
    }
}
