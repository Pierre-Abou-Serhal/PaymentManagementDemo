import { Routes } from '@angular/router';

export default [
    {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./payment-management/queue/queue.component').then((m) => m.QueueComponent)
    },
    {
        path: 'upload',
        loadComponent: () => import('./payment-management/upload/upload.component').then((m) => m.UploadComponent)
    },
    {
        path: 'receipts/:id',
        loadComponent: () => import('./payment-management/receipt/receipt.component').then((m) => m.ReceiptComponent)
    },
    {
        path: 'batches/:id',
        loadComponent: () => import('./payment-management/batch/batch.component').then((m) => m.BatchComponent)
    },
    {
        path: 'settings',
        loadComponent: () => import('./payment-management/settings/settings.component').then((m) => m.SettingsComponent)
    }
] satisfies Routes;
