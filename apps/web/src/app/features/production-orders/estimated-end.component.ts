import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideInfo, lucideTriangleAlert } from '@ng-icons/lucide';
import { HlmTooltipImports } from '@spartan-ng/helm/tooltip';
import { CalendarsStore } from '../calendars/calendars.store';

/** ISO UTC -> dd/MM HH:mm nel fuso aziendale. */
export const formatEstimatedEnd = (iso: string, tz: string): string => {
  const parts = new Intl.DateTimeFormat('it-IT', {
    timeZone: tz,
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(iso));
  const p = (t: string) => parts.find((x) => x.type === t)?.value ?? '';
  return `${p('day')}/${p('month')} ${p('hour')}:${p('minute')}`;
};

/** Fine prevista (stima): non rende nulla se la stima è assente. */
@Component({
  selector: 'app-estimated-end',
  imports: [NgIcon, HlmTooltipImports],
  providers: [provideIcons({ lucideInfo, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (text(); as t) {
      <span class="flex flex-wrap items-baseline gap-x-1.5 gap-y-0.5">
        <span class="text-muted-foreground inline-flex items-center gap-1">
          {{ planned() ? 'Fine pianificata' : 'Fine prevista (stima)' }}
          <span
            tabindex="0"
            class="inline-flex"
            role="img"
            [attr.aria-label]="tooltip()"
            [hlmTooltip]="tooltip()"
          >
            <ng-icon name="lucideInfo" size="14" aria-hidden="true" />
          </span>
        </span>
        <span class="font-semibold tabular-nums">{{ t }}</span>
        @if (late()) {
          <span class="text-destructive inline-flex items-center gap-1 text-xs font-medium">
            <ng-icon name="lucideTriangleAlert" size="14" aria-hidden="true" /> oltre la data richiesta
          </span>
        }
      </span>
    }
  `,
})
export class EstimatedEndComponent {
  private readonly store = inject(CalendarsStore);

  readonly estimatedEnd = input<string | null | undefined>(null);
  readonly requestedEnd = input<string | null | undefined>(null);
  /** True se la data viene dal piano (non da una stima). */
  readonly planned = input(false);

  protected readonly tooltip = computed(() =>
    this.planned()
      ? 'Dal piano; non considera i ritardi non ancora registrati'
      : 'Stima indicativa: usa i calendari delle macchine e non considera i conflitti di capacità',
  );

  constructor() {
    void this.store.loadTimeZone();
  }

  protected readonly text = computed(() => {
    const e = this.estimatedEnd();
    return e ? formatEstimatedEnd(e, this.store.timeZone()) : null;
  });
  protected readonly late = computed(() => {
    const e = this.estimatedEnd();
    const r = this.requestedEnd();
    return !!e && !!r && new Date(e).getTime() > new Date(r).getTime();
  });
}
