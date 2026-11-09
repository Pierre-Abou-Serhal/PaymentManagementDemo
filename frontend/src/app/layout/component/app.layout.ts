import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { GlobalErrorHandler } from '../../global/global-error-handler';
import { LoadingService } from '../../services/loading.service';
import { IconComponent } from './app.icon';

@Component({
    imports: [RouterOutlet, RouterLink, RouterLinkActive, IconComponent],
    selector: 'app-layout',
    templateUrl: './app.layout.html'
})
export class AppLayout {
    private readonly router = inject(Router);
    protected readonly loading = inject(LoadingService);
    protected readonly errors = inject(GlobalErrorHandler);

    get queueActive(): boolean {
        return (
            this.router.url === '/' ||
            this.router.url.startsWith('/batches/') ||
            this.router.url.startsWith('/receipts/')
        );
    }
}
