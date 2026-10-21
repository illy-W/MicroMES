import { ChangeDetectionStrategy, Component, computed, model, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCopy } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { HlmLabel } from '@spartan-ng/helm/label';
import { formatHours, TimeRange, WEEK_DAYS, weekMinutes } from './calendar-time';
import { TimeRangesEditorComponent } from './time-ranges-editor.component';

/** Editor settimanale: una riga per giorno (lunedì–domenica), ciascuna con le sue fasce. */
@Component({
  selector: 'app-weekly-editor',
  imports: [HlmButton, HlmCheckbox, HlmLabel, NgIcon, TimeRangesEditorComponent],
  providers: [provideIcons({ lucideCopy })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col">
      @for (d of days; track d.key; let i = $index) {
        <div class="grid grid-cols-[7rem_1fr_auto] items-start gap-x-3 border-b py-2 last:border-b-0">
          <div class="pt-1.5 text-sm font-medium">{{ d.label }}</div>
          <app-time-ranges-editor [label]="d.label" [ranges]="value()[i]" (rangesChange)="setDay(i, $event)" />
          <button
            hlmBtn
            type="button"
            variant="ghost"
            size="sm"
            [disabled]="!value()[i].length"
            [attr.aria-label]="'Copia gli orari di ' + d.label + ' su altri giorni'"
            [attr.aria-expanded]="copyFrom() === i"
            (click)="toggleCopy(i)"
          >
            <ng-icon name="lucideCopy" /> Copia su altri giorni
          </button>
          @if (copyFrom() === i) {
            <fieldset class="bg-muted/40 col-span-3 mt-2 flex flex-wrap items-center gap-x-4 gap-y-2 rounded-md border px-3 py-2">
              <legend class="sr-only">Copia gli orari di {{ d.label }} su</legend>
              <span class="text-sm">Copia su:</span>
              @for (t of days; track t.key; let j = $index) {
                @if (j !== i) {
                  <span class="flex items-center gap-1.5">
                    <hlm-checkbox [inputId]="'copy-' + j" [checked]="targets().has(j)" (checkedChange)="toggleTarget(j, $event)" />
                    <label hlmLabel [for]="'copy-' + j">{{ t.short }}</label>
                  </span>
                }
              }
              <span class="ml-auto flex gap-2">
                <button hlmBtn type="button" size="sm" [disabled]="!targets().size" (click)="applyCopy(i)">Applica</button>
                <button hlmBtn type="button" size="sm" variant="outline" (click)="copyFrom.set(null)">Annulla</button>
              </span>
            </fieldset>
          }
        </div>
      }
      <p class="text-muted-foreground pt-3 text-sm" aria-live="polite">
        Totale settimanale: <span class="text-foreground font-semibold tabular-nums">{{ total() }}</span>
      </p>
    </div>
  `,
})
export class WeeklyEditorComponent {
  readonly value = model.required<TimeRange[][]>();
  protected readonly days = WEEK_DAYS;
  protected readonly total = computed(() => formatHours(weekMinutes(this.value())));
  protected readonly copyFrom = signal<number | null>(null);
  protected readonly targets = signal<ReadonlySet<number>>(new Set());

  protected setDay(i: number, ranges: TimeRange[]): void {
    this.value.update((v) => v.map((d, k) => (k === i ? ranges : d)));
  }

  protected toggleCopy(i: number): void {
    this.targets.set(new Set());
    this.copyFrom.set(this.copyFrom() === i ? null : i);
  }

  protected toggleTarget(j: number, on: boolean): void {
    const next = new Set(this.targets());
    if (on) next.add(j);
    else next.delete(j);
    this.targets.set(next);
  }

  protected applyCopy(i: number): void {
    const source = this.value()[i];
    const targets = this.targets();
    this.value.update((v) => v.map((d, k) => (targets.has(k) ? source.map((r) => ({ ...r })) : d)));
    this.copyFrom.set(null);
  }
}
