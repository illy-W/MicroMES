import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideAlarmClock, lucideCircleAlert, lucideClock, lucideCircleCheck, lucideCircleDot, lucideLock, lucideLockOpen, lucideMove, lucideOctagonX, lucidePause, lucidePlay, lucideTriangleAlert, lucideX } from '@ng-icons/lucide';
import type { TimelineWorkOrderModel, WorkOrderDetailModel, WorkOrderStatus } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { toFailure } from '../materials/materials.service';
import { workStatus } from '../production-orders/production-order-labels';
import { WorkOrderMachineDialogComponent } from '../work-orders/work-order-machine-dialog.component';
import { WorkOrdersStore } from '../work-orders/work-orders.store';
import { runPlanLockAction } from './plan-lock-actions';
import { staleCauseLabel } from './plan-lock';
import { PlanMoveDialogComponent, type MoveTarget } from './plan-move-dialog.component';
import { PlanningStore } from './planning.store';
import { formatDateTime } from './timeline-time';

/** Collegamento di dipendenza mostrato nel pannello. */
export interface DepLink {
  id: number;
  code: string;
  status: WorkOrderStatus | null;
  /** Nome della fase (se noto). */
  phase: string | null;
  /** Inizio/fine del lavoro collegato (effettivi se in corso o terminato, altrimenti pianificati). */
  startMs: number | null;
  endMs: number | null;
  /** Quando finisce chi precede (predecessore: la sua fine; successore: la fine di questo lavoro). */
  fromMs: number | null;
  /** Quando inizia chi segue (predecessore: l'inizio di questo lavoro; successore: il suo inizio). */
  toMs: number | null;
  /** Minuti di sovrapposizione se chi segue inizia prima della fine di chi precede. */
  conflictMin: number | null;
}
export interface DepLinks {
  preds: DepLink[];
  succs: DepLink[];
  up: number;
  down: number;
}

/** Intervallo di un lavoro: effettivo se in corso o terminato, altrimenti pianificato. */
function rangeOf(status: WorkOrderStatus, ps?: string | null, pe?: string | null, as?: string | null, ae?: string | null): { s: number; e: number } | null {
  const running = status === 'Running' || status === 'Ended';
  const s0 = (running && as) || ps;
  if (!s0) return null;
  const s = new Date(s0).getTime();
  const e0 = (status === 'Ended' && ae) || pe || ae;
  const e = e0 ? new Date(e0).getTime() : s + 30 * 60_000;
  return { s, e: Math.max(e, s + 60_000) };
}

const PLAN_LABEL: Record<string, string> = { Unplanned: 'Non pianificato', Planned: 'Pianificato', Late: 'In ritardo', Stale: 'Piano obsoleto' };
const ASSIGNABLE = ['Waiting', 'Ready', 'Interrupted'];

/** Pannello laterale con il dettaglio di un lavoro selezionato in timeline. */
@Component({
  selector: 'app-planning-work-panel',
  imports: [HlmBadge, HlmButton, HlmSkeleton, NgIcon, NgTemplateOutlet, PlanMoveDialogComponent, RouterLink, WorkOrderMachineDialogComponent],
  providers: [provideIcons({ lucideAlarmClock, lucideCircleAlert, lucideClock, lucideCircleCheck, lucideCircleDot, lucideLock, lucideLockOpen, lucideMove, lucideOctagonX, lucidePause, lucidePlay, lucideTriangleAlert, lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <aside class="flex flex-col gap-4 p-3 text-sm" aria-labelledby="wo-panel-title">
      <div class="flex items-start justify-between gap-2">
        <div class="min-w-0">
          <p id="wo-panel-title" class="truncate text-base leading-6 font-medium">{{ item().code }}</p>
          <p class="text-muted-foreground truncate">{{ item().processSegmentName }} · {{ item().materialName }}</p>
        </div>
        <button hlmBtn type="button" variant="ghost" size="icon-sm" aria-label="Chiudi dettaglio" (click)="closed.emit()">
          <ng-icon name="lucideX" aria-hidden="true" />
        </button>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <span class="inline-flex items-center gap-1 font-medium" [class]="st().tone">
          <ng-icon [name]="st().icon" size="14" aria-hidden="true" /> {{ st().label }}
        </span>
        <span hlmBadge variant="outline">{{ planLabel() }}</span>
        @if (item().isPlanLocked) {
          <span hlmBadge variant="secondary"><ng-icon name="lucideLock" size="12" aria-hidden="true" /> Fissato{{ item().planLockedAt ? ' il ' + fmt(item().planLockedAt) : '' }}</span>
        }
        @if (item().isLate) {
          <span hlmBadge variant="destructive"><ng-icon name="lucideAlarmClock" size="12" aria-hidden="true" /> in ritardo{{ item().lateDays > 0 ? ' di ' + item().lateDays + ' g' : '' }}</span>
        }
      </div>

      @if (reasonText(); as r) {
        <p class="text-muted-foreground flex items-start gap-2" role="status">
          <ng-icon name="lucideTriangleAlert" size="14" class="mt-0.5 shrink-0" aria-hidden="true" /> <span>{{ r }}</span>
        </p>
      }

      <dl class="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
        <dt class="text-muted-foreground">Ordine</dt>
        <dd>{{ item().productionOrderCode }}</dd>
        <dt class="text-muted-foreground">Macchina</dt>
        <dd>{{ machineName() }}</dd>
        <dt class="text-muted-foreground">Inizio previsto</dt>
        <dd>{{ fmt(item().plannedStartAt) }}</dd>
        <dt class="text-muted-foreground">Fine prevista</dt>
        <dd>{{ fmt(item().plannedEndAt) }}</dd>
        @if (item().actualStartAt) {
          <dt class="text-muted-foreground">Inizio effettivo</dt>
          <dd>{{ fmt(item().actualStartAt) }}</dd>
        }
        @if (item().actualEndAt) {
          <dt class="text-muted-foreground">Fine effettiva</dt>
          <dd>{{ fmt(item().actualEndAt) }}</dd>
        }
        @if (detail(); as d) {
          <dt class="text-muted-foreground">Minuti</dt>
          <dd>{{ d.plannedMinutes }} previsti · {{ d.workedMinutes }} lavorati</dd>
        }
      </dl>

      <section class="flex flex-col gap-1.5" aria-label="Dipendenze">
        <p class="text-muted-foreground text-xs font-medium tracking-wide uppercase">Dipendenze</p>
        @if (links(); as l) {
          <p class="text-muted-foreground text-xs">
            Prima: {{ l.up }} {{ l.up === 1 ? 'precedente' : 'precedenti' }} → <span class="text-foreground font-medium">{{ item().code }}</span> → Dopo: {{ l.down }} {{ l.down === 1 ? 'successivo' : 'successivi' }}
          </p>
        }
        <p class="text-sm font-medium">Predecessori</p>
        @if (preds().length) {
          <ul class="flex flex-col gap-0.5">
            @for (p of preds(); track p.id) {
              <li>
                <button type="button" class="hover:bg-muted focus-visible:ring-ring flex w-full flex-col rounded px-1 py-1 text-left focus-visible:ring-2 focus-visible:outline-none" (click)="selectLink.emit(p.id)">
                  <ng-container *ngTemplateOutlet="linkRow; context: { $implicit: p, kind: 'pred' }" />
                </button>
              </li>
            }
          </ul>
        } @else if (loading()) {
          <hlm-skeleton class="h-6 w-full" aria-busy="true" aria-label="Caricamento dettaglio" />
        } @else {
          <p class="text-muted-foreground">Nessuno</p>
        }
        <p class="mt-1 text-sm font-medium">Successori</p>
        @if (succs().length) {
          <ul class="flex flex-col gap-0.5">
            @for (p of succs(); track p.id) {
              <li>
                <button type="button" class="hover:bg-muted focus-visible:ring-ring flex w-full flex-col rounded px-1 py-1 text-left focus-visible:ring-2 focus-visible:outline-none" (click)="selectLink.emit(p.id)">
                  <ng-container *ngTemplateOutlet="linkRow; context: { $implicit: p, kind: 'succ' }" />
                </button>
              </li>
            }
          </ul>
        } @else if (loading()) {
          <hlm-skeleton class="h-6 w-full" aria-busy="true" aria-label="Caricamento dettaglio" />
        } @else {
          <p class="text-muted-foreground">Nessuno</p>
        }
        @if (error()) {
          <p class="text-destructive" role="alert">{{ error() }}</p>
        }
      </section>

      <section class="flex flex-col gap-1.5" aria-label="Segnalazioni">
        <p class="text-muted-foreground text-xs font-medium tracking-wide uppercase">Segnalazioni</p>
        @if (alerts().length) {
          <ul class="flex flex-col gap-1">
            @for (a of alerts(); track $index) {
              <li class="flex items-start gap-2">
                <ng-icon [name]="a.severity === 'Critical' ? 'lucideCircleAlert' : 'lucideTriangleAlert'" [class.text-destructive]="a.severity === 'Critical'" size="14" class="mt-0.5 shrink-0" aria-hidden="true" />
                <span><span class="sr-only">{{ a.severity === 'Critical' ? 'Critica: ' : 'Avviso: ' }}</span>{{ a.message }}</span>
              </li>
            }
          </ul>
        } @else {
          <p class="text-muted-foreground">Nessuna segnalazione</p>
        }
      </section>

      <div class="flex flex-wrap gap-2">
        @if (canAssign()) {
          <button hlmBtn type="button" size="sm" (click)="assigning.set(true)">Assegna macchina</button>
        }
        @if (can('Lock')) {
          <button hlmBtn type="button" size="sm" variant="outline" [disabled]="acting()" (click)="act('lock')"><ng-icon name="lucideLock" aria-hidden="true" /> Fissa</button>
        }
        @if (can('Move') && detail()) {
          <button hlmBtn type="button" size="sm" variant="outline" (click)="moving.set(true)"><ng-icon name="lucideMove" aria-hidden="true" /> Sposta…</button>
        }
        @if (can('Unlock')) {
          <button hlmBtn type="button" size="sm" variant="outline" [disabled]="acting()" (click)="act('unlock')"><ng-icon name="lucideLockOpen" aria-hidden="true" /> Sblocca</button>
        }
        <a hlmBtn variant="outline" size="sm" [routerLink]="['/production-orders', item().productionOrderId]">Apri ordine</a>
        <a hlmBtn variant="outline" size="sm" routerLink="/work-orders" [queryParams]="{ order: item().productionOrderId }">Apri in Ordini di lavoro</a>
      </div>
    </aside>

    <ng-template #linkRow let-p let-kind="kind">
      <span class="flex items-center gap-2">
        @if (p.status; as s) {
          @let ps = status(s);
          <ng-icon [name]="ps.icon" size="14" [class]="ps.tone" aria-hidden="true" />
          <span class="font-medium">{{ p.code }}</span><span class="text-muted-foreground truncate">· {{ ps.label }}</span>
        } @else {
          <span class="font-medium">{{ p.code }}</span>
        }
      </span>
      @if (p.phase) {
        <span class="text-muted-foreground truncate text-xs">{{ p.phase }}</span>
      }
      <span class="text-muted-foreground text-xs">{{ datesText(p) }}</span>
      @if (gapText(p, kind); as g) {
        <span class="text-xs" [class.text-destructive]="p.conflictMin" [class.text-muted-foreground]="!p.conflictMin">{{ g }}</span>
      }
    </ng-template>

    @if (moving() && detail(); as d) {
      <app-plan-move-dialog [work]="moveTarget(d)" (done)="moving.set(false); onAssigned()" (closed)="moving.set(false)" />
    }
    @if (assigning() && detail(); as d) {
      <app-work-order-machine-dialog [work]="d" mode="Assign" (done)="assigning.set(false); onAssigned()" (closed)="assigning.set(false)" />
    }
  `,
})
export class PlanningWorkPanelComponent {
  private readonly orders = inject(WorkOrdersStore);
  private readonly planning = inject(PlanningStore);

  readonly item = input.required<TimelineWorkOrderModel>();
  readonly timeZone = input.required<string>();
  readonly machineLabels = input.required<ReadonlyMap<number, string>>();
  readonly links = input<DepLinks | null>(null);
  readonly selectLink = output<number>();
  readonly closed = output<void>();
  readonly changed = output<void>();

  protected readonly detail = signal<WorkOrderDetailModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly assigning = signal(false);
  protected readonly moving = signal(false);
  protected readonly acting = signal(false);
  protected readonly reasonText = computed(() => (this.item().isPlanLocked || this.item().planStatus === 'Stale' ? staleCauseLabel(this.item().planStatusReason) : null));
  protected readonly status = workStatus;
  protected readonly st = computed(() => workStatus(this.item().status));
  protected readonly planLabel = computed(() => PLAN_LABEL[this.item().planStatus] ?? this.item().planStatus);
  protected readonly canAssign = computed(() => ASSIGNABLE.includes(this.item().status) && !!this.detail());
  protected readonly machineName = computed(() => {
    const id = this.item().equipmentId;
    return id == null ? 'Nessuna macchina' : (this.machineLabels().get(id) ?? `#${id}`);
  });
  /** Dettagli dei lavori collegati non presenti nella finestra caricata (cache per id). */
  private readonly linked = signal<ReadonlyMap<number, WorkOrderDetailModel>>(new Map());
  private readonly requested = new Set<number>();
  private readonly me = computed(() => {
    const w = this.item();
    return rangeOf(w.status, w.plannedStartAt, w.plannedEndAt, w.actualStartAt, w.actualEndAt);
  });

  private readonly baseLinks = computed(() => {
    const d = this.detail();
    const blank = (l: { id: number; code: string; status: WorkOrderStatus }): DepLink => ({ id: l.id, code: l.code, status: l.status, phase: null, startMs: null, endMs: null, fromMs: null, toMs: null, conflictMin: null });
    const merge = (known: DepLink[], extra: { id: number; code: string; status: WorkOrderStatus }[]): DepLink[] => [
      ...known.map((k) => (k.status ? k : { ...k, status: extra.find((p) => p.id === k.id)?.status ?? null })),
      ...extra.filter((p) => !known.some((k) => k.id === p.id)).map(blank),
    ];
    return { preds: merge(this.links()?.preds ?? [], d?.predecessors ?? []), succs: merge(this.links()?.succs ?? [], d?.successors ?? []) };
  });

  /** Completa i collegamenti fuori finestra con il dettaglio caricato (codice, fase, stato, date). */
  private enrich(l: DepLink, kind: 'pred' | 'succ'): DepLink {
    if (l.startMs != null || l.endMs != null) return l;
    const d = this.linked().get(l.id);
    if (!d) return l;
    const r = rangeOf(d.status, d.plannedStartAt, d.plannedEndAt, d.startedAt, d.endedAt);
    const me = this.me();
    const from = kind === 'pred' ? (r?.e ?? null) : (me?.e ?? null);
    const to = kind === 'pred' ? (me?.s ?? null) : (r?.s ?? null);
    const gap = from != null && to != null ? Math.round((from - to) / 60_000) : 0;
    return { ...l, code: d.code, status: d.status, phase: d.processSegmentName || d.processSegmentCode, startMs: r?.s ?? null, endMs: r?.e ?? null, fromMs: from, toMs: to, conflictMin: gap > 0 ? gap : null };
  }
  protected readonly preds = computed(() => this.baseLinks().preds.map((l) => this.enrich(l, 'pred')));
  protected readonly succs = computed(() => this.baseLinks().succs.map((l) => this.enrich(l, 'succ')));

  protected readonly alerts = computed(() => {
    const w = this.item();
    return this.planning.alerts().filter((a) => a.workOrderId === w.id || (a.workOrderId == null && a.productionOrderId === w.productionOrderId));
  });

  private seq = 0;
  private loadedId: number | null = null;

  constructor() {
    effect(() => {
      const id = this.item().id;
      if (id !== this.loadedId) {
        this.loadedId = id;
        this.detail.set(null);
        void this.loadDetail(id);
      }
    });
    effect(() => {
      const b = this.baseLinks();
      const missing = [...b.preds, ...b.succs].filter((l) => l.startMs == null && l.endMs == null && !this.requested.has(l.id));
      untracked(() => {
        for (const l of missing) void this.loadLinked(l.id);
      });
    });
  }

  /** Date del lavoro collegato: pianificate, oppure effettive se in corso/terminato. */
  protected datesText(l: DepLink): string {
    if (l.startMs == null || l.endMs == null) return this.requested.has(l.id) && !this.linked().has(l.id) ? 'Caricamento date…' : 'Date non pianificate';
    const tz = this.timeZone();
    if (l.status === 'Ended') return `Terminato il ${formatDateTime(l.endMs, tz)}`;
    if (l.status === 'Running') return `Iniziato il ${formatDateTime(l.startMs, tz)}, in corso`;
    return `${formatDateTime(l.startMs, tz)} → ${formatDateTime(l.endMs, tz)}`;
  }

  protected gapText(l: DepLink, kind: 'pred' | 'succ'): string {
    if (l.fromMs == null || l.toMs == null) return '';
    if (l.conflictMin) return `In conflitto di ${l.conflictMin} min`;
    return kind === 'pred'
      ? `Finisce ${formatDateTime(l.fromMs, this.timeZone())} → questo inizia ${formatDateTime(l.toMs, this.timeZone())}`
      : `Questo finisce ${formatDateTime(l.fromMs, this.timeZone())} → inizia ${formatDateTime(l.toMs, this.timeZone())}`;
  }

  protected can(action: string): boolean {
    return !!this.detail()?.allowedActions.includes(action);
  }

  protected moveTarget(d: WorkOrderDetailModel): MoveTarget {
    const w = this.item();
    return { id: w.id, code: w.code, processSegmentId: d.processSegmentId, plannedStartAt: w.plannedStartAt, equipmentId: w.equipmentId };
  }

  protected async act(kind: 'lock' | 'unlock'): Promise<void> {
    this.acting.set(true);
    try {
      if (await runPlanLockAction(this.orders, kind, this.item().id)) this.onAssigned();
    } finally {
      this.acting.set(false);
    }
  }

  protected fmt(v?: string | null): string {
    return formatDateTime(v, this.timeZone());
  }

  protected onAssigned(): void {
    void this.loadDetail(this.item().id);
    void this.planning.loadAlerts();
    this.changed.emit();
  }

  private async loadDetail(id: number): Promise<void> {
    const n = ++this.seq;
    this.loading.set(true);
    this.error.set(null);
    try {
      const d = await this.orders.getDetail(id);
      if (n === this.seq) this.detail.set(d);
    } catch (e) {
      if (n === this.seq) this.error.set(toFailure(e).message);
    } finally {
      if (n === this.seq) this.loading.set(false);
    }
  }

  private async loadLinked(id: number): Promise<void> {
    this.requested.add(id);
    try {
      const d = await this.orders.getDetail(id);
      this.linked.update((m) => new Map(m).set(id, d));
    } catch {
      /* resta "Date non pianificate": il collegamento è comunque cliccabile */
    }
  }
}
