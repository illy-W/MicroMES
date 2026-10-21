import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideTrash2 } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmInput } from '@spartan-ng/helm/input';
import { TimeRange, validateRanges } from './calendar-time';

/** Elenco di fasce orarie (inizio–fine) con validazione in linea. Nessuna fascia = giorno chiuso. */
@Component({
  selector: 'app-time-ranges-editor',
  imports: [HlmButton, HlmInput, NgIcon],
  providers: [provideIcons({ lucidePlus, lucideTrash2 })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-1.5">
      @for (r of ranges(); track $index; let i = $index) {
        <div class="flex flex-wrap items-center gap-2">
          <input
            hlmInput
            class="h-8 w-28"
            type="time"
            step="300"
            [value]="r.start"
            [attr.aria-label]="label() + ', fascia ' + (i + 1) + ', inizio'"
            [attr.aria-invalid]="errors()[i] ? true : null"
            (input)="update(i, 'start', $any($event.target).value)"
          />
          <span class="text-muted-foreground" aria-hidden="true">–</span>
          <input
            hlmInput
            class="h-8 w-28"
            type="time"
            step="300"
            [value]="r.end"
            [attr.aria-label]="label() + ', fascia ' + (i + 1) + ', fine'"
            [attr.aria-invalid]="errors()[i] ? true : null"
            (input)="update(i, 'end', $any($event.target).value)"
          />
          <button hlmBtn type="button" variant="ghost" size="icon-sm" [attr.aria-label]="'Rimuovi fascia ' + (i + 1) + ' (' + label() + ')'" (click)="remove(i)">
            <ng-icon name="lucideTrash2" />
          </button>
          @if (errors()[i]; as e) {
            <span class="text-destructive text-xs" role="alert">{{ e }}</span>
          }
        </div>
      } @empty {
        <span class="text-muted-foreground py-1 text-sm">{{ emptyText() }}</span>
      }
      <div>
        <button hlmBtn type="button" variant="ghost" size="sm" (click)="add()">
          <ng-icon name="lucidePlus" /> Aggiungi fascia<span class="sr-only"> ({{ label() }})</span>
        </button>
      </div>
    </div>
  `,
})
export class TimeRangesEditorComponent {
  readonly ranges = model<TimeRange[]>([]);
  readonly label = input('Fasce orarie');
  readonly emptyText = input('Chiuso');

  protected readonly errors = computed(() => validateRanges(this.ranges()));

  protected update(i: number, field: 'start' | 'end', value: string): void {
    this.ranges.update((rs) => rs.map((r, k) => (k === i ? { ...r, [field]: value } : r)));
  }

  protected add(): void {
    const rs = this.ranges();
    const last = rs[rs.length - 1];
    this.ranges.set([...rs, last?.end && last.end < '20:00' ? { start: last.end, end: '' } : { start: '08:00', end: '12:00' }]);
  }

  protected remove(i: number): void {
    this.ranges.update((rs) => rs.filter((_, k) => k !== i));
  }
}
