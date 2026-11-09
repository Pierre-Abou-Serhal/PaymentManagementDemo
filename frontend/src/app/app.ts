import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IconComponent } from './ui';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, IconComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  router=inject(Router);
  get queueActive(){return this.router.url==='/'||this.router.url.startsWith('/batches/')||this.router.url.startsWith('/receipts/');}
}
