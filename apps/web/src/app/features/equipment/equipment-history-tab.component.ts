import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideTriangleAlert } from '@ng-icons/lucide';
import type { EquipmentHistoryModel } from '@micromes/api-client/models';
import { EquipmentService } from '@micromes/api-client/services/equipment';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmPaginationImports } from '@spartan-ng/helm/pagination';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmToggleGroupImports } from '@spartan-ng/helm/toggle-group';
import { firstValueFrom, map } from 'rxjs';
import { CalendarsStore } from '../calendars/calendars.store';
import { toFailure } from '../materials/materials.service';
import { formatDateTime } from '../planning/timeline-time';
import { formatAvailableHours, formatPercent, formatWorked, HISTORY_PAGE_SIZE, HISTORY_PERIODS, parseHistoryDays } from './equipment-history';

/** Scheda «Storico»: riepilogo del periodo, scarti per motivo e sessioni di lavoro svolte sulla macchina. */
@Component({
  selector: 'app-equipment-history-tab',
  imports: [HlmBadge, HlmButton, HlmPaginationImports, HlmSkeleton, HlmTableImports, HlmToggleGroupImports, NgIcon, RouterLink],
  providers: [provideIcons({ lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <hlm-toggle-group type="single" variant="outline" [nullable]="false" [value]="'' + days()" (valueChange)="setDays($any($event))" aria-label="Periodo">
          @for (p of periods; track p) {
            <button hlmToggleGroupItem [value]="'' + p" [attr.aria-label]="'Ultimi ' + p + ' giorni'">{{ p }} giorni</button>
          }
        </hlm-toggle-group>
        <p class="text-muted-foreground text-xs">Utilizzo = minuti lavorati ÷ minuti disponibili del calendario. Non include i fermi.</p>
      </div>

      @if (loading() && !data()) {
        <div class="flex flex-col gap-3" aria-busy="true" aria-label="Caricamento storico">
          <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            @for (i of [1, 2, 3, 4, 5]; track i) { <hlm-skeleton class="h-20 w-full" /> }
          </div>
          <hlm-skeleton class="h-48 w-full" />
        </div>
      } @else if (error()) {
        <div class="flex flex-col items-start gap-2" role="alert">
          <p class="flex items-center gap-1.5 text-sm"><ng-icon name="lucideTriangleAlert" aria-hidden="true" /> Impossibile caricare lo storico: {{ error() }}</p>
          <button hlmBtn size="sm" type="button" (click)="reload()">Riprova</button>
        </div>
      } @else if (data(); as h) {
        <dl class="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <div class="flex flex-col gap-1 rounded-md border p-3">
            <dt class="text-muted-foreground text-xs">Lavori eseguiti</dt>
            <dd class="text-xl font-semibold tabular-nums">{{ h.summary.workOrders }}</dd>
            <dd class="text-muted-foreground text-xs">{{ h.summary.completedWorkOrders }} completati</dd>
          </div>
          <div class="flex flex-col gap-1 rounded-md border p-3">
            <dt class="text-muted-foreground text-xs">Minuti lavorati</dt>
            <dd class="text-xl font-semibold tabular-nums">{{ worked(h.summary.workedMinutes) }}</dd>
          </div>
          <div class="flex flex-col gap-1 rounded-md border p-3">
            <dt class="text-muted-foreground text-xs">Utilizzo</dt>
            <dd class="text-xl font-semibold tabular-nums">{{ pct(h.summary.utilizationPercent) }}</dd>
            <dd class="text-muted-foreground text-xs">su {{ hours(h.summary.availableMinutes) }} disponibili</dd>
          </div>
          <div class="flex flex-col gap-1 rounded-md border p-3">
            <dt class="text-muted-foreground text-xs">Buoni</dt>
            <dd class="text-xl font-semibold tabular-nums">{{ h.summary.goodQuantity }}</dd>
          </div>
          <div class="flex flex-col gap-1 rounded-md border p-3">
            <dt class="text-muted-foreground text-xs">Scarti</dt>
            <dd class="text-xl font-semibold tabular-nums">{{ h.summary.scrapQuantity }}</dd>
            <dd class="text-muted-foreground text-xs">{{ pct(h.summary.scrapPercent) }} della produzione</dd>
          </div>
        </dl>

        <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="hs-scrap">
          <h3 id="hs-scrap" class="text-sm font-semibold">Scarti per motivo</h3>
          @if (!h.scrapByReason.length) {
            <p class="text-muted-foreground text-sm">Nessuno scarto nel periodo</p>
          } @else {
            <ul class="flex flex-col gap-1 text-sm">
              @for (r of h.scrapByReason; track $index) {
                <li class="flex items-center justify-between gap-3">
                  <span>@if (r.code === '-') { {{ r.name }} } @else { <span class="font-medium">{{ r.code }}</span> <span class="text-muted-foreground">{{ r.name }}</span> }</span>
                  <span class="font-medium tabular-nums">{{ r.quantity }}</span>
                </li>
              }
            </ul>
          }
        </section>

        <section class="flex flex-col gap-2" aria-labelledby="hs-sessions" [attr.aria-busy]="loading()">
          <h3 id="hs-sessions" class="text-sm font-semibold">Sessioni di lavoro</h3>
          @if (!h.sessions.items.length) {
            <p class="text-muted-foreground rounded-md border p-4 text-sm">Nessuna sessione di lavoro nel periodo.</p>
          } @else {
            <div class="overflow-auto rounded-md border" [class.opacity-60]="loading()">
              <div hlmTableContainer>
                <table hlmTable>
                  <caption class="sr-only">Sessioni di lavoro sulla macchina, dalla più recente</caption>
                  <thead hlmTHead class="bg-muted">
                    <tr hlmTr>
                      <th hlmTh>Lavoro</th><th hlmTh>Ordine</th><th hlmTh>Fase</th><th hlmTh>Inizio – fine</th>
                      <th hlmTh class="text-right">Durata</th><th hlmTh class="text-right">Buoni</th><th hlmTh class="text-right">Scarti</th>
                    </tr>
                  </thead>
                  <tbody hlmTBody>
                    @for (s of h.sessions.items; track s.sessionId) {
                      <tr hlmTr>
                        <td hlmTd class="font-medium"><a class="underline underline-offset-2" routerLink="/planning" [queryParams]="{ wo: s.workOrderId }">{{ s.workOrderCode }}</a></td>
                        <td hlmTd><a class="underline underline-offset-2" [routerLink]="['/production-orders', s.productionOrderId]">{{ s.productionOrderCode }}</a></td>
                        <td hlmTd>{{ s.processSegmentName }}</td>
                        <td hlmTd class="tabular-nums">
                          {{ dt(s.startedAt) }} –
                          @if (s.isRunning) { <span hlmBadge variant="secondary" class="px-1.5">In corso</span> } @else { {{ dt(s.endedAt) }} }
                        </td>
                        <td hlmTd class="text-right tabular-nums">{{ worked(s.durationMinutes) }}</td>
                        <td hlmTd class="text-right tabular-nums">{{ s.goodQuantity }}</td>
                        <td hlmTd class="text-right tabular-nums">{{ s.scrapQuantity }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
            @if (pageCount() > 1) {
              <nav hlmPagination aria-label="Paginazione sessioni" class="justify-between">
                <p class="text-muted-foreground text-sm">Pagina {{ page() }} di {{ pageCount() }} · {{ h.sessions.totalCount }} sessioni</p>
                <ul hlmPaginationContent>
                  <li hlmPaginationItem>
                    @if (page() > 1) {
                      <hlm-pagination-previous [link]="[]" [queryParams]="{ hpage: page() > 2 ? page() - 1 : null }" queryParamsHandling="merge" text="Precedente" aria-label="Pagina precedente" />
                    }
                  </li>
                  <li hlmPaginationItem>
                    @if (page() < pageCount()) {
                      <hlm-pagination-next [link]="[]" [queryParams]="{ hpage: page() + 1 }" queryParamsHandling="merge" text="Successiva" aria-label="Pagina successiva" />
                    }
                  </li>
                </ul>
              </nav>
            }
          }
        </section>
      }
    </div>
  `,
})
export class EquipmentHistoryTabComponent {
  readonly equipmentId = input.required<number>();
  private readonly api = inject(EquipmentService);
  private readonly calendars = inject(CalendarsStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly periods = HISTORY_PERIODS;
  protected readonly days = toSignal(this.route.queryParamMap.pipe(map((p) => parseHistoryDays(p.get('days')))), { initialValue: parseHistoryDays(this.route.snapshot.queryParamMap.get('days')) });
  protected readonly page = toSignal(this.route.queryParamMap.pipe(map((p) => Math.max(1, Math.floor(Number(p.get('hpage'))) || 1))), { initialValue: 1 });

  protected readonly data = signal<EquipmentHistoryModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly tick = signal(0);
  private seq = 0;

  protected readonly pageCount = computed(() => Math.max(1, Math.ceil((this.data()?.sessions.totalCount ?? 0) / HISTORY_PAGE_SIZE)));
  protected readonly worked = formatWorked;
  protected readonly pct = formatPercent;
  protected readonly hours = formatAvailableHours;

  constructor() {
    void this.calendars.loadTimeZone();
    effect(() => {
      const id = this.equipmentId();
      const days = this.days();
      const page = this.page();
      this.tick();
      void this.fetch(id, days, page);
    });
  }

  protected dt(iso: string | null | undefined): string {
    return iso ? formatDateTime(iso, this.calendars.timeZone()) : '—';
  }

  protected reload(): void {
    this.tick.update((n) => n + 1);
  }

  protected setDays(v: string | readonly string[] | null): void {
    const d = parseHistoryDays(Array.isArray(v) ? v[0] : (v as string | null));
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { days: d === 30 ? null : d, hpage: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private async fetch(id: number, days: number, page: number): Promise<void> {
    const n = ++this.seq;
    this.loading.set(true);
    this.error.set(null);
    try {
      const r = await firstValueFrom(this.api.getEquipmentHistory(id, { days, page, pageSize: HISTORY_PAGE_SIZE }));
      if (n === this.seq) this.data.set(r);
    } catch (e) {
      if (n === this.seq) this.error.set(toFailure(e).message);
    } finally {
      if (n === this.seq) this.loading.set(false);
    }
  }
}
