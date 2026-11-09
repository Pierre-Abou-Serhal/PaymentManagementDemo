import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DrawerDirective, GroupHeaderComponent, IconComponent, StatusComponent } from './ui';

type Row = any;
const ui = [CommonModule, FormsModule, RouterLink, IconComponent, StatusComponent, GroupHeaderComponent];
function err(e: unknown): string {
  if (e instanceof HttpErrorResponse) {
    if (e.status === 0) return 'Cannot reach the server. Check that the API is running and try again.';
    const message = e.error?.error ?? e.error?.title ?? e.message;
    const details = Array.isArray(e.error?.details) ? e.error.details.map((d: Row) => typeof d === 'string' ? d : d.error || d.message || d.Message).filter(Boolean).join(' ') : '';
    return [message, details].filter(Boolean).join(' ');
  }
  return e instanceof Error ? e.message : String(e);
}
function qs(values: Record<string, unknown>): string {
  const p = new URLSearchParams();
  for (const [k,v] of Object.entries(values)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v));
  return p.toString();
}
export function categoryLabel(value: string) { return ({LOCAL:'Local', ACH:'ACH', RTGS:'RTGS', SWIFT:'SWIFT', INVALID:'Invalid'} as Record<string,string>)[value] || value; }
function displayAmount(value:unknown){if(value===null||value===undefined)return '—';const [whole,fraction='']=String(value).split('.');return whole.replace(/\B(?=(\d{3})+(?!\d))/g,',')+'.'+fraction.replace(/0+$/,'').padEnd(2,'0');}

@Component({imports:[...ui,DrawerDirective], templateUrl:'./queue-page.html'})
export class QueuePage implements OnInit, OnDestroy {
  private http=inject(HttpClient);
  receipts=signal<Row[]>([]); institutions=signal<Row[]>([]); health=signal<Row|null>(null); selectedReceipt=signal<Row|null>(null);
  error=signal(''); loading=signal(true); updatedAt=signal<Date|null>(null);
  institution=''; state=''; search=''; skip=0; pageSize=25; Math=Math;
  private poll?: ReturnType<typeof setInterval>; private searchTimer?: ReturnType<typeof setTimeout>; private sequence=0;
  ngOnInit(){ void this.init(); this.poll=setInterval(()=>{if(!document.hidden) void this.load(false);},15000); }
  ngOnDestroy(){clearInterval(this.poll);clearTimeout(this.searchTimer);this.sequence++;}
  async init(){
    await Promise.all([this.checkHealth(),this.load(),this.loadInstitutions()]);
  }
  private async loadInstitutions(){try{this.institutions.set(await firstValueFrom(this.http.get<Row[]>('/api/institutions')));}catch(e){this.error.set(err(e));}}
  async checkHealth(){try{this.health.set(await firstValueFrom(this.http.get<Row>('/health')));}catch(e){this.health.set(e instanceof HttpErrorResponse&&e.error?.database ? e.error : {database:'unavailable',storage:'unknown'});}}
  healthy(){return this.health()?.database==='available'&&this.health()?.storage==='available';}
  async load(showLoading=true){
    const current=++this.sequence;
    if(showLoading)this.loading.set(true);
    try{
      const rows=await firstValueFrom(this.http.get<Row[]>('/api/file-receipts?'+qs({institutionId:this.institution,state:this.state,search:this.search,skip:this.skip,take:this.pageSize})));
      if(current!==this.sequence)return;
      this.receipts.set(rows);this.updatedAt.set(new Date());this.error.set('');
      const selected=this.selectedReceipt();if(selected)this.selectedReceipt.set(rows.find(r=>r.id===selected.id)||selected);
    }catch(e){if(current===this.sequence)this.error.set(err(e));}finally{if(current===this.sequence)this.loading.set(false);}
  }
  filter(state=this.state){this.state=state;this.skip=0;this.selectedReceipt.set(null);void this.load();}
  searchChanged(){clearTimeout(this.searchTimer);this.searchTimer=setTimeout(()=>this.filter(),300);}
  page(direction:number){this.skip=Math.max(0,this.skip+direction*this.pageSize);void this.load();}
  count(kind:string){return this.receipts().filter(r=>kind==='review'?r.batchState==='Editable':kind==='submitted'?r.batchState==='Finalized':['ImportFailed','Duplicate'].includes(r.importState)).length;}
  @HostListener('document:keydown.escape') closeDetails(){this.selectedReceipt.set(null);}
}

@Component({imports:[CommonModule,FormsModule,RouterLink,IconComponent],templateUrl:'./upload-page.html'})
export class UploadPage implements OnInit {
  private http=inject(HttpClient);private router=inject(Router);
  institutions=signal<Row[]>([]);error=signal('');busy=signal(false);dragging=signal(false);institutionId=0;file:File|null=null;
  ngOnInit(){firstValueFrom(this.http.get<Row[]>('/api/institutions')).then(x=>this.institutions.set(x)).catch(e=>this.error.set(err(e)));}
  choose(event:Event){const input=event.target as HTMLInputElement;this.useFile(input.files?.[0]||null);input.value='';}
  useFile(file:File|null){this.error.set('');if(file&&!file.name.toLowerCase().endsWith('.xml')){this.error.set('Choose an XML initiation file (.xml).');return;}this.file=file;}
  drag(event:DragEvent){event.preventDefault();if(!this.busy())this.dragging.set(true);}
  drop(event:DragEvent){event.preventDefault();this.dragging.set(false);if(this.busy())return;const files=event.dataTransfer?.files;if(files&&files.length>1){this.error.set('Upload one file at a time.');return;}this.useFile(files?.[0]||null);}
  async submit(){if(!this.file||!this.institutionId||this.busy())return;this.busy.set(true);this.error.set('');const data=new FormData();data.append('institutionId',String(this.institutionId));data.append('file',this.file);try{const r=await firstValueFrom(this.http.post<Row>('/api/file-receipts/upload',data));await this.router.navigate(['/receipts',r.receiptId]);}catch(e){this.error.set(err(e));}finally{this.busy.set(false);}}
}

@Component({imports:ui,templateUrl:'./receipt-page.html'})
export class ReceiptPage implements OnInit {
  private http=inject(HttpClient);private route=inject(ActivatedRoute);
  receipt=signal<Row|null>(null);error=signal('');busy=signal(false);loading=signal(true);
  ngOnInit(){void this.load();}
  async load(){try{this.receipt.set(await firstValueFrom(this.http.get<Row>('/api/file-receipts/'+this.route.snapshot.paramMap.get('id'))));}catch(e){this.error.set(err(e));}finally{this.loading.set(false);}}
  validationErrors(){try{return JSON.parse(this.receipt()?.validationJson||'[]');}catch{return [{Message:this.receipt()?.validationJson}];}}
  async retry(id:string){if(this.busy())return;this.busy.set(true);try{await firstValueFrom(this.http.post('/api/reports/'+id+'/retry-publication',{}));await this.load();}catch(e){this.error.set(err(e));}finally{this.busy.set(false);}}
}

@Component({imports:[...ui,DrawerDirective],templateUrl:'./batch-page.html'})
export class BatchPage implements OnInit {
  private http=inject(HttpClient);private route=inject(ActivatedRoute);
  id='';view=signal<Row|null>(null);receipt=signal<Row|null>(null);groups=signal<Row[]>([]);transfers=signal<Row[]>([]);reports=signal<Row[]>([]);audit=signal<Row[]>([]);institutions=signal<Row[]>([]);
  selectedTransfer=signal<Row|null>(null);error=signal('');notice=signal('');busy=signal(false);loading=signal(true);tab='transfers';
  category='';decision='';groupId='';skip=0;reason='';newInstitutionId=0;selected=new Set<string>();Math=Math;categoryLabel=categoryLabel;
  actionLabel(value:string){return value.replace(/([a-z])([A-Z])/g,'$1 $2');} formatAmount=displayAmount;
  ngOnInit(){this.id=this.route.snapshot.paramMap.get('id')||'';void this.refresh();}
  async refresh(){
    try{
      const v=await firstValueFrom(this.http.get<Row>('/api/batches/'+this.id));
      const [receipt,groups,reports,audit,institutions]=await Promise.all([
        firstValueFrom(this.http.get<Row>('/api/file-receipts/'+v.batch.receiptId)),firstValueFrom(this.http.get<Row[]>('/api/batches/'+this.id+'/payment-groups')),
        firstValueFrom(this.http.get<Row[]>('/api/batches/'+this.id+'/reports')),firstValueFrom(this.http.get<Row[]>('/api/batches/'+this.id+'/audit')),firstValueFrom(this.http.get<Row[]>('/api/institutions'))
      ]);
      this.view.set(v);this.receipt.set(receipt);this.groups.set(groups);this.reports.set(reports);this.audit.set(audit);this.institutions.set(institutions);
      this.newInstitutionId=institutions.find(i=>i.code===v.batch.institution)?.id||0;await this.loadTransfers();
    }catch(e){this.error.set(err(e));}finally{this.loading.set(false);}
  }
  async loadTransfers(){try{this.transfers.set(await firstValueFrom(this.http.get<Row[]>('/api/batches/'+this.id+'/transfers?'+qs({category:this.category,decision:this.decision,groupId:this.groupId,skip:this.skip,take:50}))));this.selected.clear();}catch(e){this.error.set(err(e));}}
  filterCategory(category:string){this.category=this.category===category?'':category;this.skip=0;this.tab='transfers';void this.loadTransfers();}
  processed(category:string){return this.view()?.categories?.find((x:Row)=>x.category===category)?.processingState==='Processed';}
  eligible(t:Row){return t.category!=='INVALID'&&this.view()?.batch?.state==='Editable'&&this.processed(t.category);}
  eligibleTransfers(){return this.transfers().filter(t=>this.eligible(t));}
  allSelected(){const eligible=this.eligibleTransfers();return eligible.length>0&&eligible.every(t=>this.selected.has(t.id));}
  toggleAll(){const all=this.allSelected();for(const t of this.eligibleTransfers())all?this.selected.delete(t.id):this.selected.add(t.id);}
  toggle(id:string){this.selected.has(id)?this.selected.delete(id):this.selected.add(id);}
  approvedCount(){const b=this.view()?.batch;return b?b.transferCount-b.pendingCount-b.systemRejectedCount-b.operatorRejectedCount:0;}
  completion(){const b=this.view()?.batch;return b?.transferCount?Math.round((b.transferCount-b.pendingCount)/b.transferCount*100):0;}
  showReports(){this.tab='reports';document.getElementById('batch-review')?.scrollIntoView({behavior:'smooth',block:'start'});}
  routeDescription(category:string){return ({LOCAL:'Own-bank transfers',ACH:'Domestic · below threshold',RTGS:'Domestic · at or above threshold',SWIFT:'International transfers',INVALID:'Automatically rejected by System'} as Record<string,string>)[category];}
  async action(work:()=>Promise<unknown>,success:string){if(this.busy())return;this.busy.set(true);this.error.set('');this.notice.set('');try{await work();this.notice.set(success);await this.refresh();}catch(e){this.error.set(err(e));}finally{this.busy.set(false);}}
  process(category:string){void this.action(()=>firstValueFrom(this.http.post('/api/batches/'+this.id+'/categories/'+category+'/process',{})),categoryLabel(category)+' marked processed. You can now record outcomes.');}
  decide(decision:string){const items=this.transfers().filter(t=>this.selected.has(t.id)).map(t=>({id:t.id,version:t.version}));void this.action(()=>firstValueFrom(this.http.post('/api/batches/'+this.id+'/transfer-decisions',{items,decision,reason:this.reason})),'Transfer outcomes saved.');}
  correctInstitution(){void this.action(()=>firstValueFrom(this.http.post('/api/file-receipts/'+this.view()?.batch?.receiptId+'/institution',{institutionId:this.newInstitutionId,expectedBatchVersion:this.view()?.batch?.version})),'Institution assignment updated.');}
  finalize(){void this.action(()=>firstValueFrom(this.http.post('/api/batches/'+this.id+'/finalize',{operationId:crypto.randomUUID(),expectedBatchVersion:this.view()?.batch?.version})),'Report generated. Download it from Reports.');}
  reopen(){void this.action(()=>firstValueFrom(this.http.post('/api/batches/'+this.id+'/reopen',{})),'Batch reopened. Previous reports remain available.');}
  retry(id:string){void this.action(()=>firstValueFrom(this.http.post('/api/reports/'+id+'/retry-publication',{})),'Publication queued for retry.');}
  async detail(id:string){try{this.selectedTransfer.set(await firstValueFrom(this.http.get<Row>('/api/transfers/'+id)));}catch(e){this.error.set(err(e));}}
  @HostListener('document:keydown.escape') closeDetails(){this.selectedTransfer.set(null);}
}

@Component({imports:[CommonModule,FormsModule,IconComponent,StatusComponent],templateUrl:'./settings-page.html'})
export class SettingsPage implements OnInit {
  private http=inject(HttpClient);
  institutions=signal<Row[]>([]);rules=signal<Row[]>([]);definitions=signal<Row[]>([]);mappings=new Map<number,Row[]>();error=signal('');notice=signal('');loading=signal(true);saving=signal('');
  selectedInput:Record<number,string>={};selectedOutput:Record<number,string>={};
  ngOnInit(){void this.load();}
  async load(){try{const [institutions,rules,definitions]=await Promise.all([firstValueFrom(this.http.get<Row[]>('/api/institutions')),firstValueFrom(this.http.get<Row[]>('/api/routing-rules')),firstValueFrom(this.http.get<Row[]>('/api/iso-definitions'))]);this.institutions.set(institutions);this.rules.set(rules);this.definitions.set(definitions);for(const i of institutions){this.mappings.set(i.id,await firstValueFrom(this.http.get<Row[]>('/api/institutions/'+i.id+'/mappings')));this.selectedInput[i.id]||='pain.001.001.13';this.pickInput(i.id);}}catch(e){this.error.set(err(e));}finally{this.loading.set(false);}}
  inputs(){return this.definitions().filter(x=>x.family==='pain.001'&&x.status==='Verified');}
  choices(input:string){return this.definitions().find(x=>x.code===input)?.counterparts||[];}
  pickInput(id:number){this.selectedOutput[id]=this.mappings.get(id)?.find(x=>x.inputCode===this.selectedInput[id])?.outputCode||this.choices(this.selectedInput[id])[0]||'';}
  async setMapping(id:number){if(this.saving())return;this.saving.set('mapping'+id);this.error.set('');this.notice.set('');try{await firstValueFrom(this.http.put('/api/institutions/'+id+'/mappings',{inputCode:this.selectedInput[id],outputCode:this.selectedOutput[id]}));await this.load();this.notice.set('Response mapping saved. New imports will use this selection.');}catch(e){this.error.set(err(e));}finally{this.saving.set('');}}
  async setRule(currency:string,value:string){if(this.saving())return;this.saving.set(currency);this.error.set('');this.notice.set('');try{await firstValueFrom(this.http.put('/api/routing-rules',{currency,threshold:Number(value)}));await this.load();this.notice.set(currency+' threshold saved. Existing classifications are retained.');}catch(e){this.error.set(err(e));}finally{this.saving.set('');}}
}
