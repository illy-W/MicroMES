import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { WorkCalendarSlotModel } from '@micromes/api-client/models';
import { formatHours, slotsToDays, summarizeRanges, WEEK_DAYS, weekMinutes } from './calendar-time';

/** Orario settimanale (sola lettura): turni per giorno e totale. Condiviso tra calendari e dettaglio macchina. */
@Component({
  selector: 'app-calendar-weekly-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="week-title">
      <h3 id="week-title" class="text-sm font-semibold">Orario settimanale</h3>
      <dl class="grid grid-cols-[7rem_1fr] gap-x-3 gap-y-1.5 text-sm">
        @for (d of days; track d.key; let i = $index) {
          <dt class="font-medium">{{ d.label }}</dt>
          <dd class="tabular-nums" [class.text-muted-foreground]="!week()[i].length">{{ summary(i) }}</dd>
        }
      </dl>
      <p class="text-muted-foreground text-sm">Totale settimanale: <span class="text-foreground font-semibold tabular-nums">{{ total() }}</span></p>
    </section>
  `,
})
export class CalendarWeeklySummaryComponent {
  readonly slots = input.required<readonly WorkCalendarSlotModel[]>();
  protected readonly days = WEEK_DAYS;
  protected readonly week = computed(() => slotsToDays(this.slots()));
  protected readonly total = computed(() => formatHours(weekMinutes(this.week())));
  protected summary(i: number): string {
    return summarizeRanges(this.week()[i]);
  }
}
