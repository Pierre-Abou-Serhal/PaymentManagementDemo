import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SHOW_LOADING } from '../interceptors/loading.interceptor';
import { ApiRequest, BaseRequest } from '../models/Request';
import { ApiResponse } from '../models/Response';
import { ConfigService } from './config.service';

export class ApiError extends Error {
    constructor(readonly response: ApiResponse) {
        const { baseResp, details } = response;
        const messages = Array.isArray(details) ? details.map(detailMessage).filter(Boolean) : [];
        const description = [...baseResp.errors, ...messages].filter(Boolean).join(' ');
        const message = description || baseResp.returnDescription || 'The payment operation failed.';
        super(
            baseResp.statusCode >= 500 && baseResp.correlationId ? `${message} (${baseResp.correlationId})` : message
        );
    }
}

function detailMessage(detail: unknown): string {
    if (typeof detail === 'string') {
        return detail;
    }

    if (!detail || typeof detail !== 'object') {
        return '';
    }

    const record = detail as Record<string, unknown>;
    const message = record['error'] ?? record['message'] ?? record['Message'];
    return typeof message === 'string' ? message : '';
}

export function errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
        if (error.status === 0) {
            return 'Cannot reach the server. Check that the API is running and try again.';
        }

        return `The server could not complete the request (HTTP ${error.status}).`;
    }

    return error instanceof Error ? error.message : 'The payment operation could not be completed.';
}

@Injectable({ providedIn: 'root' })
export class ApiService {
    private readonly http = inject(HttpClient);
    private readonly config = inject(ConfigService);

    async postData<T extends ApiResponse>(action: string, fields: object = {}, showLoading = true): Promise<T> {
        const response = await firstValueFrom(
            this.http.post<T>(this.url(action), this.request(fields), {
                context: new HttpContext().set(SHOW_LOADING, showLoading)
            })
        );
        return this.check(response);
    }

    async postForm<T extends ApiResponse>(action: string, form: FormData): Promise<T> {
        const baseReq = this.baseRequest();
        form.set('baseReq.correlationId', baseReq.correlationId);
        form.set('baseReq.userData.userName', baseReq.userData.userName);
        return this.check(await firstValueFrom(this.http.post<T>(this.url(action), form)));
    }

    async postBlob(action: string, fields: object): Promise<void> {
        const response = await firstValueFrom(
            this.http.post(this.url(action), this.request(fields), {
                observe: 'response',
                responseType: 'blob'
            })
        );
        const file = response.body;
        const disposition = response.headers.get('Content-Disposition');
        if (!file) {
            throw new Error('The server returned an empty download.');
        }

        if (!disposition) {
            if (file.type.includes('json')) {
                this.check(JSON.parse(await file.text()) as ApiResponse);
            }
            throw new Error('The server returned no attachment.');
        }

        const url = URL.createObjectURL(file);
        const link = document.createElement('a');
        link.href = url;
        link.download = this.filename(disposition);
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    async getHealth<T>(): Promise<T> {
        return firstValueFrom(
            this.http.get<T>(this.config.settings.healthUrl, {
                context: new HttpContext().set(SHOW_LOADING, false)
            })
        );
    }

    private url(action: string): string {
        return `${this.config.settings.paymentApiUrl.replace(/\/$/, '')}/${action}`;
    }

    private request<T extends object>(fields: T): T & ApiRequest {
        return { ...fields, baseReq: this.baseRequest() };
    }

    private baseRequest(): BaseRequest {
        return {
            correlationId: crypto.randomUUID(),
            userData: { userName: this.config.settings.operatorName }
        };
    }

    private check<T extends ApiResponse>(response: T): T {
        if (!response?.baseResp) {
            throw new Error('The server returned an invalid payment response.');
        }
        if (response.baseResp.returnCode !== '0x0000') {
            throw new ApiError(response);
        }
        return response;
    }

    private filename(disposition: string): string {
        const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
        if (encoded) {
            return decodeURIComponent(encoded);
        }
        return disposition.match(/filename="?([^";]+)"?/i)?.[1] ?? 'payment-document';
    }
}
