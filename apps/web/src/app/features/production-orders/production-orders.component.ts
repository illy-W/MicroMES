import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarClock,
  lucideCircleCheck,
  lucideLock,
  lucideCircleDot,
  lucideCircleOff,
  lucideClipboardList,
  lucideOctagonX,
  lucidePlay,
  lucidePlus,
  lucideSearch,
  lucideSearchX,
  lucideSend,
  lucideTriangleAlert,
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
import type { ProductionOrderModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmProgressImports } from '@spartan-ng/helm/progress';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { TablePaginatorComponent } from '../../shared/components/table-paginator.component';
import { unitLabel } from '../materials/material-labels';
import { ProductionOrderConfirmDialogComponent } from './production-order-confirm-dialog.component';
import { ProductionOrderFormDialogComponent } from './production-order-form-dialog.component';
import { formatDate, formatQty, ORDER_STATUS_OPTIONS, orderStatus } from './production-order-labels';
import { ProductionOrderRowActionsComponent } from './production-order-row-actions.component';
import { ProductionOrdersStore } from './production-orders.store';

const features = tableFeatures({
  columnVisibilityFeature,
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
});

const columnHelper = createColumnHelper<typeof features, ProductionOrderModel>();

const columns = columnHelper.columns([
  columnHelper.accessor('code', { id: 'code', header: 'Codice' }),
  columnHelper.accessor('materialCode', { id: 'material', header: 'Materiale' }),
  columnHelper.accessor('quantity', { id: 'quantity', header: 'Quantità' }),
  columnHelper.accessor('status', { id: 'status', header: 'Stato' }),
  columnHelper.accessor('priority', { id: 'priority', header: 'Priorità' }),
  columnHelper.accessor('requestedStart', { id: 'start', header: 'Inizio richiesto' }),
  columnHelper.accessor('requestedEnd', { id: 'end', header: 'Fine richiesta' }),
  columnHelper.accessor('progressPercent', { id: 'progress', header: 'Avanzamento' }),
  columnHelper.display({ id: 'actions', header: 'Azioni' }),
]);

const ALL = 'all';

@Component({
  selector: 'app-production-orders',
  imports: [
    RouterLink,
    HlmTableImports,
    HlmEmptyImports,
    HlmSelectImports,
    HlmProgressImports,
    HlmBadge,
    HlmButton,
    HlmInput,
    HlmSkeleton,
    NgIcon,
    TablePaginatorComponent,
    ProductionOrderRowActionsComponent,
    ProductionOrderFormDialogComponent,
    ProductionOrderConfirmDialogComponent,
  ],
  providers: [
    provideIcons({
      lucideCalendarClock,
      lucideCircleCheck,
      lucideLock,
      lucideCircleDot,
      lucideCircleOff,
      lucideClipboardList,
      lucideOctagonX,
      lucidePlay,
      lucidePlus,
      lucideSearch,
      lucideSearchX,
      lucideSend,
      lucideTriangleAlert,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold">Ordini di produzione</h1>
        <p class="text-muted-foreground text-sm">Ordini da produrre, con stato e avanzamento.</p>
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
            placeholder="Cerca per codice o materiale"
            aria-label="Cerca ordini"
            [value]="search()"
            (input)="search.set($any($event.target).value)"
          />
        </div>
        <hlm-select [value]="statusFilter()" [itemToString]="statusLabel" (valueChange)="statusFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-44" aria-label="Filtra per stato">
            <hlm-select-value />
          </hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="all">Tutti</hlm-select-item>
            @for (o of statusOptions; track o.value) {
              <hlm-select-item [value]="o.value">{{ o.label }}</hlm-select-item>
            }
          </hlm-select-content>
        </hlm-select>
        <button hlmBtn size="sm" class="ml-auto" type="button" (click)="openCreate()">
          <ng-icon name="lucidePlus" /> Nuovo ordine
        </button>
      </div>

      @if (store.loading() && !store.orders().length) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento ordini">
          @for (i of [1, 2, 3, 4, 5]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (store.loadError()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare gli ordini</div>
            <div hlmEmptyDescription>{{ store.loadError() }}</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="store.load()">Riprova</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (!store.orders().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideClipboardList" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessun ordine di produzione</div>
            <div hlmEmptyDescription>Crea un ordine per pianificare la produzione.</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="openCreate()"><ng-icon name="lucidePlus" /> Nuovo ordine</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (!filtered().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideSearchX" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessun risultato</div>
            <div hlmEmptyDescription>Nessun ordine corrisponde ai filtri.</div>
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
                      <th hlmTh [class.text-right]="h.column.id === 'quantity' || h.column.id === 'priority'">
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
                  @let o = row.original;
                  <tr hlmTr>
                    @for (cell of row.getVisibleCells(); track cell.id) {
                      <td hlmTd [class]="cellClass(cell.column.id)">
                        @switch (cell.column.id) {
                          @case ('code') {
                            <a class="font-medium hover:underline" [routerLink]="['/production-orders', o.id]">{{ o.code }}</a>
                          }
                          @case ('material') {
                            <span class="font-medium">{{ o.materialCode }}</span>
                            <span class="text-muted-foreground"> {{ o.materialName }}</span>
                          }
                          @case ('quantity') {
                            {{ qty(o.quantity) }} {{ unit(o.unitOfMeasure) }}
                          }
                          @case ('status') {
                            @let st = status(o.status);
                            <span hlmBadge variant="outline" class="px-1.5" [class]="st.tone">
                              <ng-icon [name]="st.icon" /> {{ st.label }}
                            </span>
                            @if (o.lockedWorkOrdersCount > 0) {
                              <span hlmBadge variant="outline" class="ml-1 gap-1 px-1.5">
                                <ng-icon name="lucideLock" size="12" aria-hidden="true" /> {{ o.lockedWorkOrdersCount }} fissati
                              </span>
                            }
                          }
                          @case ('priority') {
                            {{ o.priority }}
                          }
                          @case ('start') {
                            {{ date(o.requestedStart) }}
                          }
                          @case ('end') {
                            {{ date(o.requestedEnd) }}
                          }
                          @case ('progress') {
                            <div class="flex min-w-36 items-center gap-2">
                              <hlm-progress
                                class="flex-1"
                                [value]="o.progressPercent"
                                [attr.aria-label]="'Avanzamento ' + o.code"
                              >
                                <hlm-progress-indicator />
                              </hlm-progress>
                              <span class="text-muted-foreground w-10 text-right text-xs tabular-nums"
                                >{{ o.progressPercent }}%</span
                              >
                            </div>
                          }
                          @case ('actions') {
                            <app-production-order-row-actions
                              [order]="o"
                              (edit)="openEdit($event)"
                              (release)="askConfirm($event, 'Release')"
                              (cancelOrder)="askConfirm($event, 'Cancel')"
                              (abortOrder)="askConfirm($event, 'Abort')"
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
            {{ filtered().length }} {{ filtered().length === 1 ? 'ordine' : 'ordini' }}
          </span>
          <div table-paginator [table]="table"></div>
        </div>
      }
    </div>

    @if (formOpen()) {
      <app-production-order-form-dialog [order]="editing()" (saved)="onSaved($event)" (closed)="formOpen.set(false)" />
    }

    @if (confirm(); as c) {
      <app-production-order-confirm-dialog
        [order]="c.order"
        [action]="c.action"
        (done)="confirm.set(null); store.load()"
        (closed)="confirm.set(null)"
      />
    }
  `,
})
export class ProductionOrdersComponent implements OnInit {
  protected readonly store = inject(ProductionOrdersStore);
  private readonly router = inject(Router);

  protected readonly statusOptions = ORDER_STATUS_OPTIONS;
  protected readonly status = orderStatus;
  protected readonly statusLabel = (value: string): string =>
    value === ALL ? 'Tutti' : (this.statusOptions.find((o) => o.value === value)?.label ?? value);
  protected readonly qty = formatQty;
  protected readonly unit = unitLabel;
  protected readonly date = formatDate;

  protected readonly search = signal('');
  protected readonly statusFilter = signal<string>(ALL);
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<ProductionOrderModel | null>(null);
  protected readonly confirm = signal<{ order: ProductionOrderModel; action: string } | null>(null);

  /** Ordinamento predefinito: priorità decrescente, poi inizio richiesto (senza data in fondo). */
  protected readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const s = this.statusFilter();
    return this.store
      .orders()
      .filter(
        (o) =>
          (s === ALL || o.status === s) &&
          (!q || o.code.toLowerCase().includes(q) || o.materialCode.toLowerCase().includes(q) || o.materialName.toLowerCase().includes(q)),
      )
      .sort((a, b) => {
        if (b.priority !== a.priority) return b.priority - a.priority;
        const ta = a.requestedStart ? Date.parse(a.requestedStart) : Infinity;
        const tb = b.requestedStart ? Date.parse(b.requestedStart) : Infinity;
        return ta === tb ? a.code.localeCompare(b.code) : ta - tb;
      });
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
    getRowId: (r: ProductionOrderModel) => String(r.id),
  }));

  constructor() {
    effect(() => {
      this.search();
      this.statusFilter();
      untracked(() => this.table.resetPageIndex());
    });
  }

  ngOnInit(): void {
    void this.store.load();
  }

  protected cellClass(id: string): string {
    switch (id) {
      case 'quantity':
      case 'priority':
        return 'text-right tabular-nums';
      case 'start':
      case 'end':
        return 'text-muted-foreground tabular-nums';
      default:
        return '';
    }
  }

  protected resetFilters(): void {
    this.search.set('');
    this.statusFilter.set(ALL);
  }

  protected openCreate(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }

  protected openEdit(o: ProductionOrderModel): void {
    this.editing.set(o);
    this.formOpen.set(true);
  }

  protected askConfirm(order: ProductionOrderModel, action: string): void {
    this.confirm.set({ order, action });
  }

  protected onSaved(id: number): void {
    this.formOpen.set(false);
    if (this.editing()) void this.store.load();
    else void this.router.navigate(['/production-orders', id]);
  }
}
