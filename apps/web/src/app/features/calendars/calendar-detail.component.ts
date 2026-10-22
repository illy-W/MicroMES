import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCircleCheck, lucideCircleOff, lucideStar } from '@ng-icons/lucide';
import type { WorkCalendarModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { CalendarWeeklySummaryComponent } from './calendar-weekly-summary.component';
import { CalendarExceptionsComponent } from './calendar-exceptions.component';
import { CalendarViewComponent } from './calendar-view.component';
import { CalendarPreviewComponent } from './calendar-preview.component';

@Component({
  selector: 'app-calendar-detail',
  imports: [HlmBadge, HlmButton, HlmTabsImports, NgIcon, CalendarExceptionsComponent, CalendarPreviewComponent, CalendarViewComponent, CalendarWeeklySummaryComponent],
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
          <app-calendar-weekly-summary [slots]="calendar().slots" />
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
}
