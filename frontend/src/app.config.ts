import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
    ApplicationConfig,
    ErrorHandler,
    inject,
    provideAppInitializer,
    provideBrowserGlobalErrorListeners
} from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { appRoutes } from './app.routes';
import { GlobalErrorHandler } from './app/global/global-error-handler';
import { loadingInterceptor } from './app/interceptors/loading.interceptor';
import { ConfigService } from './app/services/config.service';

export const appConfig: ApplicationConfig = {
    providers: [
        provideBrowserGlobalErrorListeners(),
        { provide: ErrorHandler, useExisting: GlobalErrorHandler },
        provideHttpClient(withInterceptors([loadingInterceptor])),
        provideAppInitializer(() => inject(ConfigService).loadConfig()),
        provideRouter(appRoutes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' }))
    ]
};
