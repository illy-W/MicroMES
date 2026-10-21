import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideChevronRight,
  lucideCircleCheck,
  lucideCircleOff,
  lucideListTree,
  lucidePlus,
  lucideTriangleAlert,
} from '@ng-icons/lucide';
import {
  columnVisibilityFeature,
  createColumnHelper,
  createExpandedRowModel,
  type ExpandedState,
  FlexRender,
  injectTable,
  rowExpandingFeature,
  tableFeatures,
} from '@tanstack/angular-table';
import type { BomNodeModel } from '@micromes/api-client/models';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { map } from 'rxjs';
import { typeLabel, unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { BomItemDialogComponent } from './bom-item-dialog.component';
import { BomRowActionsComponent } from './bom-row-actions.component';
import { BomStore } from './bom.store';

const features = tableFeatures({
  columnVisibilityFeature,
  rowExpandingFeature,
  expandedRowModel: createExpandedRowModel(),
});

const columnHelper = createColumnHelper<typeof features, BomNodeModel>();

const columns = columnHelper.columns([
  columnHelper.accessor('code', { id: 'component', header: 'Componente' }),
  columnHelper.accessor((r) => typeLabel(r.type), { id: 'type', header: 'Tipo' }),
  columnHelper.accessor('quantity', { id: 'quantity', header: 'Quantità' }),
  columnHelper.accessor((r) => unitLabel(r.unitOfMeasure), { id: 'unit', header: 'Unità' }),
  columnHelper.accessor((r) => r.effectiveStep?.id ?? null, { id: 'step', header: 'Usato nel passo' }),
  columnHelper.accessor('isActive', { id: 'status', header: 'Stato' }),
  columnHelper.display({ id: 'actions', header: 'Azioni' }),
]);

const quantityFormat = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 4 });

@Component({
  selector: 'app-bom',
  imports: [
    RouterLink,
    HlmTableImports,
    HlmEmptyImports,
    HlmAlertDialogImports,
    HlmBadge,
    HlmButton,
    HlmSkeleton,
    NgIcon,
    FlexRender,
    BomRowActionsComponent,
    BomItemDialogComponent,
  ],
  providers: [
    BomStore,
    provideIcons({
      lucideChevronRight,
      lucideCircleCheck,
      lucideCircleOff,
      lucideListTree,
      lucidePlus,
      lucideTriangleAlert,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="flex items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
          <a hlmBtn variant="link" size="sm" class="h-auto w-fit p-0" routerLink="/materials">← Materiali</a>
          <h1 class="text-xl font-semibold">
            Distinta base
            @if (store.material(); as m) {
              <span class="text-muted-foreground font-normal">· {{ m.code }} — {{ m.name }}</span>
            }
          </h1>
          <p class="text-muted-foreground text-sm">Componenti necessari a produrre il materiale, su più livelli.</p>
        </div>
        <button hlmBtn size="sm" type="button" [disabled]="!store.material()" (click)="openAdd()">
          <ng-icon name="lucidePlus" /> Aggiungi componente
        </button>
      </div>

      @if (store.loading() && !store.nodes().length) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento distinta base">
          @for (i of [1, 2, 3, 4, 5]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (store.loadError()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare la distinta base</div>
            <div hlmEmptyDescription>{{ store.loadError() }}</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="store.reload()">Riprova</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (!store.nodes().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideListTree" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessun componente</div>
            <div hlmEmptyDescription>Aggiungi i componenti necessari a produrre questo materiale.</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="openAdd()"><ng-icon name="lucidePlus" /> Aggiungi componente</button>
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
                      <th hlmTh [class.text-right]="h.column.id === 'quantity'">
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
                      <td hlmTd [class]="cell.column.id === 'quantity' ? 'text-right tabular-nums' : ''">
                        @switch (cell.column.id) {
                          @case ('component') {
                            <div class="flex items-center gap-1" [style.padding-left.rem]="row.depth * 1.5">
                              @if (row.getCanExpand()) {
                                <button
                                  hlmBtn
                                  variant="ghost"
                                  size="icon-sm"
                                  type="button"
                                  [attr.aria-expanded]="row.getIsExpanded()"
                                  [attr.aria-label]="(row.getIsExpanded() ? 'Comprimi ' : 'Espandi ') + row.original.code"
                                  (click)="row.toggleExpanded()"
                                >
                                  <ng-icon
                                    name="lucideChevronRight"
                                    class="transition-transform"
                                    [class.rotate-90]="row.getIsExpanded()"
                                  />
                                </button>
                              } @else {
                                <span class="inline-block size-7 shrink-0" aria-hidden="true"></span>
                              }
                              <span class="font-medium">{{ row.original.code }}</span>
                              <span class="text-muted-foreground">{{ row.original.name }}</span>
                            </div>
                          }
                          @case ('type') {
                            <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">{{ cell.getValue() }}</span>
                          }
                          @case ('quantity') {
                            {{ formatQuantity(row.original.quantity) }}
                          }
                          @case ('step') {
                            @if (row.original.effectiveStep; as st) {
                              <span class="whitespace-nowrap">Op. {{ st.operationNumber }} · {{ st.processSegmentName }}</span>
                              @if (st.isAutomatic) {
                                <span class="text-muted-foreground ml-1 text-xs">automatico</span>
                              }
                            } @else {
                              <span class="text-muted-foreground">—</span>
                            }
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
                            @if (row.depth === 0) {
                              <app-bom-row-actions
                                [node]="row.original"
                                (edit)="openEdit($event)"
                                (remove)="toRemove.set($event); removeError.set(null)"
                              />
                            } @else if (row.original.children.length) {
                              <a
                                hlmBtn
                                variant="link"
                                size="sm"
                                [routerLink]="['/materials', row.original.componentMaterialId, 'bom']"
                              >
                                Apri distinta
                              </a>
                            }
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
      }
    </div>

    @if (dialogOpen()) {
      <app-bom-item-dialog [item]="editing()" (saved)="onSaved()" (closed)="dialogOpen.set(false)" />
    }

    @if (toRemove(); as n) {
      <hlm-alert-dialog state="open" (closed)="toRemove.set(null)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Rimuovere il componente?</h2>
              <p hlmAlertDialogDescription>
                {{ n.code }} — {{ n.name }} sarà tolto dalla distinta base di questo materiale.
              </p>
            </hlm-alert-dialog-header>
            @if (removeError()) {
              <p class="text-destructive text-sm" role="alert">{{ removeError() }}</p>
            }
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Annulla</button>
              <button hlmBtn type="button" variant="destructive" [disabled]="removing()" (click)="confirmRemove(n)">
                Rimuovi
              </button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }
  `,
})
export class BomComponent {
  protected readonly store = inject(BomStore);

  private readonly materialId = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((p) => Number(p.get('id')))),
    { initialValue: 0 },
  );

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<BomNodeModel | null>(null);
  protected readonly toRemove = signal<BomNodeModel | null>(null);
  protected readonly removing = signal(false);
  protected readonly removeError = signal<string | null>(null);

  private readonly expanded = signal<ExpandedState>({});

  protected readonly table = injectTable(() => ({
    features,
    columns,
    data: this.store.nodes(),
    getSubRows: (r: BomNodeModel) => r.children,
    autoResetExpanded: false,
    state: { expanded: this.expanded() },
    onExpandedChange: (updater) => {
      this.expanded.set(typeof updater === 'function' ? updater(this.expanded()) : updater);
    },
    getRowId: (r: BomNodeModel) => String(r.id),
  }));

  constructor() {
    effect(() => {
      const id = this.materialId();
      untracked(() => {
        this.expanded.set({});
        if (id) void this.store.load(id);
      });
    });
  }

  protected formatQuantity(q: number): string {
    return quantityFormat.format(q);
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.dialogOpen.set(true);
  }

  protected openEdit(n: BomNodeModel): void {
    this.editing.set(n);
    this.dialogOpen.set(true);
  }

  protected onSaved(): void {
    this.dialogOpen.set(false);
    void this.store.reload();
  }

  protected async confirmRemove(n: BomNodeModel): Promise<void> {
    this.removing.set(true);
    this.removeError.set(null);
    try {
      await this.store.remove(n.id);
      this.toRemove.set(null);
      await this.store.reload();
    } catch (e) {
      this.removeError.set(toFailure(e).message);
    } finally {
      this.removing.set(false);
    }
  }
}
