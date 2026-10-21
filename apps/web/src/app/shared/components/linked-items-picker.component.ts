import { ChangeDetectionStrategy, Component, computed, input, model, signal } from '@angular/core';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';

export interface LinkableItem {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
}

/**
 * Selezione multipla con ricerca (checkbox). Mostra gli elementi attivi più
 * quelli già selezionati anche se disattivati (marcati come tali).
 */
@Component({
  selector: 'app-linked-items-picker',
  imports: [HlmCheckbox, HlmInput, HlmLabel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="grid gap-2">
      <legend class="text-sm font-medium">{{ legend() }}</legend>
      <p class="text-muted-foreground text-xs" [id]="idPrefix() + '-hint'">
        {{ hint() }}
        @if (selected().size) {
          Selezionate: {{ selected().size }}.
        }
      </p>
      <input
        hlmInput
        class="h-8"
        type="search"
        [placeholder]="searchLabel()"
        [attr.aria-label]="searchLabel()"
        [value]="query()"
        (input)="query.set($any($event.target).value)"
      />
      @if (loadError()) {
        <p class="text-destructive text-xs" role="alert">{{ loadError() }}</p>
      }
      <ul class="max-h-48 overflow-auto rounded-md border" [attr.aria-describedby]="idPrefix() + '-hint'">
        @for (s of visible(); track s.id) {
          <li class="hover:bg-muted flex items-center gap-2 px-3 py-2">
            <hlm-checkbox [inputId]="idPrefix() + '-' + s.id" [checked]="selected().has(s.id)" (checkedChange)="toggle(s.id, $event)" />
            <label hlmLabel [for]="idPrefix() + '-' + s.id" class="flex-1 cursor-pointer">
              <span class="font-medium">{{ s.code }}</span>
              <span class="text-muted-foreground"> {{ s.name }}{{ s.isActive ? '' : ' (disattivata)' }}</span>
            </label>
          </li>
        } @empty {
          <li class="text-muted-foreground px-3 py-2 text-sm">{{ emptyText() }}</li>
        }
      </ul>
    </fieldset>
  `,
})
export class LinkedItemsPickerComponent {
  readonly items = input.required<readonly LinkableItem[]>();
  readonly selected = model<ReadonlySet<number>>(new Set());
  readonly legend = input.required<string>();
  readonly hint = input.required<string>();
  readonly searchLabel = input.required<string>();
  readonly emptyText = input('Nessun elemento trovato.');
  readonly idPrefix = input.required<string>();
  readonly loadError = input<string | null>(null);

  protected readonly query = signal('');

  protected readonly visible = computed(() => {
    const q = this.query().trim().toLowerCase();
    const sel = this.selected();
    return this.items().filter(
      (s) => (s.isActive || sel.has(s.id)) && (!q || s.code.toLowerCase().includes(q) || s.name.toLowerCase().includes(q)),
    );
  });

  protected toggle(id: number, checked: boolean): void {
    const next = new Set(this.selected());
    if (checked) next.add(id);
    else next.delete(id);
    this.selected.set(next);
  }
}
