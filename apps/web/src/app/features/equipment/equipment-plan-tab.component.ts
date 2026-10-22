import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideTriangleAlert } from '@ng-icons/lucide';
import type { EquipmentModel } from '@micromes/api-client/models';
import { PlanningService } from '@micromes/api-client/services/planning';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { firstValueFrom } from 'rxjs';
import { CalendarsStore } from '../calendars/calendars.store';
import { toFailure } from '../materials/materials.service';
import { PlanningTimelineComponent } from '../planning/planning-timeline.component';
import { TIMELINE_EMBED } from '../planning/timeline-embed';
import { addDays, dayKey } from '../planning/timeline-time';
import { dayStartIso, dailyLoad, type DayLoad } from './equipment-history';
import { loadText, loadWidth } from './equipment-overview';
import { EquipmentPageStore } from './equipment-page.store';

export const PLAN_DAYS = 14;
const DAY_FMT = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' });

/** Scheda «Pianificazione»: timeline compatta della sola macchina (14 giorni) e carico per giorno. */
@Component({
  selector: 'app-equipment-plan-tab',
  imports: [HlmButton, HlmSkeleton, NgIcon, RouterLink, PlanningTimelineComponent],
  providers: [
    provideIcons({ lucideTriangleAlert }),
    { provide: TIMELINE_EMBED, useFactory: () => ({ equipmentId: inject(EquipmentPageStore).equipment()?.id ?? 0 }) },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <p class="text-muted-foreground text-sm">Prossimi {{ days }} giorni della sola macchina. Clic su una barra per i dettagli del lavoro.</p>
        <a hlmBtn size="sm" variant="outline" routerLink="/planning" [queryParams]="{ machine: equipment().id }">Apri nella pianificazione completa</a>
      </div>

      <div class="flex h-[28rem] min-h-[20rem] min-w-0 flex-col">
        <app-planning-timeline class="flex min-h-0 min-w-0 flex-1 flex-col" />
      </div>

      <section class="flex flex-col gap-3 rounded-md border p-4" aria-labelledby="eq-dayload">
        <h3 id="eq-dayload" class="text-sm font-semibold">Carico per giorno</h3>
        @if (loading()) {
          <div aria-busy="true" aria-label="Caricamento carico"><hlm-skeleton class="h-24 w-full" /></div>
        } @else if (error()) {
          <div class="flex flex-col items-start gap-2" role="alert">
            <p class="text-sm">Impossibile caricare il carico: {{ error() }}</p>
            <button hlmBtn size="sm" type="button" (click)="load()">Riprova</button>
          </div>
        } @else {
          <ul class="grid gap-x-6 gap-y-2 md:grid-cols-2">
            @for (d of rows(); track d.key) {
              <li class="flex flex-col gap-1">
                <div class="flex items-center justify-between text-sm">
                  <span class="capitalize">{{ d.label }}</span>
                  <span class="flex items-center gap-1 font-medium tabular-nums" [class.text-destructive]="d.high">
                    @if (d.high) { <ng-icon name="lucideTriangleAlert" size="14" aria-hidden="true" /> }
                    {{ d.percent === null ? 'chiusa' : d.text }}
                    @if (d.high) { <span class="sr-only">(carico alto)</span> }
                  </span>
                </div>
                <div class="bg-muted h-2 overflow-hidden rounded-full" role="meter" [attr.aria-label]="'Carico ' + d.label" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="d.percent ?? 0" [attr.aria-valuetext]="d.percent === null ? 'chiusa' : d.text">
                  <div class="h-full rounded-full" [class.bg-primary]="!d.high" [class.bg-destructive]="d.high" [style.width.%]="d.width"></div>
                </div>
              </li>
            }
          </ul>
        }
      </section>
    </div>
  `,
})
export class EquipmentPlanTabComponent implements OnInit {
  readonly equipment = input.required<EquipmentModel>();
  private readonly api = inject(PlanningService);
  private readonly calendars = inject(CalendarsStore);

  protected readonly days = PLAN_DAYS;
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  private readonly load14 = signal<DayLoad[]>([]);

  protected readonly rows = computed(() =>
    this.load14().map((d) => ({
      ...d,
      label: DAY_FMT.format(new Date(`${d.key}T12:00:00Z`)),
      text: loadText(d.percent),
      width: loadWidth(d.percent),
    })),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.calendars.loadTimeZone();
      const tz0 = this.calendars.timeZone();
      const from = dayKey(Date.now(), tz0);
      const model = await firstValueFrom(
        this.api.getPlanningTimeline({ from: dayStartIso(from, tz0), to: dayStartIso(addDays(from, PLAN_DAYS), tz0), equipmentId: this.equipment().id }),
      );
      this.load14.set(dailyLoad(model.load, this.equipment().id, dayKey(Date.now(), model.timeZoneId), PLAN_DAYS, model.timeZoneId));
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }
}
