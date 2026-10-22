import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal, untracked } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleCheck, lucideCircleOff, lucidePlus, lucideSearch, lucideSearchX, lucideTriangleAlert, lucideWrench } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import {
  columnVisibilityFeature,
  createColumnHelper,
  createPaginatedRowModel,
  FlexRender,
  injectTable,
  type PaginationState,
  rowPaginationFeature,
  tableFeatures,
} from '@tanstack/angular-table';
import type { ToolingModel } from '@micromes/api-client/models';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { TablePaginatorComponent } from '../../shared/components/table-paginator.component';
import { toFailure } from '../materials/materials.service';
import { ACTIVE_FILTER_LABELS, type ActiveFilter, compatibleLabel, filterItems } from './tooling-filters';
import { ToolingDetailComponent } from './tooling-detail.component';
import { ToolingFormDialogComponent } from './tooling-form-dialog.component';
import { ToolingRowActionsComponent } from './tooling-row-actions.component';
import { ToolingStore } from './tooling.store';

const features = tableFeatures({
  columnVisibilityFeature,
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
});

const columnHelper = createColumnHelper<typeof features, ToolingModel>();

const columns = columnHelper.columns([
  columnHelper.accessor('code', { id: 'code', header: 'Codice' }),
  columnHelper.accessor('name', { id: 'name', header: 'Nome' }),
  columnHelper.accessor('quantity', { id: 'quantity', header: 'Quantità' }),
  columnHelper.accessor('setupMinutes', { id: 'setupMinutes', header: 'Montaggio (min)' }),
  columnHelper.display({ id: 'equipment', header: 'Macchine compatibili' }),
  columnHelper.accessor('isActive', { id: 'status', header: 'Stato' }),
  columnHelper.display({ id: 'actions', header: 'Azioni' }),
]);

@Component({
  selector: 'app-tooling',
  imports: [
    HlmTableImports,
    HlmEmptyImports,
    HlmAlertDialogImports,
    HlmSelectImports,
    HlmBadge,
    HlmButton,
    HlmInput,
    HlmSkeleton,
    NgIcon,
    FlexRender,
    TablePaginatorComponent,
    ToolingRowActionsComponent,
    ToolingFormDialogComponent,
    ToolingDetailComponent,
  ],
  providers: [provideIcons({ lucideCircleCheck, lucideCircleOff, lucidePlus, lucideSearch, lucideSearchX, lucideTriangleAlert, lucideWrench })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      @if (detail(); as t) {
        <app-tooling-detail [tooling]="t" (back)="detailId.set(null)" (edit)="openEdit($event)" />
      } @else {
        <div class="flex flex-col gap-1">
          <h1 class="text-xl font-semibold">Attrezzature</h1>
          <p class="text-muted-foreground text-sm">Stampi, seghe e accessori: quante unità esistono, quanto costa montarle e su quali macchine si usano.</p>
        </div>

        <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
          <div class="relative">
            <ng-icon name="lucideSearch" class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2" aria-hidden="true" />
            <input
              hlmInput
              class="h-8 w-80 pl-8"
              type="search"
              placeholder="Cerca per codice, nome o descrizione"
              aria-label="Cerca attrezzature"
              [value]="search()"
              (input)="search.set($any($event.target).value)"
            />
          </div>
          <hlm-select [value]="activeFilter()" [itemToString]="filterLabel" (valueChange)="activeFilter.set($any($event) ?? 'active')">
            <hlm-select-trigger class="w-44" aria-label="Filtra per stato"><hlm-select-value /></hlm-select-trigger>
            <hlm-select-content *hlmSelectPortal>
              @for (f of filterOptions; track f) {
                <hlm-select-item [value]="f">{{ filterLabel(f) }}</hlm-select-item>
              }
            </hlm-select-content>
          </hlm-select>
          <button hlmBtn size="sm" class="ml-auto" type="button" (click)="openCreate()"><ng-icon name="lucidePlus" /> Nuova attrezzatura</button>
        </div>

        @if (store.loading() && !store.items().length) {
          <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento attrezzature">
            @for (i of [1, 2, 3, 4, 5]; track i) {
              <hlm-skeleton class="h-10 w-full" />
            }
          </div>
        } @else if (store.loadError()) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
              <div hlmEmptyTitle>Impossibile caricare le attrezzature</div>
              <div hlmEmptyDescription>{{ store.loadError() }}</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" (click)="store.load()">Riprova</button></hlm-empty-content>
          </hlm-empty>
        } @else if (!store.items().length) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideWrench" /></hlm-empty-media>
              <div hlmEmptyTitle>Nessuna attrezzatura</div>
              <div hlmEmptyDescription>Aggiungi stampi, seghe o accessori da richiedere nei passi del ciclo.</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" (click)="openCreate()"><ng-icon name="lucidePlus" /> Nuova attrezzatura</button></hlm-empty-content>
          </hlm-empty>
        } @else if (!filtered().length) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideSearchX" /></hlm-empty-media>
              <div hlmEmptyTitle>Nessun risultato</div>
              <div hlmEmptyDescription>Nessuna attrezzatura corrisponde ai filtri.</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" variant="outline" (click)="resetFilters()">Azzera filtri</button></hlm-empty-content>
          </hlm-empty>
        } @else {
          <div class="max-h-[700px] w-full overflow-auto rounded-md border">
            <div hlmTableContainer>
              <table hlmTable>
                <caption class="sr-only">Elenco delle attrezzature</caption>
                <thead hlmTHead class="bg-muted sticky top-0 z-10">
                  @for (hg of table.getHeaderGroups(); track hg.id) {
                    <tr hlmTr>
                      @for (h of hg.headers; track h.id) {
                        <th hlmTh>
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
                    <tr hlmTr [class.text-muted-foreground]="!row.original.isActive">
                      @for (cell of row.getVisibleCells(); track cell.id) {
                        <td hlmTd [class]="cell.column.id === 'code' ? 'font-medium' : ['quantity', 'setupMinutes'].includes(cell.column.id) ? 'tabular-nums' : ''">
                          @switch (cell.column.id) {
                            @case ('equipment') {
                              {{ compat(row.original.equipmentCount) }}
                            }
                            @case ('status') {
                              <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
                                @if (row.original.isActive) { <ng-icon name="lucideCircleCheck" /> Attiva } @else { <ng-icon name="lucideCircleOff" /> Disattivata }
                              </span>
                            }
                            @case ('actions') {
                              <app-tooling-row-actions
                                [tooling]="row.original"
                                (open)="detailId.set($event.id)"
                                (edit)="openEdit($event)"
                                (deactivate)="askDeactivate($event)"
                                (reactivate)="reactivate($event)"
                              />
                            }
                            @default {
                              <ng-container *flexRender="cell.column.columnDef.cell; props: cell.getContext(); let v">{{ v ?? '—' }}</ng-container>
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
              {{ filtered().length }} {{ filtered().length === 1 ? 'attrezzatura' : 'attrezzature' }}
            </span>
            <div table-paginator [table]="table"></div>
          </div>
        }
      }
    </div>

    @if (formOpen()) {
      <app-tooling-form-dialog [tooling]="editing()" (saved)="onSaved()" (closed)="formOpen.set(false)" />
    }

    @if (toDeactivate(); as t) {
      <hlm-alert-dialog state="open" (closed)="toDeactivate.set(null)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Disattivare l'attrezzatura?</h2>
              <p hlmAlertDialogDescription>
                {{ t.code }} — {{ t.name }} non sarà più selezionabile nei passi del ciclo. I collegamenti esistenti restano e potrai riattivarla in qualsiasi momento.
              </p>
            </hlm-alert-dialog-header>
            @if (actionError()) {
              <p class="text-destructive text-sm" role="alert">{{ actionError() }}</p>
            }
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Annulla</button>
              <button hlmBtn type="button" variant="destructive" [disabled]="deactivating()" (click)="confirmDeactivate(t)">Disattiva</button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }
  `,
})
export class ToolingComponent implements OnInit {
  protected readonly store = inject(ToolingStore);

  protected readonly search = signal('');
  protected readonly activeFilter = signal<ActiveFilter>('active');
  protected readonly filterOptions: ActiveFilter[] = ['active', 'inactive', 'all'];
  protected readonly filterLabel = (v: ActiveFilter | null): string => ACTIVE_FILTER_LABELS[v ?? 'active'];
  protected readonly compat = compatibleLabel;
  protected readonly detailId = signal<number | null>(null);
  protected readonly detail = computed(() => this.store.items().find((t) => t.id === this.detailId()) ?? null);
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<ToolingModel | null>(null);
  protected readonly toDeactivate = signal<ToolingModel | null>(null);
  protected readonly deactivating = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly filtered = computed(() => filterItems(this.store.items(), this.search(), this.activeFilter()));

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
    getRowId: (r: ToolingModel) => String(r.id),
  }));

  constructor() {
    effect(() => {
      this.search();
      this.activeFilter();
      untracked(() => this.table.resetPageIndex());
    });
  }

  ngOnInit(): void {
    void this.store.load();
  }

  protected resetFilters(): void {
    this.search.set('');
    this.activeFilter.set('all');
  }

  protected openCreate(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }

  protected openEdit(t: ToolingModel): void {
    this.editing.set(t);
    this.formOpen.set(true);
  }

  protected askDeactivate(t: ToolingModel): void {
    this.toDeactivate.set(t);
    this.actionError.set(null);
  }

  protected onSaved(): void {
    this.formOpen.set(false);
    toast.success('Attrezzatura salvata');
    void this.store.load();
  }

  protected async confirmDeactivate(t: ToolingModel): Promise<void> {
    this.deactivating.set(true);
    this.actionError.set(null);
    try {
      await this.store.deactivate(t.id);
      this.toDeactivate.set(null);
      toast.success('Attrezzatura disattivata');
      await this.store.load();
    } catch (e) {
      this.actionError.set(toFailure(e).message);
    } finally {
      this.deactivating.set(false);
    }
  }

  protected async reactivate(t: ToolingModel): Promise<void> {
    try {
      await this.store.reactivate(t.id);
      toast.success('Attrezzatura riattivata');
      await this.store.load();
    } catch (e) {
      toast.error(toFailure(e).message);
    }
  }
}
