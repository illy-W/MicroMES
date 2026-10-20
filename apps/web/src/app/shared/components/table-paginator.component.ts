import { Component, model } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronLeft, lucideChevronRight, lucideChevronsLeft, lucideChevronsRight } from '@ng-icons/lucide';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';

/**
 * Tipo strutturale minimo: `Table<TFeatures, TData>` è invariante (`in out`) e ogni tabella ha feature/righe
 * diverse, quindi neppure `Table<any, any>` accetta un `AngularTable` concreto. Basta che la tabella usi
 * `rowPaginationFeature` (+ `paginatedRowModel`).
 */
export interface PaginatedTable {
  atoms: { pagination: { get(): { pageIndex: number; pageSize: number } } };
  setPageSize(size: number): void;
  resetPageIndex(defaultState?: boolean): void;
  getPageCount(): number;
  getCanPreviousPage(): boolean;
  getCanNextPage(): boolean;
  firstPage(): void;
  previousPage(): void;
  nextPage(): void;
  lastPage(): void;
}

@Component({
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'div [table-paginator]',
  imports: [NgIcon, HlmButtonImports, HlmSelectImports, HlmLabel],
  providers: [provideIcons({ lucideChevronLeft, lucideChevronRight, lucideChevronsLeft, lucideChevronsRight })],
  template: `
    <div class="mt-2 flex gap-8 sm:mt-0">
      <div class="flex items-center gap-2">
        <span hlmLabel>Righe per pagina:</span>
        <hlm-select
          class="inline-block"
          [value]="table().atoms.pagination.get().pageSize"
          (valueChange)="table().setPageSize($event ?? 10); table().resetPageIndex()"
        >
          <hlm-select-trigger size="sm" class="mr-1 inline-flex h-8 w-fit">
            <hlm-select-value />
          </hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-group>
              @for (size of availablePageSizes; track size) {
                <hlm-select-item [value]="size">{{ size }}</hlm-select-item>
              }
            </hlm-select-group>
          </hlm-select-content>
        </hlm-select>
      </div>

      <span class="flex items-center" hlmLabel>
        Pagina {{ table().atoms.pagination.get().pageIndex + 1 }} di {{ table().getPageCount() }}
      </span>

      <div class="flex gap-1">
        <button hlmBtn variant="outline" size="icon-sm" aria-label="Prima pagina" [disabled]="!table().getCanPreviousPage()" (click)="table().firstPage()">
          <ng-icon name="lucideChevronsLeft" />
        </button>
        <button hlmBtn variant="outline" size="icon-sm" aria-label="Pagina precedente" [disabled]="!table().getCanPreviousPage()" (click)="table().previousPage()">
          <ng-icon name="lucideChevronLeft" />
        </button>
        <button hlmBtn variant="outline" size="icon-sm" aria-label="Pagina successiva" [disabled]="!table().getCanNextPage()" (click)="table().nextPage()">
          <ng-icon name="lucideChevronRight" />
        </button>
        <button hlmBtn variant="outline" size="icon-sm" aria-label="Ultima pagina" [disabled]="!table().getCanNextPage()" (click)="table().lastPage()">
          <ng-icon name="lucideChevronsRight" />
        </button>
      </div>
    </div>
  `,
})
export class TablePaginatorComponent {
  public table = model.required<PaginatedTable>();

  protected readonly availablePageSizes = [5, 10, 20];
}
