import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideTrash2 } from '@ng-icons/lucide';
import type { RoutingStepToolingModel, SetupAttributeModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { duplicateAttributeIds, type SetupValueRow, toggleId, type ToolingOption, toolingChoices } from './routing-step-extras';

/** Editor di «Attrezzature richieste» e «Attributi di cambio» di un passo del ciclo. */
@Component({
  selector: 'app-routing-step-extras',
  imports: [HlmBadge, HlmButton, HlmCheckbox, HlmInput, HlmLabel, HlmSelectImports, NgIcon],
  providers: [provideIcons({ lucidePlus, lucideTrash2 })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="flex flex-col gap-1.5">
      <legend class="text-sm font-medium">Attrezzature richieste</legend>
      <p class="text-muted-foreground text-xs" [id]="idPrefix() + '-tl-hint'">Una unità per ciascuna. Nessuna selezione = il passo non richiede attrezzature.</p>
      <ul class="max-h-40 overflow-auto rounded-md border" [attr.aria-describedby]="idPrefix() + '-tl-hint'">
        @for (c of choices(); track c.id) {
          <li class="hover:bg-muted flex items-center gap-2 px-3 py-1.5">
            <hlm-checkbox
              [inputId]="idPrefix() + '-tl-' + c.id"
              [checked]="toolingIds().includes(c.id)"
              [disabled]="c.linkedInactive"
              (checkedChange)="toolingIdsChange.emit(toggle(c.id, $event))"
            />
            <label hlmLabel [for]="idPrefix() + '-tl-' + c.id" class="flex flex-1 flex-wrap items-center gap-x-1 text-sm">
              <span class="font-medium">{{ c.code }}</span>
              <span class="text-muted-foreground">{{ c.name }}</span>
              @if (c.linkedInactive) {
                <span hlmBadge variant="outline" class="px-1 py-0 text-[10px]">disattivata</span>
              }
            </label>
            @if (c.linkedInactive) {
              <button hlmBtn variant="ghost" size="icon-sm" type="button" [attr.aria-label]="'Rimuovi ' + c.code" (click)="toolingIdsChange.emit(toggle(c.id, false))">
                <ng-icon name="lucideTrash2" />
              </button>
            }
          </li>
        } @empty {
          <li class="text-muted-foreground px-3 py-2 text-sm">Nessuna attrezzatura attiva.</li>
        }
      </ul>
    </fieldset>

    <fieldset class="flex flex-col gap-1.5">
      <legend class="text-sm font-medium">Attributi di cambio</legend>
      <p class="text-muted-foreground text-xs">Valore dell'attributo per questo passo (testo libero, es. «rosso»). Valore vuoto = attributo rimosso dal passo.</p>
      @for (r of rows(); track $index; let i = $index) {
        <div class="flex items-start gap-2" role="group" [attr.aria-label]="'Attributo ' + (i + 1)">
          <div class="flex min-w-0 flex-1 flex-col gap-1">
            <hlm-select [value]="r.attributeId" [itemToString]="attrLabel" (valueChange)="setAttr(i, $event)">
              <hlm-select-trigger class="w-full" [attr.aria-label]="'Attributo, riga ' + (i + 1)"><hlm-select-value placeholder="Scegli attributo" /></hlm-select-trigger>
              <hlm-select-content *hlmSelectPortal>
                @for (a of attributeOptions(r.attributeId); track a.id) {
                  <hlm-select-item [value]="a.id" [disabled]="usedElsewhere(a.id, i)">{{ a.code }} — {{ a.name }}{{ a.isActive ? '' : ' (disattivato)' }}</hlm-select-item>
                }
              </hlm-select-content>
            </hlm-select>
            <input
              hlmInput
              class="h-8"
              type="text"
              maxlength="200"
              placeholder="Valore"
              [attr.aria-label]="'Valore, riga ' + (i + 1)"
              [value]="r.value"
              (input)="setValue(i, $any($event.target).value)"
            />
          </div>
          <button hlmBtn variant="ghost" size="icon-sm" type="button" [attr.aria-label]="'Rimuovi attributo, riga ' + (i + 1)" (click)="remove(i)">
            <ng-icon name="lucideTrash2" />
          </button>
        </div>
      } @empty {
        <p class="text-muted-foreground text-sm">Nessun attributo impostato.</p>
      }
      @if (duplicates().length) {
        <p class="text-destructive text-xs" role="alert">Lo stesso attributo compare in più righe: tienine una sola.</p>
      }
      <button hlmBtn variant="outline" size="sm" type="button" class="w-fit" [disabled]="!attributes().length" (click)="add()">
        <ng-icon name="lucidePlus" /> Aggiungi attributo
      </button>
    </fieldset>
  `,
})
export class RoutingStepExtrasComponent {
  readonly idPrefix = input.required<string>();
  readonly tooling = input.required<readonly ToolingOption[]>();
  readonly linkedTooling = input.required<readonly RoutingStepToolingModel[]>();
  readonly toolingIds = input.required<readonly number[]>();
  readonly attributes = input.required<readonly SetupAttributeModel[]>();
  readonly rows = input.required<readonly SetupValueRow[]>();

  readonly toolingIdsChange = output<number[]>();
  readonly rowsChange = output<SetupValueRow[]>();

  protected readonly choices = computed(() => toolingChoices(this.tooling(), this.linkedTooling()));
  protected readonly duplicates = computed(() => duplicateAttributeIds(this.rows()));
  protected readonly attrLabel = (id: number | null): string => {
    const a = this.attributes().find((x) => x.id === id);
    return a ? `${a.code} — ${a.name}` : '';
  };

  protected toggle(id: number, on: boolean): number[] {
    return toggleId(this.toolingIds(), id, on);
  }

  /** Attive più quella già scelta nella riga, anche se disattivata. */
  protected attributeOptions(current: number | null): SetupAttributeModel[] {
    return this.attributes().filter((a) => a.isActive || a.id === current);
  }

  protected usedElsewhere(id: number, index: number): boolean {
    return this.rows().some((r, i) => i !== index && r.attributeId === id);
  }

  protected add(): void {
    this.rowsChange.emit([...this.rows(), { attributeId: null, value: '' }]);
  }

  protected remove(index: number): void {
    this.rowsChange.emit(this.rows().filter((_, i) => i !== index));
  }

  protected setAttr(index: number, id: number | null | undefined): void {
    this.rowsChange.emit(this.rows().map((r, i) => (i === index ? { ...r, attributeId: id ?? null } : r)));
  }

  protected setValue(index: number, value: string): void {
    this.rowsChange.emit(this.rows().map((r, i) => (i === index ? { ...r, value } : r)));
  }
}
