import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, Directive, ElementRef, HostListener, inject, Input, OnDestroy } from '@angular/core';

export interface GroupHeader {
  available: boolean;
  definition?: string;
  messageId?: string;
  createdAt?: string;
  numberOfTransactions?: string;
  controlSum?: string;
  initiatingParty?: string;
  forwardingAgent?: string;
  batchBooking?: string;
  grouping?: string;
  fields?: { path: string; value: string }[];
  xml?: string;
  notice?: string;
}

@Component({selector:'app-icon', template:`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="paths[name] || paths['file']"/></svg>`})
export class IconComponent {
  @Input() name = 'file';
  paths: Record<string,string> = {
    grid:'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
    inbox:'M4 4h16l2 11v5H2v-5L4 4z M2 15h6l2 3h4l2-3h6',
    upload:'M12 16V3 M7 8l5-5 5 5 M4 15v6h16v-6',
    file:'M14 2H5v20h14V7l-5-5z M14 2v5h5 M8 12h8 M8 16h6',
    settings:'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8 M9 3l-1 3-3 1-2 3 2 2-1 3 3 3 3-1 2 2 3-2 3 1 2-3-1-3 2-2-2-3-3-1-1-3H9z',
    arrow:'M4 12h16 M14 6l6 6-6 6',
    back:'M20 12H4 M10 6l-6 6 6 6',
    chevron:'M9 5l7 7-7 7',
    check:'M5 12l4 4L19 6',
    circleCheck:'M22 11v1a10 10 0 1 1-6-9 M8 11l4 4L22 5',
    clock:'M12 8v5l3 2 M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
    alert:'M12 3L1 21h22L12 3z M12 9v5 M12 18h.01',
    search:'M21 21l-5-5 M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
    refresh:'M20 8a9 9 0 1 0 1 7 M20 3v5h-5',
    download:'M12 3v12 M7 10l5 5 5-5 M4 17v4h16v-4',
    close:'M6 6l12 12 M18 6L6 18',
    layers:'M12 2L2 7l10 5 10-5-10-5z M2 12l10 5 10-5 M2 17l10 5 10-5',
    activity:'M2 12h5l3-9 4 18 3-9h5',
    globe:'M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0 M2 12h20 M12 2c6 6 6 14 0 20 M12 2c-6 6-6 14 0 20',
    bank:'M2 9l10-6 10 6H2z M4 21h16 M6 12v6 M12 12v6 M18 12v6',
    lock:'M6 10V7a6 6 0 0 1 12 0v3 M4 10h16v12H4z M12 15v3',
    code:'M8 6l-6 6 6 6 M16 6l6 6-6 6 M14 3l-4 18',
    user:'M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0 M4 22v-3a8 8 0 0 1 16 0v3',
  };
}

@Component({selector:'app-status', template:`<span class="status" [class]="'status '+tone"><span class="status-dot"></span>{{label}}</span>`})
export class StatusComponent {
  @Input() value = '';
  get label() { return ({Editable:'In review',Finalized:'Submitted',ImportFailed:'Import failed',PublishedToOut:'Published to OUT',SupersededUnpublished:'Superseded',NotProcessed:'Awaiting processing',NotApplicable:'No transfers',NotRequired:'Automatic',Processed:'Processed',Publishing:'Publishing',Pending:'Pending',Approved:'Approved',Rejected:'Rejected',Imported:'Imported',Duplicate:'Duplicate',ArchivePending:'Archive pending',ErrorMovePending:'Error move pending'} as Record<string,string>)[this.value] || this.value; }
  get tone() { return ['Finalized','Imported','Approved','PublishedToOut','Archived','Processed','Ready'].includes(this.value) ? 'positive' : ['ImportFailed','Rejected','Failed','Error'].includes(this.value) ? 'negative' : ['Pending','NotProcessed','Duplicate','Publishing','ArchivePending','ErrorMovePending'].includes(this.value) ? 'warning' : 'neutral'; }
}

@Component({selector:'app-group-header', imports:[CommonModule,IconComponent], template:`
  <section class="group-header-block">
    <div class="section-head"><div class="section-title"><span class="section-icon"><app-icon name="file"/></span><div><h2>File details</h2><p>Original ISO 20022 group header</p></div></div><span class="code-tag">GrpHdr</span></div>
    @if(header?.available){
      <dl class="header-fields" [class.compact]="compact">
        <div><dt>Message ID</dt><dd class="mono">{{header?.messageId || 'Not supplied'}}</dd></div>
        <div><dt>Initiating party</dt><dd>{{header?.initiatingParty || 'Not supplied'}}</dd></div>
        <div><dt>Created in source file</dt><dd class="mono">{{header?.createdAt || 'Not supplied'}}</dd></div>
        <div><dt>Declared transactions</dt><dd>{{header?.numberOfTransactions ?? 'Not supplied'}}</dd></div>
        <div><dt>Control sum</dt><dd class="mono">{{header?.controlSum ?? 'Not supplied'}}</dd></div>
        <div><dt>Message definition</dt><dd class="mono">{{header?.definition || 'Unknown'}}</dd></div>
        @if(header?.forwardingAgent){<div><dt>Forwarding agent</dt><dd>{{header?.forwardingAgent}}</dd></div>}
        @if(header?.batchBooking){<div><dt>Batch booking</dt><dd>{{header?.batchBooking}}</dd></div>}
        @if(header?.grouping){<div><dt>Grouping</dt><dd>{{header?.grouping}}</dd></div>}
      </dl>
      <details class="disclosure"><summary>All group header fields <span>{{header?.fields?.length}} values</span></summary><div class="header-values">@for(field of header?.fields;track $index){<div><code>{{field.path}}</code><span>{{field.value || '(empty)'}}</span></div>}</div></details>
      <details class="disclosure"><summary>Original GrpHdr XML <app-icon name="code"/></summary><pre>{{header?.xml}}</pre></details>
    }@else{<p class="empty-note">{{header?.notice || 'Group header information is unavailable.'}}</p>}
  </section>
`})
export class GroupHeaderComponent { @Input() header: GroupHeader | null = null; @Input() compact = false; }

@Directive({selector:'[appDrawer]'})
export class DrawerDirective implements AfterViewInit, OnDestroy {
  private element:ElementRef<HTMLElement>=inject(ElementRef);
  private previous:HTMLElement|null=null;
  private overflow='';
  private controls(){return Array.from(this.element.nativeElement.querySelectorAll<HTMLElement>('button:not(:disabled),a[href],input:not(:disabled),select:not(:disabled),summary,[tabindex="0"]')).filter(e=>e.getClientRects().length>0);}
  ngAfterViewInit(){this.previous=document.activeElement as HTMLElement;this.overflow=document.body.style.overflow;document.body.style.overflow='hidden';this.controls()[0]?.focus();}
  ngOnDestroy(){document.body.style.overflow=this.overflow;if(this.previous?.isConnected)this.previous.focus();}
  @HostListener('keydown',['$event']) trapFocus(event:KeyboardEvent){
    if(event.key!=='Tab')return;
    const controls=this.controls(),first=controls[0],last=controls[controls.length-1];
    if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus();}
    else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus();}
  }
}
