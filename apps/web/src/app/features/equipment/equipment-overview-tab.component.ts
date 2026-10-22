import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCircleAlert,
  lucideCircleCheck,
  lucideCircleOff,
  lucideLock,
  lucideMoon,
  lucidePlay,
  lucideTriangleAlert,
  lucideWrench,
} from '@ng-icons/lucide';
import type { EquipmentOverviewModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { CalendarsStore } from '../calendars/calendars.store';
import { PlanStatusBadgeComponent } from '../planning/plan-status-badge.component';
import { PlanningStore } from '../planning/planning.store';
import { formatDateTime } from '../planning/timeline-time';
import { formatElapsed, loadLevel, loadText, loadWidth, STATE_ICON, stateLabel } from './equipment-overview';

/** Scheda «Panoramica»: stato adesso, coda, montaggio, carico, segnalazioni. */
@Component({
  selector: 'app-equipment-overview-tab',
  imports: [HlmBadge, HlmButton, HlmTableImports, NgIcon, RouterLink, PlanStatusBadgeComponent],
  providers: [
    provideIcons({ lucideCircleAlert, lucideCircleCheck, lucideCircleOff, lucideLock, lucideMoon, lucidePlay, lucideTriangleAlert, lucideWrench }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid gap-4 lg:grid-cols-2">
      <section class="flex flex-col gap-3 rounded-md border p-4" aria-labelledby="ov-now">
        <h3 id="ov-now" class="text-sm font-semibold">Adesso</h3>
        <p class="flex items-center gap-2 text-base font-medium">
          <ng-icon [name]="stateIcon()" size="18" aria-hidden="true" />
          {{ stateText() }}
        </p>
        @if (overview().running.length) {
          <ul class="flex flex-col gap-1.5 text-sm" aria-label="Lavori in corso">
            @for (r of overview().running; track r.workOrderId) {
              <li class="flex flex-wrap items-center gap-x-2">
                <a class="font-medium underline underline-offset-2" routerLink="/planning" [queryParams]="{ wo: r.workOrderId }">{{ r.workOrderCode }}</a>
                <span class="text-muted-foreground">{{ r.processSegmentName }} · ordine {{ r.productionOrderCode }}</span>
                <span class="tabular-nums">{{ elapsed(r.elapsedMinutes) }}</span>
              </li>
            }
          </ul>
        }
      </section>

      <section class="flex flex-col gap-3 rounded-md border p-4" aria-labelledby="ov-load">
        <h3 id="ov-load" class="text-sm font-semibold">Carico</h3>
        <ul class="flex flex-col gap-2">
          @for (m of meters(); track m.label) {
            <li class="flex flex-col gap-1">
              <div class="flex items-center justify-between text-sm">
                <span>{{ m.label }}</span>
                <span class="flex items-center gap-1 font-medium tabular-nums" [class.text-destructive]="m.level === 'high'">
                  @if (m.level === 'high') { <ng-icon name="lucideTriangleAlert" size="14" aria-hidden="true" /> }
                  {{ m.text }}
                  @if (m.level === 'high') { <span class="sr-only">(carico alto)</span> }
                </span>
              </div>
              <div class="bg-muted h-2 overflow-hidden rounded-full" role="meter" [attr.aria-label]="'Carico ' + m.label" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="m.value" [attr.aria-valuetext]="m.text">
                <div class="h-full rounded-full" [class.bg-primary]="m.level !== 'high'" [class.bg-destructive]="m.level === 'high'" [style.width.%]="m.width"></div>
              </div>
            </li>
          }
        </ul>
      </section>

      <section class="flex flex-col gap-3 rounded-md border p-4 lg:col-span-2" aria-labelledby="ov-queue">
        <h3 id="ov-queue" class="text-sm font-semibold">Coda</h3>
        @if (!overview().queue.length) {
          <p class="text-muted-foreground text-sm">Nessun lavoro in coda</p>
        } @else {
          <div class="overflow-auto rounded-md border">
            <div hlmTableContainer>
              <table hlmTable>
                <thead hlmTHead class="bg-muted">
                  <tr hlmTr>
                    <th hlmTh>Lavoro</th><th hlmTh>Ordine</th><th hlmTh>Fase</th><th hlmTh>Inizio – fine</th><th hlmTh>Attrezzaggio</th><th hlmTh>Piano</th>
                  </tr>
                </thead>
                <tbody hlmTBody>
                  @for (q of overview().queue; track q.workOrderId) {
                    <tr hlmTr>
                      <td hlmTd class="font-medium"><a class="underline underline-offset-2" routerLink="/planning" [queryParams]="{ wo: q.workOrderId }">{{ q.workOrderCode }}</a></td>
                      <td hlmTd><a class="underline underline-offset-2" [routerLink]="['/production-orders', q.productionOrderId]">{{ q.productionOrderCode }}</a></td>
                      <td hlmTd>{{ q.processSegmentCode }} <span class="text-muted-foreground">{{ q.processSegmentName }}</span></td>
                      <td hlmTd class="tabular-nums">{{ dt(q.plannedStartAt) }} – {{ dt(q.plannedEndAt) }}</td>
                      <td hlmTd class="tabular-nums">{{ q.plannedSetupMinutes ? q.plannedSetupMinutes + ' min' : '—' }}</td>
                      <td hlmTd>
                        <span class="flex flex-wrap items-center gap-1">
                          <app-plan-status-badge [status]="q.planStatus" [hide]="['Planned']" />
                          @if (q.isPlanLocked) {
                            <span hlmBadge variant="outline" class="gap-1 px-1.5"><ng-icon name="lucideLock" size="12" aria-hidden="true" /> Fissato</span>
                          }
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </section>

      <section class="flex flex-col gap-3 rounded-md border p-4" aria-labelledby="ov-mount">
        <h3 id="ov-mount" class="text-sm font-semibold">Stato di montaggio</h3>
        @if (!overview().mountedState.known) {
          <p class="text-muted-foreground text-sm">Sconosciuto: nessun lavoro precedente</p>
        } @else {
          <ul class="flex flex-wrap gap-1.5" aria-label="Attrezzature e valori montati">
            @for (t of overview().mountedState.tooling; track t.id) {
              <li><span hlmBadge variant="secondary" class="gap-1"><ng-icon name="lucideWrench" size="12" aria-hidden="true" /> {{ t.code }}</span></li>
            }
            @for (v of overview().mountedState.setupValues; track v.attributeCode) {
              <li><span hlmBadge variant="outline">{{ v.attributeName }}: {{ v.value }}</span></li>
            }
          </ul>
          @if (!overview().mountedState.tooling.length && !overview().mountedState.setupValues.length) {
            <p class="text-muted-foreground text-sm">Nessuna attrezzatura o valore montato.</p>
          }
          @if (overview().mountedState.fromWorkOrderCode; as c) {
            <p class="text-muted-foreground text-sm">dal lavoro {{ c }}</p>
          }
        }
      </section>

      <section class="flex flex-col gap-3 rounded-md border p-4" aria-labelledby="ov-alerts">
        <h3 id="ov-alerts" class="text-sm font-semibold">Segnalazioni</h3>
        @if (!overview().alerts.length) {
          <p class="text-muted-foreground text-sm">Nessuna segnalazione</p>
        } @else {
          <ul class="flex flex-col divide-y">
            @for (a of overview().alerts; track $index) {
              <li class="flex flex-col gap-1 py-2 first:pt-0">
                <p class="flex items-start gap-1.5 text-sm font-medium">
                  <ng-icon [name]="a.severity === 'Critical' ? 'lucideCircleAlert' : 'lucideTriangleAlert'" size="16" class="mt-0.5 shrink-0" [class.text-destructive]="a.severity === 'Critical'" aria-hidden="true" />
                  <span><span class="font-semibold">{{ a.severity === 'Critical' ? 'Critica' : 'Avviso' }}:</span> {{ a.message }}</span>
                </p>
                <p class="text-muted-foreground text-sm">{{ a.suggestion }}</p>
                @if (a.workOrderId ?? a.workOrderIds?.[0]; as wo) {
                  <button hlmBtn type="button" variant="link" size="sm" class="h-auto self-start p-0" (click)="show(wo, a.productionOrderId)">Mostra nel piano</button>
                } @else if (a.productionOrderId) {
                  <button hlmBtn type="button" variant="link" size="sm" class="h-auto self-start p-0" (click)="show(null, a.productionOrderId)">Mostra nel piano</button>
                }
              </li>
            }
          </ul>
        }
      </section>
    </div>
  `,
})
export class EquipmentOverviewTabComponent {
  readonly overview = input.required<EquipmentOverviewModel>();
  private readonly planning = inject(PlanningStore);
  private readonly router = inject(Router);
  private readonly calendars = inject(CalendarsStore);

  constructor() {
    void this.calendars.loadTimeZone();
  }

  protected readonly stateIcon = computed(() => STATE_ICON[this.overview().state]);
  protected readonly stateText = computed(() => stateLabel(this.overview().state, this.overview().closedReason));
  protected readonly meters = computed(() => {
    const l = this.overview().load;
    return [
      { label: 'Oggi', value: l.loadToday },
      { label: '7 giorni', value: l.load7 },
      { label: '30 giorni', value: l.load30 },
    ].map((m) => ({ ...m, level: loadLevel(m.value), text: loadText(m.value), width: loadWidth(m.value) }));
  });
  protected readonly elapsed = formatElapsed;
  protected dt(iso: string): string {
    return formatDateTime(iso, this.calendars.timeZone());
  }
  protected show(wo: number | null, order?: number | null): void {
    this.planning.showInPlan(wo, order);
    void this.router.navigate(['/planning'], wo ? { queryParams: { wo } } : {});
  }
}
