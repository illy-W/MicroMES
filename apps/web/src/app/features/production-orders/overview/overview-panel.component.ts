import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarClock,
  lucideCircleCheck,
  lucideCircleDot,
  lucideCircleOff,
  lucideClock,
  lucideLock,
  lucideOctagonX,
  lucidePause,
  lucidePlay,
  lucideSend,
} from '@ng-icons/lucide';
import type {
  PlanStatus,
  ProductionOrderOverviewModel,
  ProductionOrderOverviewOrderModel,
  ProductionOrderOverviewWorkOrderModel,
} from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmProgressImports } from '@spartan-ng/helm/progress';
import { CalendarsStore } from '../../calendars/calendars.store';
import { PlanStatusBadgeComponent } from '../../planning/plan-status-badge.component';
import { formatDateTime } from '../../planning/timeline-time';
import { unitLabel } from '../../materials/material-labels';
import { formatMinutes } from '../../routing/routing-metrics';
import { formatQty } from '../production-order-labels';
import { orderStatusStyle, workProgressPercent, workStatusStyle } from './overview-model';

/** Elemento selezionato nel diagramma: lavoro o ordine figlio (gruppo). */
export interface OverviewSelection {
  kind: 'work' | 'order';
  id: number;
}

interface LinkedWork {
  work: ProductionOrderOverviewWorkOrderModel;
  orderCode: string;
}

/**
 * Pannello laterale di sola lettura: dettagli del lavoro o dell'ordine figlio selezionato, calcolati
 * dall'ultima panoramica (si aggiorna da solo con "Aggiorna"). Nessun comando: solo link.
 */
@Component({
  selector: 'app-overview-panel',
  imports: [RouterLink, PlanStatusBadgeComponent, NgIcon, HlmBadge, HlmButton, HlmCardImports, HlmProgressImports],
  providers: [
    provideIcons({
      lucideCalendarClock,
      lucideCircleCheck,
      lucideCircleDot,
      lucideCircleOff,
      lucideClock,
      lucideLock,
      lucideOctagonX,
      lucidePause,
      lucidePlay,
      lucideSend,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (work(); as w) {
      @let st = workStyle(w.status);
      <section hlmCard aria-labelledby="ov-panel-title">
        <div hlmCardHeader>
          <h2 hlmCardTitle id="ov-panel-title">Lavoro {{ w.code }}</h2>
          <p hlmCardDescription>Op. {{ w.operationNumber }} · {{ w.processSegmentName }}</p>
        </div>
        <div hlmCardContent class="flex flex-col gap-4">
          <span hlmBadge variant="outline" class="w-fit px-1.5" [class]="st.tone">
            <ng-icon [name]="st.icon" aria-hidden="true" /> {{ st.label }}
          </span>
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <dt class="text-muted-foreground">Ordine</dt>
            <dd>{{ workOrder()?.code }}</dd>
            <dt class="text-muted-foreground">Fase</dt>
            <dd>{{ w.processSegmentCode }}</dd>
            @if (w.assignedEquipment || (w.status === 'Running' && w.currentEquipment)) {
              @if (w.assignedEquipment; as eq) {
                <dt class="text-muted-foreground">Macchina assegnata</dt>
                <dd>
                  <span class="font-medium">{{ eq.code }}</span>
                  <span class="text-muted-foreground"> {{ eq.name }}</span>
                  @if (!eq.isEnabled) {
                    <span hlmBadge variant="outline" class="text-destructive ml-1 px-1.5">non più abilitata</span>
                  }
                </dd>
              }
              @if (w.status === 'Running' && w.currentEquipment; as cur) {
                <dt class="text-muted-foreground">Macchina in uso</dt>
                <dd>
                  <span class="font-medium">{{ cur.code }}</span>
                  <span class="text-muted-foreground"> {{ cur.name }}</span>
                </dd>
              }
            }
            <dt class="text-muted-foreground">Piano</dt>
            <dd class="flex flex-wrap items-center gap-x-2 gap-y-1">
              @if (w.plannedStartAt && w.plannedEndAt) {
                <span class="tabular-nums">{{ fmt(w.plannedStartAt) }} – {{ fmt(w.plannedEndAt) }}</span>
              } @else {
                <span class="text-muted-foreground">—</span>
              }
              <app-plan-status-badge [status]="w.planStatus" [hide]="hiddenPlan(w)" />
              @if (w.isPlanLocked) {
                <span hlmBadge variant="outline" class="gap-1 px-1.5"><ng-icon name="lucideLock" size="12" aria-hidden="true" /> Fissato</span>
              }
            </dd>
            <dt class="text-muted-foreground">Pianificato</dt>
            <dd class="tabular-nums">{{ qty(w.plannedQuantity) }} {{ unit(workOrder()?.unitOfMeasure) }}</dd>
            <dt class="text-muted-foreground">Buoni</dt>
            <dd class="flex items-center gap-2 tabular-nums">
              {{ qty(w.goodQuantity) }}
              <hlm-progress class="w-16" [value]="percent(w)" aria-label="Avanzamento del lavoro">
                <hlm-progress-indicator />
              </hlm-progress>
            </dd>
            <dt class="text-muted-foreground">Scarto</dt>
            <dd class="tabular-nums">{{ qty(w.scrapQuantity) }}</dd>
            <dt class="text-muted-foreground">Tempo pianificato</dt>
            <dd class="tabular-nums">{{ minutes(w.plannedMinutes) }}</dd>
            <dt class="text-muted-foreground">Tempo lavorato</dt>
            <dd class="tabular-nums">{{ minutes(w.workedMinutes) }}</dd>
          </dl>
          <div class="flex flex-col gap-3">
            <div>
              <h3 class="text-muted-foreground mb-1 text-xs font-medium">Predecessori</h3>
              @for (p of predecessors(); track p.work.id) {
                <p class="flex flex-wrap items-center gap-x-2 text-sm">
                  <span class="font-medium">{{ p.work.code }}</span>
                  <span class="text-muted-foreground text-xs">{{ p.orderCode }}</span>
                  <span class="flex items-center gap-1 text-xs" [class]="workStyle(p.work.status).tone">
                    <ng-icon [name]="workStyle(p.work.status).icon" aria-hidden="true" />
                    {{ workStyle(p.work.status).label }}
                  </span>
                </p>
              } @empty {
                <p class="text-muted-foreground text-sm">Nessuno: il lavoro è iniziale.</p>
              }
            </div>
            <div>
              <h3 class="text-muted-foreground mb-1 text-xs font-medium">Successori</h3>
              @for (s of successors(); track s.work.id) {
                <p class="flex flex-wrap items-center gap-x-2 text-sm">
                  <span class="font-medium">{{ s.work.code }}</span>
                  <span class="text-muted-foreground text-xs">{{ s.orderCode }}</span>
                  <span class="flex items-center gap-1 text-xs" [class]="workStyle(s.work.status).tone">
                    <ng-icon [name]="workStyle(s.work.status).icon" aria-hidden="true" />
                    {{ workStyle(s.work.status).label }}
                  </span>
                </p>
              } @empty {
                <p class="text-muted-foreground text-sm">Nessuno: il lavoro è finale.</p>
              }
            </div>
          </div>
          <div>
            <h3 class="text-muted-foreground mb-1 text-xs font-medium">Materiali</h3>
            @for (m of w.materials; track m.id) {
              <p class="flex flex-wrap items-baseline gap-x-2 text-sm">
                <span class="font-medium">{{ m.materialCode }}</span>
                <span class="text-muted-foreground">{{ m.materialName }}</span>
                <span class="tabular-nums">{{ qty(m.requiredQuantity) }} {{ unit(m.unitOfMeasure) }}</span>
                @if (m.childProductionOrderId; as childId) {
                  <a hlmBtn variant="link" size="sm" class="h-auto p-0 text-xs" [routerLink]="['/production-orders', childId]">
                    Ordine {{ m.childProductionOrderCode }}
                  </a>
                }
              </p>
            } @empty {
              <p class="text-muted-foreground text-sm">Nessun materiale per questo lavoro</p>
            }
          </div>
          <div class="flex flex-wrap gap-2">
            <a hlmBtn variant="outline" size="sm" routerLink="/work-orders" [queryParams]="{ order: w.productionOrderId }">
              Apri in Ordini di lavoro
            </a>
            @if (isChildWork()) {
              <a hlmBtn variant="outline" size="sm" [routerLink]="['/production-orders', w.productionOrderId]">
                Apri ordine figlio
              </a>
            }
          </div>
        </div>
      </section>
    } @else if (order(); as o) {
      @let st = orderStyle(o.status);
      <section hlmCard aria-labelledby="ov-panel-title">
        <div hlmCardHeader>
          <h2 hlmCardTitle id="ov-panel-title">Ordine {{ o.code }}</h2>
          <p hlmCardDescription>{{ o.materialCode }} · {{ o.materialName }}</p>
        </div>
        <div hlmCardContent class="flex flex-col gap-4">
          <span hlmBadge variant="outline" class="w-fit px-1.5" [class]="st.tone">
            <ng-icon [name]="st.icon" aria-hidden="true" /> {{ st.label }}
          </span>
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <dt class="text-muted-foreground">Quantità richiesta</dt>
            <dd class="tabular-nums">{{ qty(o.quantity) }} {{ unit(o.unitOfMeasure) }}</dd>
            <dt class="text-muted-foreground">Quantità prodotta</dt>
            <dd class="flex items-center gap-2 tabular-nums">
              {{ qty(o.producedQuantity) }} {{ unit(o.unitOfMeasure) }}
              <hlm-progress class="w-16" [value]="o.progressPercent" aria-label="Avanzamento dell’ordine">
                <hlm-progress-indicator />
              </hlm-progress>
              <span class="text-muted-foreground text-xs">{{ o.progressPercent }}%</span>
            </dd>
            <dt class="text-muted-foreground">Lavori</dt>
            <dd class="tabular-nums">{{ orderWorkCount() }}</dd>
          </dl>
          <div class="flex flex-wrap gap-2">
            <a hlmBtn variant="outline" size="sm" [routerLink]="['/production-orders', o.id]">Apri ordine</a>
            <a hlmBtn variant="outline" size="sm" routerLink="/work-orders" [queryParams]="{ order: o.id }">
              Apri in Ordini di lavoro
            </a>
          </div>
        </div>
      </section>
    } @else {
      <p class="text-muted-foreground text-sm">Seleziona un lavoro o un ordine figlio per vederne i dettagli.</p>
    }
  `,
})
export class OverviewPanelComponent {
  readonly overview = input.required<ProductionOrderOverviewModel>();
  readonly rootOrderId = input.required<number>();
  readonly selection = input<OverviewSelection | null>(null);

  constructor() {
    void inject(CalendarsStore).loadTimeZone();
  }

  private readonly calendars = inject(CalendarsStore);
  protected readonly fmt = (iso: string): string => formatDateTime(iso, this.calendars.timeZone());
  protected readonly hiddenPlan = (w: ProductionOrderOverviewWorkOrderModel): readonly PlanStatus[] =>
    ['Waiting', 'Ready', 'Interrupted', 'Running'].includes(w.status) ? ['Planned'] : ['Planned', 'Unplanned'];
  protected readonly qty = formatQty;
  protected readonly minutes = formatMinutes;
  protected readonly unit = (u?: string): string => (u ? unitLabel(u) : '');
  protected readonly workStyle = workStatusStyle;
  protected readonly orderStyle = orderStatusStyle;
  protected readonly percent = (w: ProductionOrderOverviewWorkOrderModel): number =>
    workProgressPercent(w.goodQuantity, w.plannedQuantity);

  private readonly orders = computed(() => new Map(this.overview().orders.map((o) => [o.id, o])));
  private readonly works = computed(() => new Map(this.overview().workOrders.map((w) => [w.id, w])));

  protected readonly work = computed<ProductionOrderOverviewWorkOrderModel | null>(() => {
    const s = this.selection();
    return s?.kind === 'work' ? (this.works().get(s.id) ?? null) : null;
  });

  protected readonly order = computed<ProductionOrderOverviewOrderModel | null>(() => {
    const s = this.selection();
    return s?.kind === 'order' ? (this.orders().get(s.id) ?? null) : null;
  });

  /** Ordine a cui appartiene il lavoro selezionato. */
  protected readonly workOrder = computed(() => {
    const w = this.work();
    return w ? (this.orders().get(w.productionOrderId) ?? null) : null;
  });

  protected readonly isChildWork = computed(() => {
    const w = this.work();
    return !!w && w.productionOrderId !== this.rootOrderId();
  });

  protected readonly orderWorkCount = computed(() => {
    const o = this.order();
    return o ? this.overview().workOrders.filter((w) => w.productionOrderId === o.id).length : 0;
  });

  protected readonly predecessors = computed<LinkedWork[]>(() => {
    const w = this.work();
    return w ? this.linked(this.overview().dependencies.filter((d) => d.successorWorkOrderId === w.id).map((d) => d.predecessorWorkOrderId)) : [];
  });

  protected readonly successors = computed<LinkedWork[]>(() => {
    const w = this.work();
    return w ? this.linked(this.overview().dependencies.filter((d) => d.predecessorWorkOrderId === w.id).map((d) => d.successorWorkOrderId)) : [];
  });

  private linked(ids: number[]): LinkedWork[] {
    return ids
      .map((id) => this.works().get(id))
      .filter((x): x is ProductionOrderOverviewWorkOrderModel => !!x)
      .sort((a, b) => a.operationNumber - b.operationNumber)
      .map((work) => ({ work, orderCode: this.orders().get(work.productionOrderId)?.code ?? '' }));
  }
}
