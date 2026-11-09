import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';

export const SHOW_LOADING = new HttpContextToken(() => true);

export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
    if (!request.context.get(SHOW_LOADING)) {
        return next(request);
    }

    const loading = inject(LoadingService);
    loading.start();
    return next(request).pipe(finalize(() => loading.finish()));
};
