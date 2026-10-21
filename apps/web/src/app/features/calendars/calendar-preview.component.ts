import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import type { CalendarPreviewModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { toFailure } from '../materials/materials.service';
import { formatDateTime, formatWeekdayTime, toIsoDate, zonedToUtcIso } from './calendar-time';
import { CalendarsStore } from './calendars.store';

/** Anteprima: da un inizio (ora aziendale) e N minuti, dove finisce il lavoro secondo il calendario. */
@Component({
  selector: 'app-calendar-preview',
  imports: [HlmButton, HlmInput, HlmLabel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="flex flex-col gap-3 rounded-md border p-4" aria-labelledby="cal-preview-title">
      <div>
        <h3 id="cal-preview-title" class="text-sm font-semibold">Anteprima</h3>
        <p class="text-muted-foreground text-xs">Calcola la fine di un lavoro con questo calendario (ora aziendale, {{ store.timeZone() }}).</p>
      </div>
      <form class="flex flex-wrap items-end gap-3" (submit)="$event.preventDefault(); run()">
        <div class="grid gap-1.5">
          <label hlmLabel for="prev-date">Data di inizio</label>
          <input hlmInput id="prev-date" class="h-8 w-40" type="date" [value]="date()" (input)="date.set($any($event.target).value)" />
        </div>
        <div class="grid gap-1.5">
          <label hlmLabel for="prev-time">Ora di inizio</label>
          <input hlmInput id="prev-time" class="h-8 w-28" type="time" step="300" [value]="time()" (input)="time.set($any($event.target).value)" />
        </div>
        <div class="grid gap-1.5">
          <label hlmLabel for="prev-min">Minuti di lavoro</label>
          <input hlmInput id="prev-min" class="h-8 w-28" type="number" min="1" step="1" [value]="minutes()" (input)="minutes.set(+$any($event.target).value)" />
        </div>
        <button hlmBtn size="sm" type="submit" [disabled]="busy() || !date() || !time() || minutes() < 1">{{ busy() ? 'Calcolo…' : 'Calcola' }}</button>
      </form>
      <div aria-live="polite">
        @if (error()) {
          <p class="text-destructive text-sm" role="alert">{{ error() }}</p>
        } @else if (result(); as r) {
          <p class="text-sm">
            <span class="tabular-nums">{{ r.standardMinutes }}</span> minuti da
            <strong>{{ describe(startIso(), r) }}</strong> finiscono
            <strong>{{ endText(r) }}</strong>.
          </p>
          @if (r.firstWorkingInstant !== r.start) {
            <p class="text-muted-foreground text-xs">Il lavoro parte alla prima ora lavorativa: {{ formatDateTime(r.firstWorkingInstant, r.timeZoneId) }}.</p>
          }
        }
      </div>
    </section>
  `,
})
export class CalendarPreviewComponent implements OnInit {
  protected readonly store = inject(CalendarsStore);
  readonly calendarId = input.required<number>();

  protected readonly date = signal('');
  protected readonly time = signal('08:00');
  protected readonly minutes = signal(100);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<CalendarPreviewModel | null>(null);
  protected readonly startIso = signal('');
  protected readonly formatDateTime = formatDateTime;

  ngOnInit(): void {
    void this.store.loadTimeZone();
    const d = new Date();
    d.setDate(d.getDate() + ((8 - d.getDay()) % 7 || 7)); // prossimo lunedì
    this.date.set(toIsoDate(d));
  }

  protected describe(_iso: string, r: CalendarPreviewModel): string {
    return formatWeekdayTime(r.start, r.timeZoneId);
  }

  protected endText(r: CalendarPreviewModel): string {
    const endDay = new Intl.DateTimeFormat('it-IT', { timeZone: r.timeZoneId, day: '2-digit', month: '2-digit' }).format(new Date(r.end));
    return `${formatWeekdayTime(r.end, r.timeZoneId)} (${endDay})`;
  }

  protected async run(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.result.set(null);
    try {
      const iso = zonedToUtcIso(this.date(), this.time(), this.store.timeZone());
      this.startIso.set(iso);
      this.result.set(await this.store.preview(this.calendarId(), iso, this.minutes()));
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.busy.set(false);
    }
  }
}
