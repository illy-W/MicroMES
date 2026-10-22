import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal, untracked } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleCheck, lucideCircleOff, lucidePlus, lucideSearch, lucideSearchX, lucideTriangleAlert, lucideSlidersHorizontal } from '@ng-icons/lucide';
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
import type { SetupAttributeModel } from '@micromes/api-client/models';
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
import { ACTIVE_FILTER_LABELS, type ActiveFilter, filterItems } from '../tooling/tooling-filters';
import { SetupAttributeFormDialogComponent } from './setup-attribute-form-dialog.component';
import { SetupAttributeRowActionsComponent } from './setup-attribute-row-actions.component';
import { SetupAttributesStore } from './setup-attributes.store';

const features = tableFeatures({
  columnVisibilityFeature,
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
});

const columnHelper = createColumnHelper<typeof features, SetupAttributeModel>();

const columns = columnHelper.columns([
  columnHelper.accessor('code', { id: 'code', header: 'Codice' }),
  columnHelper.accessor('name', { id: 'name', header: 'Nome' }),
  columnHelper.accessor('changeMinutes', { id: 'changeMinutes', header: 'Minuti di cambio' }),
  columnHelper.accessor('isActive', { id: 'status', header: 'Stato' }),
  columnHelper.display({ id: 'actions', header: 'Azioni' }),
]);

@Component({
  selector: 'app-setup-attributes',
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
    SetupAttributeRowActionsComponent,
    SetupAttributeFormDialogComponent,
  ],
  providers: [provideIcons({ lucideCircleCheck, lucideCircleOff, lucidePlus, lucideSearch, lucideSearchX, lucideTriangleAlert, lucideSlidersHorizontal })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="contents">
        <div class="flex flex-col gap-1">
          <h1 class="text-xl font-semibold">Attributi di cambio</h1>
          <p class="text-muted-foreground text-sm">Cosa fa cambiare l'attrezzaggio tra due lavori (colore, materiale…) e quanti minuti costa il cambio.</p>
        </div>

        <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
          <div class="relative">
            <ng-icon name="lucideSearch" class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2" aria-hidden="true" />
            <input
              hlmInput
              class="h-8 w-80 pl-8"
              type="search"
              placeholder="Cerca per codice o nome"
              aria-label="Cerca attributi di cambio"
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
          <button hlmBtn size="sm" class="ml-auto" type="button" (click)="openCreate()"><ng-icon name="lucidePlus" /> Nuovo attributo</button>
        </div>

        @if (store.loading() && !store.items().length) {
          <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento attributi di cambio">
            @for (i of [1, 2, 3, 4, 5]; track i) {
              <hlm-skeleton class="h-10 w-full" />
            }
          </div>
        } @else if (store.loadError()) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
              <div hlmEmptyTitle>Impossibile caricare gli attributi di cambio</div>
              <div hlmEmptyDescription>{{ store.loadError() }}</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" (click)="store.load()">Riprova</button></hlm-empty-content>
          </hlm-empty>
        } @else if (!store.items().length) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideSlidersHorizontal" /></hlm-empty-media>
              <div hlmEmptyTitle>Nessun attributo di cambio</div>
              <div hlmEmptyDescription>Aggiungi ciò che fa cambiare l'attrezzaggio, per esempio colore o materiale.</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" (click)="openCreate()"><ng-icon name="lucidePlus" /> Nuovo attributo</button></hlm-empty-content>
          </hlm-empty>
        } @else if (!filtered().length) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideSearchX" /></hlm-empty-media>
              <div hlmEmptyTitle>Nessun risultato</div>
              <div hlmEmptyDescription>Nessun attributo corrisponde ai filtri.</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" variant="outline" (click)="resetFilters()">Azzera filtri</button></hlm-empty-content>
          </hlm-empty>
        } @else {
          <div class="max-h-[700px] w-full overflow-auto rounded-md border">
            <div hlmTableContainer>
              <table hlmTable>
                <caption class="sr-only">Elenco degli attributi di cambio</caption>
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
                        <td hlmTd [class]="cell.column.id === 'code' ? 'font-medium' : cell.column.id === 'changeMinutes' ? 'tabular-nums' : ''">
                          @switch (cell.column.id) {
                            @case ('status') {
                              <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
                                @if (row.original.isActive) { <ng-icon name="lucideCircleCheck" /> Attivo } @else { <ng-icon name="lucideCircleOff" /> Disattivato }
                              </span>
                            }
                            @case ('actions') {
                              <app-setup-attribute-row-actions
                                [attribute]="row.original"
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
              {{ filtered().length }} {{ filtered().length === 1 ? 'attributo' : 'attributi' }}
            </span>
            <div table-paginator [table]="table"></div>
          </div>
        }
      </div>
    </div>

    @if (formOpen()) {
      <app-setup-attribute-form-dialog [attribute]="editing()" (saved)="onSaved()" (closed)="formOpen.set(false)" />
    }

    @if (toDeactivate(); as t) {
      <hlm-alert-dialog state="open" (closed)="toDeactivate.set(null)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Disattivare l'attributo?</h2>
              <p hlmAlertDialogDescription>
                {{ t.code }} — {{ t.name }} non sarà più selezionabile nei passi del ciclo. I valori già impostati restano e potrai riattivarla in qualsiasi momento.
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
export class SetupAttributesComponent implements OnInit {
  protected readonly store = inject(SetupAttributesStore);

  protected readonly search = signal('');
  protected readonly activeFilter = signal<ActiveFilter>('active');
  protected readonly filterOptions: ActiveFilter[] = ['active', 'inactive', 'all'];
  protected readonly filterLabel = (v: ActiveFilter | null): string => ACTIVE_FILTER_LABELS[v ?? 'active'];
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<SetupAttributeModel | null>(null);
  protected readonly toDeactivate = signal<SetupAttributeModel | null>(null);
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
    getRowId: (r: SetupAttributeModel) => String(r.id),
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

  protected openEdit(t: SetupAttributeModel): void {
    this.editing.set(t);
    this.formOpen.set(true);
  }

  protected askDeactivate(t: SetupAttributeModel): void {
    this.toDeactivate.set(t);
    this.actionError.set(null);
  }

  protected onSaved(): void {
    this.formOpen.set(false);
    toast.success('Attributo salvato');
    void this.store.load();
  }

  protected async confirmDeactivate(t: SetupAttributeModel): Promise<void> {
    this.deactivating.set(true);
    this.actionError.set(null);
    try {
      await this.store.deactivate(t.id);
      this.toDeactivate.set(null);
      toast.success('Attributo disattivato');
      await this.store.load();
    } catch (e) {
      this.actionError.set(toFailure(e).message);
    } finally {
      this.deactivating.set(false);
    }
  }

  protected async reactivate(t: SetupAttributeModel): Promise<void> {
    try {
      await this.store.reactivate(t.id);
      toast.success('Attributo riattivato');
      await this.store.load();
    } catch (e) {
      toast.error(toFailure(e).message);
    }
  }
}
