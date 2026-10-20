import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  untracked,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleCheck, lucideCircleOff, lucidePackage, lucidePlus, lucideSearch, lucideSearchX, lucideTriangleAlert } from '@ng-icons/lucide';
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
import type { MaterialModel } from '@micromes/api-client/models';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { TablePaginatorComponent } from '../../shared/components/table-paginator.component';
import { MaterialRowActionsComponent } from './material-row-actions.component';
import { MaterialFormDialogComponent } from './material-form-dialog.component';
import { typeLabel, unitLabel } from './material-labels';
import { MaterialsStore, toFailure } from './materials.service';

const features = tableFeatures({
  columnVisibilityFeature,
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
});

const columnHelper = createColumnHelper<typeof features, MaterialModel>();

const columns = columnHelper.columns([
  columnHelper.accessor('code', { id: 'code', header: 'Codice' }),
  columnHelper.accessor('name', { id: 'name', header: 'Nome' }),
  columnHelper.accessor((r) => typeLabel(r.type), { id: 'type', header: 'Tipo' }),
  columnHelper.accessor((r) => unitLabel(r.unitOfMeasure), { id: 'unit', header: 'Unità' }),
  columnHelper.accessor((r) => r.vendor ?? '', { id: 'vendor', header: 'Fornitore' }),
  columnHelper.accessor((r) => r.vendorMaterialId ?? '', { id: 'vendorMaterialId', header: 'Cod. fornitore' }),
  columnHelper.accessor('isActive', { id: 'status', header: 'Stato' }),
  columnHelper.display({ id: 'actions', header: 'Azioni' }),
]);

@Component({
  selector: 'app-materials',
  imports: [
    HlmTableImports,
    HlmEmptyImports,
    HlmAlertDialogImports,
    HlmBadge,
    MaterialRowActionsComponent,
    TablePaginatorComponent,
    HlmButton,
    HlmInput,
    HlmLabel,
    HlmSkeleton,
    HlmSwitch,
    NgIcon,
    FlexRender,
    MaterialFormDialogComponent,
  ],
  providers: [provideIcons({ lucideCircleCheck, lucideCircleOff, lucidePackage, lucidePlus, lucideSearch, lucideSearchX, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="flex items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
          <h1 class="text-xl font-semibold">Materiali</h1>
          <p class="text-muted-foreground text-sm">Anagrafica dei materiali usati in produzione.</p>
        </div>
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
            placeholder="Cerca per codice, nome o fornitore"
            aria-label="Cerca materiali"
            [value]="search()"
            (input)="search.set($any($event.target).value)"
          />
        </div>
        <div class="flex items-center gap-2">
          <hlm-switch inputId="show-inactive" [checked]="showInactive()" (checkedChange)="showInactive.set($event)" />
          <label hlmLabel for="show-inactive">Mostra disattivati</label>
        </div>
        <button hlmBtn size="sm" class="ml-auto" type="button" (click)="openCreate()">
          <ng-icon name="lucidePlus" /> Nuovo materiale
        </button>
      </div>

      @if (store.loading() && !store.materials().length) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento materiali">
          @for (i of [1, 2, 3, 4, 5]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (store.loadError()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare i materiali</div>
            <div hlmEmptyDescription>{{ store.loadError() }}</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="store.load()">Riprova</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (!store.materials().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucidePackage" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessun materiale</div>
            <div hlmEmptyDescription>Aggiungi un materiale per usarlo nelle lavorazioni.</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="openCreate()"><ng-icon name="lucidePlus" /> Nuovo materiale</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (!filtered().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideSearchX" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessun risultato</div>
            <div hlmEmptyDescription>
              Nessun materiale corrisponde alla ricerca{{ showInactive() ? '' : ' tra quelli attivi' }}.
            </div>
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
                      <td hlmTd [class]="cellClass(cell.column.id)">
                        @switch (cell.column.id) {
                          @case ('type') {
                            <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">{{ cell.getValue() }}</span>
                          }
                          @case ('status') {
                            <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
                              @if (row.original.isActive) {
                                <ng-icon name="lucideCircleCheck" /> Attivo
                              } @else {
                                <ng-icon name="lucideCircleOff" /> Disattivato
                              }
                            </span>
                          }
                          @case ('actions') {
                            <app-material-row-actions
                              [material]="row.original"
                              (edit)="openEdit($event)"
                              (deactivate)="askDeactivate($event)"
                            />
                          }
                          @default {
                            <ng-container *flexRender="cell.column.columnDef.cell; props: cell.getContext(); let v">{{
                              v || '—'
                            }}</ng-container>
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
            {{ filtered().length }} {{ filtered().length === 1 ? 'materiale' : 'materiali' }}
          </span>
          <div table-paginator [table]="table"></div>
        </div>
      }
    </div>

    @if (formOpen()) {
      <app-material-form-dialog
        [material]="editing()"
        (saved)="onSaved()"
        (closed)="formOpen.set(false)"
      />
    }

    @if (toDeactivate(); as m) {
      <hlm-alert-dialog state="open" (closed)="toDeactivate.set(null)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Disattivare il materiale?</h2>
              <p hlmAlertDialogDescription>
                {{ m.code }} — {{ m.name }} non sarà più selezionabile. Potrai riattivarlo in qualsiasi momento dalla modifica.
              </p>
            </hlm-alert-dialog-header>
            @if (deactivateError()) {
              <p class="text-destructive text-sm" role="alert">{{ deactivateError() }}</p>
            }
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Annulla</button>
              <button hlmBtn type="button" variant="destructive" [disabled]="deactivating()" (click)="confirmDeactivate(m)">
                Disattiva
              </button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }
  `,
})
export class MaterialsComponent implements OnInit {
  protected readonly store = inject(MaterialsStore);

  protected readonly search = signal('');
  protected readonly showInactive = signal(false);
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<MaterialModel | null>(null);
  protected readonly toDeactivate = signal<MaterialModel | null>(null);
  protected readonly deactivating = signal(false);
  protected readonly deactivateError = signal<string | null>(null);

  protected readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const inactive = this.showInactive();
    return this.store.materials().filter(
      (m) =>
        (inactive || m.isActive) &&
        (!q ||
          m.code.toLowerCase().includes(q) ||
          m.name.toLowerCase().includes(q) ||
          (m.vendor ?? '').toLowerCase().includes(q)),
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
    getRowId: (r: MaterialModel) => String(r.id),
  }));

  constructor() {
    effect(() => {
      this.search();
      this.showInactive();
      untracked(() => this.table.resetPageIndex());
    });
  }

  ngOnInit(): void {
    void this.store.load();
  }

  protected cellClass(id: string): string {
    switch (id) {
      case 'code':
        return 'font-medium';
      case 'vendor':
      case 'vendorMaterialId':
        return 'text-muted-foreground';
      default:
        return '';
    }
  }

  protected resetFilters(): void {
    this.search.set('');
    this.showInactive.set(true);
  }

  protected openCreate(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }

  protected askDeactivate(m: MaterialModel): void {
    this.toDeactivate.set(m);
    this.deactivateError.set(null);
  }

  protected openEdit(m: MaterialModel): void {
    this.editing.set(m);
    this.formOpen.set(true);
  }

  protected onSaved(): void {
    this.formOpen.set(false);
    void this.store.load();
  }

  protected async confirmDeactivate(m: MaterialModel): Promise<void> {
    this.deactivating.set(true);
    this.deactivateError.set(null);
    try {
      await this.store.deactivate(m.id);
      this.toDeactivate.set(null);
      await this.store.load();
    } catch (e) {
      this.deactivateError.set(toFailure(e).message);
    } finally {
      this.deactivating.set(false);
    }
  }
}
