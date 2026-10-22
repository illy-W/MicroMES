import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCircleCheck,
  lucideCircleDot,
  lucideClock,
  lucideListChecks,
  lucideLock,
  lucideOctagonX,
  lucidePause,
  lucidePlay,
  lucideSearch,
  lucideSearchX,
  lucideTriangleAlert,
  lucideX,
} from '@ng-icons/lucide';
import {
  columnVisibilityFeature,
  createColumnHelper,
  createPaginatedRowModel,
  injectTable,
  type PaginationState,
  rowPaginationFeature,
  tableFeatures,
} from '@tanstack/angular-table';
import type { PlanStatus, WorkOrderModel } from '@micromes/api-client/models';
import { WorkOrderStatus } from '@micromes/api-client/models';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { TablePaginatorComponent } from '../../shared/components/table-paginator.component';
import { EquipmentStore } from '../equipment/equipment.store';
import { ProcessSegmentsStore } from '../process-segments/process-segments.store';
import { unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { formatQty, WORK_STATUS, workStatus } from '../production-orders/production-order-labels';
import { formatMinutes } from '../routing/routing-metrics';
import { CalendarsStore } from '../calendars/calendars.store';
import { PlanStatusBadgeComponent } from '../planning/plan-status-badge.component';
import { formatDateTime } from '../planning/timeline-time';
import { runPlanLockAction } from '../planning/plan-lock-actions';
import { PlanMoveDialogComponent, type MoveTarget } from '../planning/plan-move-dialog.component';
import { WorkOrderAbortDialogComponent } from './work-order-abort-dialog.component';
import { type CloseMode, WorkOrderCloseDialogComponent } from './work-order-close-dialog.component';
import { machineUsage, usageText } from './work-order-machines';
import { type MachineMode, WorkOrderMachineDialogComponent } from './work-order-machine-dialog.component';
import { WorkOrderSessionsDialogComponent } from './work-order-sessions-dialog.component';
import { WorkOrderRowActionsComponent } from './work-order-row-actions.component';
import { WorkOrdersStore } from './work-orders.store';

const features = tableFeatures({
  columnVisibilityFeature,
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
});

const columnHelper = createColumnHelper<typeof features, WorkOrderModel>();

const columns = columnHelper.columns([
  columnHelper.accessor('code', { id: 'code', header: 'Codice lavoro' }),
  columnHelper.accessor('processSegmentName', { id: 'phase', header: 'Fase' }),
  columnHelper.display({ id: 'equipment', header: 'Macchina' }),
  columnHelper.accessor('materialCode', { id: 'material', header: 'Materiale prodotto' }),
  columnHelper.accessor('plannedQuantity', { id: 'planned', header: 'Quantità pianificata' }),
  columnHelper.accessor('status', { id: 'status', header: 'Stato' }),
  columnHelper.display({ id: 'plannedStart', header: 'Inizio previsto' }),
  columnHelper.display({ id: 'plannedEnd', header: 'Fine prevista' }),
  columnHelper.accessor('goodQuantity', { id: 'good', header: 'Buono' }),
  columnHelper.accessor('scrapQuantity', { id: 'scrap', header: 'Scarto' }),
  columnHelper.accessor('plannedMinutes', { id: 'plannedTime', header: 'Tempo pianificato' }),
  columnHelper.accessor('workedMinutes', { id: 'workedTime', header: 'Tempo reale' }),
  columnHelper.display({ id: 'actions', header: 'Azioni' }),
]);

const ALL = 'all';
const OPERATIONAL = 'operational';
const OPERATIONAL_STATUSES: string[] = [WorkOrderStatus.Ready, WorkOrderStatus.Running, WorkOrderStatus.Interrupted];
const isOpen = (w: WorkOrderModel): boolean => ['Waiting', 'Ready', 'Interrupted', 'Running'].includes(w.status);
const NUMERIC_COLUMNS = new Set(['planned', 'good', 'scrap', 'plannedTime', 'workedTime']);

@Component({
  selector: 'app-work-orders',
  imports: [
    HlmTableImports,
    HlmEmptyImports,
    HlmSelectImports,
    HlmBadge,
    HlmButton,
    HlmInput,
    HlmSkeleton,
    HlmSwitch,
    NgIcon,
    TablePaginatorComponent,
    PlanStatusBadgeComponent,
    WorkOrderRowActionsComponent,
    WorkOrderCloseDialogComponent,
    WorkOrderAbortDialogComponent,
    WorkOrderSessionsDialogComponent,
    WorkOrderMachineDialogComponent,
    PlanMoveDialogComponent,
  ],
  providers: [
    provideIcons({
      lucideCircleCheck,
      lucideCircleDot,
      lucideClock,
      lucideListChecks,
      lucideLock,
      lucideOctagonX,
      lucidePause,
      lucidePlay,
      lucideSearch,
      lucideSearchX,
      lucideTriangleAlert,
      lucideX,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold">Ordini di lavoro</h1>
        <p class="text-muted-foreground text-sm">Lavori da eseguire, con comandi di avvio, pausa e chiusura.</p>
      </div>

      <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
        <div class="relative">
          <ng-icon
            name="lucideSearch"
            class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2"
            aria-hidden="true"
          />
          <input
            hlmInput
            class="h-8 w-80 pl-8"
            type="search"
            placeholder="Cerca per codice, fase o materiale"
            aria-label="Cerca lavori"
            [value]="search()"
            (input)="search.set($any($event.target).value)"
          />
        </div>
        <hlm-select [value]="statusFilter()" [itemToString]="statusLabel" (valueChange)="statusFilter.set($event ?? 'operational')">
          <hlm-select-trigger size="sm" class="w-48" aria-label="Filtra per stato">
            <hlm-select-value />
          </hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="operational">Operativi</hlm-select-item>
            <hlm-select-item value="all">Tutti</hlm-select-item>
            @for (o of statusOptions; track o.value) {
              <hlm-select-item [value]="o.value">{{ o.label }}</hlm-select-item>
            }
          </hlm-select-content>
        </hlm-select>
        <div class="flex items-center gap-2">
          <hlm-select [value]="orderFilter()" [itemToString]="orderLabel" (valueChange)="orderFilter.set($event ?? 'all')">
            <hlm-select-trigger size="sm" class="w-48" aria-label="Filtra per ordine di produzione">
              <hlm-select-value />
            </hlm-select-trigger>
            <hlm-select-content *hlmSelectPortal>
              <hlm-select-item value="all">Tutti gli ordini</hlm-select-item>
              @for (o of orderOptions(); track o.id) {
                <hlm-select-item [value]="o.id">{{ o.code }}</hlm-select-item>
              }
            </hlm-select-content>
          </hlm-select>
          @if (orderFilter() !== 'all') {
            <button hlmBtn size="sm" variant="ghost" type="button" (click)="clearOrderFilter()">
              <ng-icon name="lucideX" /> Rimuovi filtro
            </button>
          }
        </div>
        <hlm-select [value]="planFilter()" [itemToString]="planLabel" (valueChange)="planFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-48" aria-label="Filtra per piano">
            <hlm-select-value />
          </hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            @for (o of planOptions; track o.value) {
              <hlm-select-item [value]="o.value">{{ o.label }}</hlm-select-item>
            }
          </hlm-select-content>
        </hlm-select>
        <label class="flex items-center gap-2 text-sm" for="wo-only-locked">
          <hlm-switch inputId="wo-only-locked" [checked]="onlyLocked()" (checkedChange)="setOnlyLocked($event)" />
          Solo fissati
        </label>
        <label class="flex items-center gap-2 text-sm" for="wo-sort-plan">
          <hlm-switch inputId="wo-sort-plan" [checked]="sortByPlan()" (checkedChange)="sortByPlan.set($event)" />
          Ordina per inizio previsto
        </label>
        @if (machineOptions().length) {
          <hlm-select [value]="machineFilter()" [itemToString]="machineFilterLabel" (valueChange)="machineFilter.set($event ?? 'all')">
            <hlm-select-trigger size="sm" class="w-48" aria-label="Filtra per macchina">
              <hlm-select-value />
            </hlm-select-trigger>
            <hlm-select-content *hlmSelectPortal>
              <hlm-select-item value="all">Tutte le macchine</hlm-select-item>
              @for (m of machineOptions(); track m.id) {
                <hlm-select-item [value]="m.id">{{ m.code }} · {{ m.name }}</hlm-select-item>
              }
            </hlm-select-content>
          </hlm-select>
        }
      </div>

      @if (store.loading() && !store.workOrders().length) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento lavori">
          @for (i of [1, 2, 3, 4, 5]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (store.loadError()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare i lavori</div>
            <div hlmEmptyDescription>{{ store.loadError() }}</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="store.load()">Riprova</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (!store.workOrders().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideListChecks" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessun ordine di lavoro</div>
            <div hlmEmptyDescription>I lavori vengono generati al rilascio di un ordine di produzione.</div>
          </hlm-empty-header>
        </hlm-empty>
      } @else if (!filtered().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideSearchX" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessun risultato</div>
            <div hlmEmptyDescription>Nessun lavoro corrisponde ai filtri.</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" variant="outline" (click)="resetFilters()">Azzera filtri</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else {
        <div class="max-h-[700px] w-full overflow-auto rounded-md border">
          <div hlmTableContainer>
            <table hlmTable>
              <thead hlmTHead class="bg-muted sticky top-0 z-10">
                @for (hg of table.getHeaderGroups(); track hg.id) {
                  <tr hlmTr>
                    @for (h of hg.headers; track h.id) {
                      <th hlmTh [class.text-right]="numeric(h.column.id)">
                        @if (h.column.id === 'actions') {
                          <span class="sr-only">Azioni</span>
                        } @else {
                          {{ h.column.columnDef.header }}
                        }
                      </th>
                    }
                  </tr>
                }
              </thead>
              <tbody hlmTBody>
                @for (row of table.getRowModel().rows; track row.id) {
                  @let w = row.original;
                  <tr hlmTr>
                    @for (cell of row.getVisibleCells(); track cell.id) {
                      <td hlmTd [class]="cellClass(cell.column.id)">
                        @switch (cell.column.id) {
                          @case ('code') {
                            <span class="inline-flex items-center gap-1 font-medium">
                              {{ w.code }}
                              @if (w.isPlanLocked) {
                                <ng-icon name="lucideLock" size="12" class="text-muted-foreground" aria-hidden="true" /><span class="sr-only">(fissato)</span>
                              }
                            </span>
                          }
                          @case ('phase') {
                            {{ w.processSegmentName }}
                          }
                          @case ('equipment') {
                            @if (w.assignedEquipment; as eq) {
                              <span class="flex flex-wrap items-center gap-1">
                                <span class="font-medium">{{ eq.code }}</span>
                                @if (!eq.isEnabled) {
                                  <span hlmBadge variant="outline" class="text-destructive px-1.5">non più abilitata</span>
                                }
                                @if (inUseBy(w); as uses) {
                                  <span hlmBadge variant="secondary" class="px-1.5" [attr.title]="usageText(uses)">
                                    in uso<span class="sr-only"> da {{ usageCodes(uses) }}</span>
                                  </span>
                                }
                              </span>
                            } @else if (hasMachines(w) && w.status !== 'Ended' && w.status !== 'Aborted') {
                              <span class="text-muted-foreground">Da assegnare</span>
                            }
                          }
                          @case ('material') {
                            <span class="font-medium">{{ w.materialCode }}</span>
                            <span class="text-muted-foreground"> {{ w.materialName }}</span>
                          }
                          @case ('planned') {
                            {{ qty(w.plannedQuantity) }} {{ unit(w.unitOfMeasure) }}
                          }
                          @case ('status') {
                            @let st = status(w.status);
                            <span hlmBadge variant="outline" class="px-1.5" [class]="st.tone">
                              <ng-icon [name]="st.icon" /> {{ st.label }}
                            </span>
                            @if (w.status === 'Waiting' && w.predecessors.length) {
                              <div class="text-muted-foreground mt-1 text-xs">Attende: {{ waitingFor(w) }}</div>
                            }
                          }
                          @case ('plannedStart') {
                            @if (w.plannedStartAt) {
                              {{ fmt(w.plannedStartAt) }}
                            } @else {
                              <span class="text-muted-foreground" aria-label="Non pianificato">—</span>
                            }
                          }
                          @case ('plannedEnd') {
                            <span class="flex flex-wrap items-center gap-x-2 gap-y-1">
                              @if (w.plannedEndAt) {
                                {{ fmt(w.plannedEndAt) }}
                              } @else {
                                <span class="text-muted-foreground" aria-label="Non pianificato">—</span>
                              }
                              <app-plan-status-badge [status]="w.planStatus" [hide]="hiddenPlan(w)" />
                            </span>
                          }
                          @case ('good') {
                            {{ qty(w.goodQuantity) }}
                          }
                          @case ('scrap') {
                            {{ qty(w.scrapQuantity) }}
                          }
                          @case ('plannedTime') {
                            {{ minutes(w.plannedMinutes) }}
                          }
                          @case ('workedTime') {
                            {{ minutes(w.workedMinutes) }}
                          }
                          @case ('actions') {
                            <app-work-order-row-actions
                              [work]="w"
                              (startWork)="run(w, 'start')"
                              (resumeWork)="run(w, 'resume')"
                              (pauseWork)="closing.set({ work: $event, mode: 'Pause' })"
                              (stopWork)="closing.set({ work: $event, mode: 'Stop' })"
                              (abortWork)="aborting.set($event)"
                              (sessions)="viewing.set($event)"
                              [hasMachines]="hasMachines(w)"
                              (lockPlan)="planAction(w, 'lock')"
                              (unlockPlan)="planAction(w, 'unlock')"
                              (movePlan)="moving.set($event)"
                              (assignMachine)="machineDialog.set({ work: $event, mode: 'Assign' })"
                            />
                          }
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
        <div class="mt-4 flex flex-col justify-between sm:flex-row sm:items-center">
          <span class="text-muted-foreground text-sm tabular-nums" aria-live="polite">
            {{ filtered().length }} {{ filtered().length === 1 ? 'lavoro' : 'lavori' }}
          </span>
          <div table-paginator [table]="table"></div>
        </div>
      }
    </div>

    @if (closing(); as c) {
      <app-work-order-close-dialog [work]="c.work" [mode]="c.mode" (done)="afterCommand(); closing.set(null)" (closed)="closing.set(null)" />
    }
    @if (viewing(); as w) {
      <app-work-order-sessions-dialog [work]="w" (changed)="afterCommand()" (closed)="viewing.set(null)" />
    }
    @if (machineDialog(); as d) {
      <app-work-order-machine-dialog [work]="d.work" [mode]="d.mode" (done)="afterCommand(); machineDialog.set(null)" (closed)="machineDialog.set(null)" />
    }
    @if (moving(); as w) {
      <app-plan-move-dialog [work]="moveTarget(w)" (done)="afterCommand(); moving.set(null)" (closed)="moving.set(null)" />
    }
    @if (aborting(); as w) {
      <app-work-order-abort-dialog [work]="w" (done)="afterCommand(); aborting.set(null)" (closed)="aborting.set(null)" />
    }
  `,
})
export class WorkOrdersComponent implements OnInit {
  protected readonly store = inject(WorkOrdersStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly statusOptions = (Object.keys(WORK_STATUS) as WorkOrderStatus[]).map((value) => ({
    value,
    label: WORK_STATUS[value].label,
  }));
  protected readonly status = workStatus;

  /** Etichetta mostrata nel trigger (il brain select altrimenti mostra il valore grezzo). */
  protected readonly statusLabel = (value: string): string =>
    value === ALL ? 'Tutti' : value === OPERATIONAL ? 'Operativi' : (this.statusOptions.find((o) => o.value === value)?.label ?? value);
  protected readonly orderLabel = (value: string): string =>
    value === ALL ? 'Tutti gli ordini' : (this.orderOptions().find((o) => o.id === value)?.code ?? value);
  protected readonly qty = formatQty;
  protected readonly unit = unitLabel;
  protected readonly minutes = formatMinutes;

  protected readonly machineFilterLabel = (value: string): string =>
    value === ALL ? 'Tutte le macchine' : (this.machineOptions().find((m) => m.id === value)?.label ?? value);
  protected readonly usageText = usageText;

  protected readonly planOptions = [
    { value: 'all', label: 'Piano: tutti' },
    { value: 'Unplanned', label: 'Da pianificare' },
    { value: 'Late', label: 'In ritardo' },
    { value: 'Stale', label: 'Obsoleto' },
  ];
  protected readonly planLabel = (value: string): string => this.planOptions.find((o) => o.value === value)?.label ?? value;
  protected readonly planFilter = signal<string>(ALL);
  protected readonly sortByPlan = signal(false);
  protected readonly onlyLocked = signal(false);
  private readonly calendars = inject(CalendarsStore);
  protected readonly fmt = (iso: string): string => formatDateTime(iso, this.calendars.timeZone());

  protected readonly search = signal('');
  protected readonly statusFilter = signal<string>(OPERATIONAL);
  protected readonly orderFilter = signal<string>(ALL);
  protected readonly closing = signal<{ work: WorkOrderModel; mode: CloseMode } | null>(null);
  protected readonly machineFilter = signal<string>(ALL);
  protected readonly moving = signal<WorkOrderModel | null>(null);
  protected readonly machineDialog = signal<{ work: WorkOrderModel; mode: MachineMode } | null>(null);
  protected readonly aborting = signal<WorkOrderModel | null>(null);
  protected readonly viewing = signal<WorkOrderModel | null>(null);

  private readonly equipmentStore = inject(EquipmentStore);
  private readonly segmentsStore = inject(ProcessSegmentsStore);

  /** Macchine attive per il filtro. */
  protected readonly machineOptions = computed(() =>
    this.equipmentStore
      .items()
      .filter((e) => e.isActive)
      .map((e) => ({ id: String(e.id), code: e.code, name: e.name, label: `${e.code} · ${e.name}` })),
  );

  /** Fasi con almeno una macchina attiva collegata (per sapere se la macchina va chiesta). */
  private readonly segmentsWithMachines = computed(
    () => new Set(this.segmentsStore.segments().filter((s) => s.equipment.some((e) => e.isActive)).map((s) => s.id)),
  );

  /** Macchina -> lavori Running che la usano, dall'elenco già caricato. */
  private readonly usage = computed(() => machineUsage(this.store.workOrders()));

  private readonly queryParams = toSignal(this.route.queryParamMap, { initialValue: this.route.snapshot.queryParamMap });

  /** Ordini di produzione presenti nell'elenco (non filtrato), per il filtro. */
  protected readonly orderOptions = computed(() => {
    const seen = new Map<number, string>();
    for (const w of this.store.workOrders()) seen.set(w.productionOrderId, w.productionOrderCode);
    return [...seen].map(([id, code]) => ({ id: String(id), code })).sort((a, b) => a.code.localeCompare(b.code));
  });

  protected readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const s = this.statusFilter();
    const o = this.orderFilter();
    const m = this.machineFilter();
    const p = this.planFilter();
    const byPlan = this.sortByPlan();
    const lockedOnly = this.onlyLocked();
    return this.store
      .workOrders()
      .filter(
        (w) =>
          (s === ALL || (s === OPERATIONAL ? OPERATIONAL_STATUSES.includes(w.status) : w.status === s)) &&
          (o === ALL || String(w.productionOrderId) === o) &&
          (p === ALL || (w.planStatus === p && (p !== 'Unplanned' || isOpen(w)))) &&
          (!lockedOnly || w.isPlanLocked) &&
          (m === ALL || String(w.assignedEquipment?.id) === m) &&
          (!q ||
            w.code.toLowerCase().includes(q) ||
            w.processSegmentName.toLowerCase().includes(q) ||
            w.materialCode.toLowerCase().includes(q) ||
            w.materialName.toLowerCase().includes(q)),
      )
      .sort((a, b) =>
        byPlan
          ? (a.plannedStartAt ? Date.parse(a.plannedStartAt) : Infinity) - (b.plannedStartAt ? Date.parse(b.plannedStartAt) : Infinity) ||
            a.productionOrderCode.localeCompare(b.productionOrderCode) ||
            a.operationNumber - b.operationNumber
          : a.productionOrderCode.localeCompare(b.productionOrderCode) || a.operationNumber - b.operationNumber,
      );
  });

  private readonly pagination = signal<PaginationState>({ pageIndex: 0, pageSize: 10 });

  protected readonly table = injectTable(() => ({
    features,
    columns,
    data: this.filtered(),
    autoResetPageIndex: false,
    state: { pagination: this.pagination() },
    onPaginationChange: (updater) => {
      this.pagination.set(typeof updater === 'function' ? updater(this.pagination()) : updater);
    },
    getRowId: (r: WorkOrderModel) => String(r.id),
  }));

  constructor() {
    effect(() => {
      this.search();
      this.statusFilter();
      this.orderFilter();
      this.machineFilter();
      this.planFilter();
      this.onlyLocked();
      untracked(() => this.table.resetPageIndex());
    });
    // ?order=<id> dalla pagina dell'ordine: mostra tutti i lavori di quell'ordine.
    effect(() => {
      const id = this.queryParams().get('order');
      untracked(() => {
        if (id) {
          this.orderFilter.set(id);
          this.statusFilter.set(ALL);
        }
      });
    });
    // ?locked=true: filtro «Solo fissati» persistito nell'URL.
    effect(() => {
      const v = this.queryParams().get('locked') === 'true';
      untracked(() => this.onlyLocked.set(v));
    });
  }

  ngOnInit(): void {
    void this.store.load();
    void this.calendars.loadTimeZone();
    void this.equipmentStore.load();
    void this.segmentsStore.load();
  }

  /** `Unplanned` si segnala solo per i lavori aperti. */
  protected hiddenPlan(w: WorkOrderModel): readonly PlanStatus[] {
    return isOpen(w) ? ['Planned'] : ['Planned', 'Unplanned'];
  }

  protected hasMachines(w: WorkOrderModel): boolean {
    return this.segmentsWithMachines().has(w.processSegmentId);
  }

  /** Altri lavori Running che usano la macchina assegnata (null se nessuno). */
  protected inUseBy(w: WorkOrderModel) {
    const id = w.assignedEquipment?.id;
    const uses = id == null ? [] : (this.usage().get(id) ?? []).filter((u) => u.workOrderId !== w.id);
    return uses.length ? uses : null;
  }

  protected usageCodes(uses: readonly { code: string }[]): string {
    return uses.map((u) => u.code).join(', ');
  }

  protected numeric(id: string): boolean {
    return NUMERIC_COLUMNS.has(id);
  }

  protected cellClass(id: string): string {
    return NUMERIC_COLUMNS.has(id) ? 'text-right tabular-nums' : '';
  }

  protected waitingFor(w: WorkOrderModel): string {
    return w.predecessors.map((p) => `${p.code} (${workStatus(p.status).label})`).join(', ');
  }

  protected setOnlyLocked(value: boolean): void {
    this.onlyLocked.set(value);
    void this.router.navigate([], { queryParams: { locked: value ? 'true' : null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  protected resetFilters(): void {
    this.search.set('');
    this.statusFilter.set(OPERATIONAL);
    this.machineFilter.set(ALL);
    this.planFilter.set(ALL);
    this.setOnlyLocked(false);
    this.clearOrderFilter();
  }

  protected clearOrderFilter(): void {
    this.orderFilter.set(ALL);
    if (this.queryParams().has('order')) {
      void this.router.navigate([], { queryParams: { order: null }, queryParamsHandling: 'merge', replaceUrl: true });
    }
  }

  /**
   * Avvia / Riprendi. Se la fase ha macchine si apre il dialogo di scelta (precompilato: un clic se la macchina è
   * già assegnata e libera); senza macchine la chiamata è diretta, come prima.
   */
  protected async run(w: WorkOrderModel, command: 'start' | 'resume'): Promise<void> {
    if (!this.segmentsStore.segments().length) await this.segmentsStore.load();
    if (this.hasMachines(w)) {
      this.machineDialog.set({ work: w, mode: command === 'start' ? 'Start' : 'Resume' });
      return;
    }
    try {
      if (command === 'start') await this.store.start(w.id);
      else await this.store.resume(w.id);
      toast.success(command === 'start' ? `Lavoro ${w.code} avviato` : `Lavoro ${w.code} ripreso`);
      this.afterCommand();
    } catch (e) {
      toast.error(toFailure(e).message);
      void this.store.load();
    }
  }

  protected moveTarget(w: WorkOrderModel): MoveTarget {
    return { id: w.id, code: w.code, processSegmentId: w.processSegmentId, plannedStartAt: w.plannedStartAt, equipmentId: (w.assignedEquipment ?? w.currentEquipment)?.id ?? null };
  }

  protected async planAction(w: WorkOrderModel, kind: 'lock' | 'unlock'): Promise<void> {
    if (await runPlanLockAction(this.store, kind, w.id)) this.afterCommand();
  }

  /** La riga è già aggiornata dalla risposta; si ricarica per riallineare successori e stato dell'ordine. */
  protected afterCommand(): void {
    void this.store.load();
  }
}
