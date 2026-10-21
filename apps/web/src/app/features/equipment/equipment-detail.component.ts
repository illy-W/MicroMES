import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCircleCheck, lucideCircleOff, lucideWrench } from '@ng-icons/lucide';
import type { EquipmentModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { formatHours, formatDate } from '../calendars/calendar-time';
import { CalendarExceptionsComponent } from '../calendars/calendar-exceptions.component';
import { EquipmentStore } from './equipment.store';
import { CalendarViewComponent } from '../calendars/calendar-view.component';

/** Dettaglio macchina: calendario effettivo, prossime chiusure, eccezioni proprie e calendario mensile. */
@Component({
  selector: 'app-equipment-detail',
  imports: [HlmBadge, HlmButton, HlmTabsImports, NgIcon, CalendarExceptionsComponent, CalendarViewComponent],
  providers: [provideIcons({ lucideArrowLeft, lucideCircleCheck, lucideCircleOff, lucideWrench })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div>
        <button hlmBtn variant="ghost" size="sm" type="button" (click)="back.emit()"><ng-icon name="lucideArrowLeft" /> Tutte le macchine</button>
      </div>
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="flex flex-col gap-1">
          <h2 class="flex flex-wrap items-center gap-2 text-lg font-semibold">
            {{ equipment().name }}
            <span class="text-muted-foreground text-sm font-normal">{{ equipment().code }}</span>
            <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
              @if (equipment().isActive) { <ng-icon name="lucideCircleCheck" /> Attiva } @else { <ng-icon name="lucideCircleOff" /> Disattivata }
            </span>
          </h2>
          <p class="text-muted-foreground text-sm">Capacità {{ equipment().capacity }} · efficienza {{ equipment().efficiencyPercent }}%</p>
        </div>
        <button hlmBtn size="sm" variant="outline" type="button" (click)="edit.emit(equipment())">Modifica macchina</button>
      </div>

      <hlm-tabs [tab]="tab()" (tabActivated)="setTab($any($event))">
        <hlm-tabs-list aria-label="Sezioni della macchina">
          <button hlmTabsTrigger="overview" type="button">Calendario effettivo</button>
          <button hlmTabsTrigger="exceptions" type="button">Eccezioni della macchina</button>
          <button hlmTabsTrigger="calendar" type="button">Calendario mensile</button>
        </hlm-tabs-list>
      </hlm-tabs>

      @switch (tab()) {
        @case ('overview') {
          <div class="grid gap-4 lg:grid-cols-2">
            <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="eq-eff-title">
              <h3 id="eq-eff-title" class="text-sm font-semibold">Calendario effettivo</h3>
              @if (equipment().effectiveCalendar; as c) {
                <p class="text-sm">
                  <span class="font-medium">{{ c.name }}</span> <span class="text-muted-foreground">({{ c.code }})</span>
                </p>
                <p class="text-muted-foreground text-sm">
                  {{ c.isInherited ? 'Ereditato dal predefinito aziendale.' : 'Calendario proprio della macchina.' }}
                  Ore settimanali: <span class="text-foreground font-semibold tabular-nums">{{ hours(c.weeklyHours) }}</span>
                </p>
              } @else {
                <p class="text-muted-foreground text-sm">Informazione non disponibile.</p>
              }
              <button hlmBtn class="self-start" size="sm" variant="outline" type="button" (click)="setTab('calendar')">Vedi il calendario mensile</button>
            </section>
            <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="eq-closures-title">
              <h3 id="eq-closures-title" class="text-sm font-semibold">Prossime chiusure</h3>
              @if (equipment().effectiveCalendar?.upcomingClosures?.length) {
                <ul class="flex flex-col gap-1.5 text-sm">
                  @for (u of equipment().effectiveCalendar!.upcomingClosures; track $index) {
                    <li class="flex flex-wrap items-center gap-x-2">
                      <span class="font-medium tabular-nums">{{ period(u.fromDate, u.toDate) }}</span>
                      <span>{{ u.reason }}</span>
                      @if (u.scope === 'Equipment') {
                        <span hlmBadge variant="outline" class="gap-1"><ng-icon name="lucideWrench" class="size-3" aria-hidden="true" /> Macchina</span>
                      } @else {
                        <span class="text-muted-foreground text-xs">Calendario</span>
                      }
                    </li>
                  }
                </ul>
              } @else {
                <p class="text-muted-foreground text-sm">Nessuna chiusura in programma.</p>
              }
            </section>
          </div>
        }
        @case ('exceptions') {
          <app-calendar-exceptions [calendarId]="equipment().id" owner="equipment" />
        }
        @default {
          <app-calendar-view [equipmentId]="equipment().id" [lockScope]="true" />
        }
      }
    </div>
  `,
})
export class EquipmentDetailComponent {
  readonly equipment = input.required<EquipmentModel>();
  readonly back = output<void>();
  readonly edit = output<EquipmentModel>();

  private readonly store = inject(EquipmentStore);
  protected readonly tab = signal<string>('overview');
  protected setTab(t: string): void {
    this.tab.set(t);
    // Le eccezioni modificate cambiano le prossime chiusure: riallinea il calendario effettivo.
    if (t === 'overview') void this.store.load();
  }
  protected readonly hours = (weekly: number): string => formatHours(weekly * 60);
  protected period(from: string, to: string): string {
    return from === to ? formatDate(from) : `${formatDate(from)} – ${formatDate(to)}`;
  }
}
