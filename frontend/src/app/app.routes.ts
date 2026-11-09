import { Routes } from '@angular/router';
import { QueuePage, UploadPage, ReceiptPage, BatchPage, SettingsPage } from './pages';

export const routes: Routes = [
  { path: '', component: QueuePage },
  { path: 'upload', component: UploadPage },
  { path: 'receipts/:id', component: ReceiptPage },
  { path: 'batches/:id', component: BatchPage },
  { path: 'settings', component: SettingsPage },
  { path: '**', redirectTo: '' },
];
