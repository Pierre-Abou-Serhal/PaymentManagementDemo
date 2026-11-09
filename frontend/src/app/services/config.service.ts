import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface AppSettings {
    paymentApiUrl: string;
    healthUrl: string;
    operatorName: string;
}

@Injectable({ providedIn: 'root' })
export class ConfigService {
    private readonly http = inject(HttpClient);
    settings!: AppSettings;

    async loadConfig(): Promise<void> {
        this.settings = await firstValueFrom(this.http.get<AppSettings>('appsettings.json'));
    }
}
