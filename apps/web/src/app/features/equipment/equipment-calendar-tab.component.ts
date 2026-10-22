import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarX, lucideStar } from '@ng-icons/lucide';
import type { EquipmentModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { formatDate } from '../calendars/calendar-time';
import { CalendarExceptionsComponent } from '../calendars/calendar-exceptions.component';
import { CalendarViewComponent } from '../calendars/calendar-view.component';
import { CalendarWeeklySummaryComponent } from '../calendars/calendar-weekly-summary.component';
import { CalendarsStore } from '../calendars/calendars.store';

/** Scheda «Calendario»: calendario seguito (sola lettura), prossime chiusure e gestione delle eccezioni della macchina. */
@Component({
  selector: 'app-equipment-calendar-tab',
  imports: [HlmBadge, HlmButton, NgIcon, RouterLink, CalendarExceptionsComponent, CalendarViewComponent, CalendarWeeklySummaryComponent],
  providers: [provideIcons({ lucideCalendarX, lucideStar })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <p class="flex flex-wrap items-center gap-2 text-sm">
          <span class="text-muted-foreground">Calendario seguito:</span>
          <span class="font-semibold">{{ name() }}</span>
          @if (isDefault()) {
            <span hlmBadge variant="outline" class="border-primary/40 bg-primary/10 text-primary gap-1 font-semibold">
              <ng-icon name="lucideStar" class="size-3 fill-current" aria-hidden="true" /> Predefinito
            </span>
          }
        </p>
        <div class="flex flex-wrap gap-2">
          @if (calendarId(); as cid) {
            <a hlmBtn size="sm" variant="outline" routerLink="/calendars" [queryParams]="{ calendar: cid }">Apri calendario</a>
          }
          <button hlmBtn size="sm" type="button" [attr.aria-expanded]="manage()" aria-controls="eq-exceptions" (click)="manage.set(!manage())">
            {{ manage() ? 'Chiudi gestione eccezioni' : 'Gestisci eccezioni della macchina' }}
          </button>
        </div>
      </div>

      <div class="grid gap-4 lg:grid-cols-2">
        @if (slots(); as s) {
          <app-calendar-weekly-summary [slots]="s" />
        }
        <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="eq-closures">
          <h3 id="eq-closures" class="text-sm font-semibold">Prossime chiusure</h3>
          @if (!closures().length) {
            <p class="text-muted-foreground text-sm">Nessuna chiusura in programma.</p>
          } @else {
            <ul class="flex flex-col gap-1.5 text-sm">
              @for (c of closures(); track $index) {
                <li class="flex flex-wrap items-center gap-x-2">
                  <ng-icon name="lucideCalendarX" size="14" aria-hidden="true" />
                  <span class="font-medium tabular-nums">{{ period(c.fromDate, c.toDate) }}</span>
                  <span>{{ c.reason }}</span>
                  <span hlmBadge variant="outline" class="px-1.5">{{ c.scope === 'Equipment' ? 'Macchina' : 'Aziendale' }}</span>
                </li>
              }
            </ul>
          }
        </section>
      </div>

      @if (manage()) {
        <section id="eq-exceptions" class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="eq-exc-title">
          <h3 id="eq-exc-title" class="text-sm font-semibold">Eccezioni della macchina</h3>
          <app-calendar-exceptions [calendarId]="equipment().id" owner="equipment" />
        </section>
      }

      <app-calendar-view [equipmentId]="equipment().id" [lockScope]="true" />
    </div>
  `,
})
export class EquipmentCalendarTabComponent implements OnInit {
  readonly equipment = input.required<EquipmentModel>();
  private readonly calendars = inject(CalendarsStore);

  protected readonly manage = signal(false);
  protected readonly calendarId = computed(() => this.equipment().effectiveCalendar?.calendarId ?? this.equipment().workCalendarId ?? null);
  protected readonly name = computed(() => this.equipment().effectiveCalendar?.name ?? 'Predefinito aziendale');
  protected readonly isDefault = computed(() => !this.equipment().workCalendarId);
  protected readonly closures = computed(() => this.equipment().effectiveCalendar?.upcomingClosures ?? []);
  protected readonly slots = computed(() => this.calendars.items().find((c) => c.id === this.calendarId())?.slots ?? null);

  ngOnInit(): void {
    if (!this.calendars.items().length) void this.calendars.load();
  }

  protected period(from: string, to: string): string {
    return from === to ? formatDate(from) : `${formatDate(from)} – ${formatDate(to)}`;
  }
}
