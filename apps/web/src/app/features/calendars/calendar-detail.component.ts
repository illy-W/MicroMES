import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCircleCheck, lucideCircleOff, lucideStar } from '@ng-icons/lucide';
import type { WorkCalendarModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { formatHours, slotsToDays, summarizeRanges, WEEK_DAYS, weekMinutes } from './calendar-time';
import { CalendarExceptionsComponent } from './calendar-exceptions.component';
import { CalendarViewComponent } from './calendar-view.component';
import { CalendarPreviewComponent } from './calendar-preview.component';

@Component({
  selector: 'app-calendar-detail',
  imports: [HlmBadge, HlmButton, HlmTabsImports, NgIcon, CalendarExceptionsComponent, CalendarPreviewComponent, CalendarViewComponent],
  providers: [provideIcons({ lucideArrowLeft, lucideCircleCheck, lucideCircleOff, lucideStar })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-start gap-3">
        <button hlmBtn variant="ghost" size="sm" type="button" (click)="back.emit()"><ng-icon name="lucideArrowLeft" /> Tutti i calendari</button>
      </div>
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="flex flex-col gap-1">
          <h2 class="flex flex-wrap items-center gap-2 text-lg font-semibold">
            {{ calendar().name }}
            <span class="text-muted-foreground text-sm font-normal">{{ calendar().code }}</span>
            @if (calendar().isDefault) {
              <span hlmBadge variant="outline" class="border-primary/40 bg-primary/10 text-primary gap-1 font-semibold">
                <ng-icon name="lucideStar" class="size-3 fill-current" aria-hidden="true" /> Predefinito
              </span>
            }
            <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
              @if (calendar().isActive) { <ng-icon name="lucideCircleCheck" /> Attivo } @else { <ng-icon name="lucideCircleOff" /> Disattivato }
            </span>
          </h2>
          @if (calendar().description) {
            <p class="text-muted-foreground text-sm">{{ calendar().description }}</p>
          }
        </div>
        <button hlmBtn size="sm" variant="outline" type="button" (click)="edit.emit(calendar())">Modifica orari e dati</button>
      </div>

      <hlm-tabs [tab]="tab()" (tabActivated)="tab.set($any($event))">
        <hlm-tabs-list aria-label="Sezioni del calendario">
          <button hlmTabsTrigger="hours" type="button">Orari</button>
          <button hlmTabsTrigger="exceptions" type="button">Eccezioni</button>
          <button hlmTabsTrigger="calendar" type="button">Calendario</button>
        </hlm-tabs-list>
      </hlm-tabs>

      @if (tab() === 'hours') {
        <div class="grid gap-4 lg:grid-cols-2">
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
          <app-calendar-preview [calendarId]="calendar().id" />
        </div>
      } @else if (tab() === 'calendar') {
        <app-calendar-view [calendarId]="calendar().id" />
      } @else {
        <app-calendar-exceptions [calendarId]="calendar().id" />
      }
    </div>
  `,
})
export class CalendarDetailComponent {
  readonly calendar = input.required<WorkCalendarModel>();
  readonly back = output<void>();
  readonly edit = output<WorkCalendarModel>();

  protected readonly tab = signal<string>('hours');
  protected readonly days = WEEK_DAYS;
  protected readonly week = computed(() => slotsToDays(this.calendar().slots));
  protected readonly total = computed(() => formatHours(weekMinutes(this.week())));
  protected summary(i: number): string {
    return summarizeRanges(this.week()[i]);
  }
}
