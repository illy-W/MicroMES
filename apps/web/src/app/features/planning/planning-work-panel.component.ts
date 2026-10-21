import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideAlarmClock, lucideCircleAlert, lucideClock, lucideCircleCheck, lucideCircleDot, lucideOctagonX, lucidePause, lucidePlay, lucideTriangleAlert, lucideX } from '@ng-icons/lucide';
import type { TimelineWorkOrderModel, WorkOrderDetailModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { toFailure } from '../materials/materials.service';
import { workStatus } from '../production-orders/production-order-labels';
import { WorkOrderMachineDialogComponent } from '../work-orders/work-order-machine-dialog.component';
import { WorkOrdersStore } from '../work-orders/work-orders.store';
import { PlanningStore } from './planning.store';
import { formatDateTime } from './timeline-time';

const PLAN_LABEL: Record<string, string> = { Unplanned: 'Non pianificato', Planned: 'Pianificato', Late: 'In ritardo', Stale: 'Piano obsoleto' };
const ASSIGNABLE = ['Waiting', 'Ready', 'Interrupted'];

/** Pannello laterale con il dettaglio di un lavoro selezionato in timeline. */
@Component({
  selector: 'app-planning-work-panel',
  imports: [HlmBadge, HlmButton, HlmSkeleton, NgIcon, RouterLink, WorkOrderMachineDialogComponent],
  providers: [provideIcons({ lucideAlarmClock, lucideCircleAlert, lucideClock, lucideCircleCheck, lucideCircleDot, lucideOctagonX, lucidePause, lucidePlay, lucideTriangleAlert, lucideX })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="flex flex-col gap-3 rounded-md border p-3 text-sm" aria-labelledby="wo-panel-title">
      <div class="flex items-start justify-between gap-2">
        <div class="min-w-0">
          <h3 id="wo-panel-title" class="truncate text-base font-semibold">{{ item().code }}</h3>
          <p class="text-muted-foreground truncate">{{ item().processSegmentName }} · {{ item().materialName }}</p>
        </div>
        <button hlmBtn type="button" variant="ghost" size="icon" aria-label="Chiudi dettaglio" (click)="closed.emit()">
          <ng-icon name="lucideX" aria-hidden="true" />
        </button>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <span class="inline-flex items-center gap-1 font-medium" [class]="st().tone">
          <ng-icon [name]="st().icon" size="14" aria-hidden="true" /> {{ st().label }}
        </span>
        <span hlmBadge variant="outline">{{ planLabel() }}</span>
        @if (item().isLate) {
          <span hlmBadge variant="destructive"><ng-icon name="lucideAlarmClock" size="12" aria-hidden="true" /> in ritardo{{ item().lateDays > 0 ? ' di ' + item().lateDays + ' g' : '' }}</span>
        }
      </div>

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

      @if (loading()) {
        <hlm-skeleton class="h-10 w-full" aria-busy="true" aria-label="Caricamento dettaglio" />
      } @else if (error()) {
        <p class="text-destructive" role="alert">{{ error() }}</p>
      } @else if (detail(); as d) {
        <div class="flex flex-col gap-1">
          <h4 class="font-medium">Predecessori</h4>
          @if (d.predecessors.length) {
            <ul class="flex flex-col gap-0.5">
              @for (p of d.predecessors; track p.id) {
                @let ps = status(p.status);
                <li class="flex items-center gap-2">
                  <ng-icon [name]="ps.icon" size="14" [class]="ps.tone" aria-hidden="true" />
                  <span>{{ p.code }}</span><span class="text-muted-foreground">· {{ ps.label }}</span>
                </li>
              }
            </ul>
          } @else {
            <p class="text-muted-foreground">Nessuno</p>
          }
        </div>
      }

      <div class="flex flex-col gap-1">
        <h4 class="font-medium">Segnalazioni</h4>
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
      </div>

      <div class="flex flex-wrap gap-2">
        @if (canAssign()) {
          <button hlmBtn type="button" size="sm" (click)="assigning.set(true)">Assegna macchina</button>
        }
        <a hlmBtn variant="outline" size="sm" [routerLink]="['/production-orders', item().productionOrderId]">Apri ordine</a>
        <a hlmBtn variant="outline" size="sm" routerLink="/work-orders" [queryParams]="{ order: item().productionOrderId }">Apri in Ordini di lavoro</a>
      </div>
    </aside>

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
  readonly closed = output<void>();
  readonly changed = output<void>();

  protected readonly detail = signal<WorkOrderDetailModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly assigning = signal(false);
  protected readonly status = workStatus;
  protected readonly st = computed(() => workStatus(this.item().status));
  protected readonly planLabel = computed(() => PLAN_LABEL[this.item().planStatus] ?? this.item().planStatus);
  protected readonly canAssign = computed(() => ASSIGNABLE.includes(this.item().status) && !!this.detail());
  protected readonly machineName = computed(() => {
    const id = this.item().equipmentId;
    return id == null ? 'Nessuna macchina' : (this.machineLabels().get(id) ?? `#${id}`);
  });
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
}
