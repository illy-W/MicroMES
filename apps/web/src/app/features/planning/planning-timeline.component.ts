import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, ElementRef, inject, signal, untracked, viewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideAlarmClock,
  lucideChevronLeft,
  lucideChevronRight,
  lucideCircleAlert,
  lucideCircleCheck,
  lucideCircleDot,
  lucideClock,
  lucideOctagonX,
  lucidePause,
  lucidePlay,
  lucideRefreshCw,
  lucideSearch,
  lucideTriangleAlert,
} from '@ng-icons/lucide';
import { WorkOrderStatus } from '@micromes/api-client/models';
import type { TimelineLoadModel, TimelineModel, TimelineNonWorkingModel, TimelineWorkOrderModel } from '@micromes/api-client/models';
import { PlanningService } from '@micromes/api-client/services/planning';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';
import { WORK_STATUS, workStatus } from '../production-orders/production-order-labels';
import { PlanningWorkPanelComponent } from './planning-work-panel.component';
import {
  addDays, addMonths, dayKey, daysInMonth, dayStart, formatDateTime, formatDay, formatMonthTitle, formatWeekday, HOUR, hourLabel, mondayOf, monthStart,
} from './timeline-time';

type Zoom = 'day' | 'week' | 'month';
const ZOOMS: { value: Zoom; label: string }[] = [
  { value: 'day', label: 'Giorno' },
  { value: 'week', label: 'Settimana' },
  { value: 'month', label: 'Mese' },
];
/** Pixel per ora per ogni zoom. */
const PPH: Record<Zoom, number> = { day: 48, week: 8, month: 2 };
const LEFT = 224;
const LANE_H = 28;
const STRIP_H = 10;
const MIN_W = 22;
const ALL = 'all';
const NONE_ROW = -1;
const ACCENTS = ['border-l-primary', 'border-l-ring', 'border-l-muted-foreground', 'border-l-foreground'];

interface BarVm {
  w: TimelineWorkOrderModel;
  left: number;
  width: number;
  lane: number;
  rowKey: number;
  overload: boolean;
  dim: boolean;
  late: boolean;
  stale: boolean;
  statusIcon: string;
  tone: string;
  text: string;
  aria: string;
  cls: string;
}
interface BandVm { left: number; width: number; reason: string; cls: string }
interface StripVm { left: number; width: number; pct: number; fill: number; over: boolean; title: string }
interface RowVm {
  key: number;
  code: string;
  name: string;
  capacity: number;
  inactive: boolean;
  height: number;
  bars: BarVm[];
  bands: BandVm[];
  strips: StripVm[];
  summary: number | null;
}
interface Tick { left: number; label: string; major: boolean }

const bootstrapTz = (): string => Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';

/** Timeline di pianificazione a griglia CSS (nessuna libreria), con pannello di dettaglio e filtri. */
@Component({
  selector: 'app-planning-timeline',
  imports: [HlmButton, HlmEmptyImports, HlmInput, HlmSelectImports, HlmSkeleton, NgIcon, PlanningWorkPanelComponent],
  providers: [
    provideIcons({
      lucideAlarmClock, lucideChevronLeft, lucideChevronRight, lucideCircleAlert, lucideCircleCheck, lucideCircleDot, lucideClock,
      lucideOctagonX, lucidePause, lucidePlay, lucideRefreshCw, lucideSearch, lucideTriangleAlert,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-card text-card-foreground flex flex-col gap-3 rounded-xl border p-3" aria-labelledby="timeline-title">
      <div class="flex flex-wrap items-center gap-2">
        <h2 id="timeline-title" class="mr-2 text-base font-semibold">Timeline</h2>
        <div class="flex gap-1" role="group" aria-label="Zoom">
          @for (z of zooms; track z.value) {
            <button hlmBtn type="button" size="sm" [variant]="zoom() === z.value ? 'default' : 'outline'" [attr.aria-pressed]="zoom() === z.value" (click)="setZoom(z.value)">{{ z.label }}</button>
          }
        </div>
        <div class="flex items-center gap-1">
          <button hlmBtn type="button" size="icon" variant="outline" aria-label="Periodo precedente" (click)="shift(-1)"><ng-icon name="lucideChevronLeft" aria-hidden="true" /></button>
          <button hlmBtn type="button" size="sm" variant="outline" (click)="today()">Oggi</button>
          <button hlmBtn type="button" size="icon" variant="outline" aria-label="Periodo successivo" (click)="shift(1)"><ng-icon name="lucideChevronRight" aria-hidden="true" /></button>
        </div>
        <p class="text-sm font-medium" aria-live="polite">{{ title() }}</p>
        <button hlmBtn type="button" size="icon" variant="ghost" aria-label="Ricarica timeline" (click)="reload()"><ng-icon name="lucideRefreshCw" aria-hidden="true" /></button>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <div class="relative">
          <ng-icon name="lucideSearch" class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2" aria-hidden="true" />
          <input hlmInput type="search" class="h-8 w-52 pl-8" placeholder="Cerca codice lavoro o ordine" aria-label="Cerca per codice" [value]="search()" (input)="search.set($any($event.target).value)" />
        </div>
        <hlm-select [value]="orderFilter()" [itemToString]="orderLabel" (valueChange)="orderFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-44" aria-label="Filtra per ordine"><hlm-select-value /></hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="all">Tutti gli ordini</hlm-select-item>
            @for (o of orderOptions(); track o.id) { <hlm-select-item [value]="o.id">{{ o.code }}</hlm-select-item> }
          </hlm-select-content>
        </hlm-select>
        <hlm-select [value]="statusFilter()" [itemToString]="statusLabel" (valueChange)="statusFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-40" aria-label="Filtra per stato"><hlm-select-value /></hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="all">Tutti gli stati</hlm-select-item>
            @for (s of statuses; track s.value) { <hlm-select-item [value]="s.value">{{ s.label }}</hlm-select-item> }
          </hlm-select-content>
        </hlm-select>
        <hlm-select [value]="machineFilter()" [itemToString]="machineLabel" (valueChange)="machineFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-44" aria-label="Filtra per macchina"><hlm-select-value /></hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="all">Tutte le macchine</hlm-select-item>
            @for (m of machineOptions(); track m.id) { <hlm-select-item [value]="m.id">{{ m.label }}</hlm-select-item> }
          </hlm-select-content>
        </hlm-select>
        @if (filtersActive()) {
          <button hlmBtn type="button" size="sm" variant="ghost" (click)="clearFilters()">Azzera filtri</button>
        }
        @if (search().trim()) {
          <span class="text-muted-foreground text-sm" role="status">{{ matchCount() }} lavori corrispondenti</span>
        }
      </div>

      @if (error()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare la timeline</div>
            <div hlmEmptyDescription>{{ error() }}</div>
          </hlm-empty-header>
          <hlm-empty-content><button hlmBtn type="button" (click)="reload()">Riprova</button></hlm-empty-content>
        </hlm-empty>
      } @else if (!data()) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento timeline">
          @for (i of [1, 2, 3, 4]; track i) { <hlm-skeleton class="h-12 w-full" /> }
        </div>
      } @else {
        <div class="flex flex-col gap-3 lg:flex-row" [attr.aria-busy]="loading()">
          <div class="min-w-0 flex-1">
            <div
              #scroller
              role="region"
              aria-label="Timeline delle macchine"
              class="relative max-h-[560px] overflow-auto rounded-md border"
              [class.opacity-70]="loading()"
              (scroll)="onScroll()"
            >
              <div class="relative" [style.width.px]="LEFT + totalPx()">
                <div class="bg-card sticky top-0 z-20 flex border-b">
                  <div class="bg-card sticky left-0 z-10 flex shrink-0 items-center border-r px-2 text-xs font-medium" [style.width.px]="LEFT">Macchina</div>
                  <div class="relative h-9" [style.width.px]="totalPx()">
                    @for (t of ticks(); track t.left) {
                      <span class="absolute top-0 flex h-full items-center border-l pl-1 text-xs" [class.font-medium]="t.major" [class.text-muted-foreground]="!t.major" [style.left.px]="t.left">{{ t.label }}</span>
                    }
                  </div>
                </div>

                @if (nowPx(); as np) {
                  <div class="bg-primary pointer-events-none absolute top-9 bottom-0 z-[5] w-0.5" [style.left.px]="LEFT + np" aria-hidden="true" title="Adesso"></div>
                }

                @for (r of rows(); track r.key) {
                  <div class="flex border-b" role="group" [attr.aria-label]="'Macchina ' + r.code" [attr.data-row]="r.key">
                    <div class="bg-card sticky left-0 z-10 flex shrink-0 flex-col justify-center gap-0.5 border-r px-2 py-1 text-xs" [style.width.px]="LEFT">
                      <div class="flex items-center gap-1">
                        <span class="truncate font-medium">{{ r.code }}</span>
                        @if (r.capacity > 1) { <span class="bg-secondary text-secondary-foreground rounded px-1" [title]="'Capacità ' + r.capacity">×{{ r.capacity }}</span> }
                        @if (r.inactive) { <span class="text-muted-foreground">(non attiva)</span> }
                      </div>
                      <span class="text-muted-foreground truncate">{{ r.name }}</span>
                      @if (r.summary !== null) {
                        <span class="flex items-center gap-1" [class.text-destructive]="r.summary > 100">
                          @if (r.summary > 100) { <ng-icon name="lucideCircleAlert" size="12" aria-hidden="true" /> }
                          Carico {{ r.summary }}%{{ r.summary > 100 ? ' (sovraccarico)' : '' }}
                        </span>
                      }
                    </div>
                    <div class="relative shrink-0" [style.width.px]="totalPx()" [style.height.px]="r.height" [style.background-image]="'linear-gradient(to right, var(--border) 1px, transparent 1px)'" [style.background-size.px]="dayPx()">
                      @for (b of r.bands; track $index) {
                        <div class="absolute top-0" [class]="b.cls" [style.left.px]="b.left" [style.width.px]="b.width" [style.height.px]="r.height - STRIP_H" [title]="b.reason" aria-hidden="true"></div>
                      }
                      @for (b of r.bars; track b.w.id) {
                        <button
                          type="button"
                          class="absolute"
                          [class]="b.cls"
                          [style.left.px]="b.left"
                          [style.width.px]="b.width"
                          [style.top.px]="b.lane * LANE_H + 2"
                          [style.height.px]="LANE_H - 4"
                          [attr.data-bar]="b.w.id"
                          [attr.aria-label]="b.aria"
                          [attr.aria-pressed]="selectedId() === b.w.id"
                          [attr.tabindex]="b.w.id === tabStop() ? 0 : -1"
                          [title]="b.aria"
                          (click)="select(b.w)"
                          (focus)="focusId.set(b.w.id)"
                          (keydown)="onKey($event, b)"
                        >
                          <ng-icon [name]="b.statusIcon" size="14" [class]="b.tone" class="shrink-0" aria-hidden="true" />
                          @if (b.late) { <ng-icon name="lucideAlarmClock" size="14" class="text-destructive shrink-0" aria-hidden="true" /> }
                          @if (b.stale) { <ng-icon name="lucideTriangleAlert" size="14" class="shrink-0" aria-hidden="true" /> }
                          @if (b.overload) { <ng-icon name="lucideCircleAlert" size="14" class="text-destructive shrink-0" aria-hidden="true" /> }
                          @if (b.width >= 70) { <span class="truncate" [class]="b.tone">{{ b.text }}</span> }
                        </button>
                      }
                      <div class="absolute bottom-0 left-0 w-full" [style.height.px]="STRIP_H">
                        @for (s of r.strips; track $index) {
                          <div class="absolute bottom-0 flex items-end" [style.left.px]="s.left" [style.width.px]="s.width" [style.height.px]="STRIP_H" [title]="s.title">
                            <div class="w-full" [class]="s.over ? 'bg-destructive' : 'bg-primary/40'" [style.height.%]="s.fill"></div>
                          </div>
                        }
                      </div>
                    </div>
                  </div>
                }
              </div>
            </div>
            <p class="text-muted-foreground mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
              @for (s of legend; track s.label) {
                <span class="inline-flex items-center gap-1"><ng-icon [name]="s.icon" size="12" aria-hidden="true" /> {{ s.label }}</span>
              }
              <span class="inline-flex items-center gap-1"><ng-icon name="lucideAlarmClock" size="12" aria-hidden="true" /> in ritardo</span>
              <span class="inline-flex items-center gap-1"><ng-icon name="lucideTriangleAlert" size="12" aria-hidden="true" /> piano obsoleto</span>
              <span class="inline-flex items-center gap-1"><ng-icon name="lucideCircleAlert" size="12" aria-hidden="true" /> sovraccarico</span>
              <span class="inline-flex items-center gap-1"><span class="bg-muted inline-block h-3 w-4 border" aria-hidden="true"></span> non lavorativo (a righe: eccezione)</span>
              @if (undated() > 0) { <span>· {{ undated() }} lavori senza data non mostrati</span> }
            </p>
            @if (!data()!.workOrders.length && !loading()) {
              <p class="text-muted-foreground mt-2 text-sm" role="status">Nessun lavoro pianificato nel periodo con questi filtri.</p>
            }
          </div>

          @if (selected(); as sel) {
            <div class="w-full shrink-0 lg:w-80">
              <app-planning-work-panel [item]="sel" [timeZone]="tz()" [machineLabels]="machineLabels()" (closed)="selectedId.set(null)" (changed)="reload()" />
            </div>
          }
        </div>
      }
    </section>
  `,
})
export class PlanningTimelineComponent {
  private readonly api = inject(PlanningService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');

  protected readonly LEFT = LEFT;
  protected readonly LANE_H = LANE_H;
  protected readonly STRIP_H = STRIP_H;
  protected readonly zooms = ZOOMS;
  protected readonly statuses = (Object.keys(WORK_STATUS) as WorkOrderStatus[]).map((value) => ({ value, label: WORK_STATUS[value].label }));
  protected readonly legend = (Object.keys(WORK_STATUS) as WorkOrderStatus[]).map((s) => ({ icon: WORK_STATUS[s].icon, label: WORK_STATUS[s].label }));

  // --- stato (inizializzato dai query params) ---
  private readonly qp = this.route.snapshot.queryParamMap;
  protected readonly zoom = signal<Zoom>((['day', 'week', 'month'] as const).find((z) => z === this.qp.get('zoom')) ?? 'week');
  protected readonly cursor = signal<string>(/^\d{4}-\d{2}-\d{2}$/.test(this.qp.get('from') ?? '') ? (this.qp.get('from') as string) : dayKey(Date.now(), bootstrapTz()));
  protected readonly orderFilter = signal(this.qp.get('order') ?? ALL);
  protected readonly statusFilter = signal(this.qp.get('status') ?? ALL);
  protected readonly machineFilter = signal(this.qp.get('machine') ?? ALL);
  protected readonly search = signal(this.qp.get('q') ?? '');
  protected readonly selectedId = signal<number | null>(null);
  protected readonly focusId = signal<number | null>(null);

  protected readonly data = signal<TimelineModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly knownOrders = signal<ReadonlyMap<number, string>>(new Map());
  private readonly lastSelected = signal<TimelineWorkOrderModel | null>(null);
  private readonly now = signal(Date.now());
  private readonly vp = signal({ from: 0, to: 1600 });

  protected readonly tz = computed(() => this.data()?.timeZoneId ?? bootstrapTz());
  private readonly pph = computed(() => PPH[this.zoom()]);

  /** Chiave del primo giorno e numero di giorni della finestra visibile. */
  private readonly span = computed(() => {
    const z = this.zoom();
    const c = this.cursor();
    if (z === 'day') return { start: c, days: 1 };
    if (z === 'week') return { start: mondayOf(c), days: 7 };
    const s = monthStart(c);
    return { start: s, days: daysInMonth(s) };
  });
  protected readonly win = computed(() => {
    const { start, days } = this.span();
    const tz = this.tz();
    return { start: dayStart(start, tz), end: dayStart(addDays(start, days), tz) };
  });
  protected readonly totalPx = computed(() => ((this.win().end - this.win().start) / HOUR) * this.pph());
  protected readonly dayPx = computed(() => 24 * this.pph());
  protected readonly nowPx = computed(() => {
    const n = this.now();
    const w = this.win();
    return n >= w.start && n <= w.end ? ((n - w.start) / HOUR) * this.pph() : null;
  });

  protected readonly title = computed(() => {
    const w = this.win();
    const tz = this.tz();
    const z = this.zoom();
    if (z === 'month') return formatMonthTitle(w.start, tz);
    if (z === 'day') return `${formatWeekday(w.start, tz)} ${formatDay(w.start, tz, true).replace(/^\d+\s/, '')}`;
    return `${formatDay(w.start, tz)} – ${formatDay(w.end - 1, tz, true)}`;
  });

  protected readonly ticks = computed<Tick[]>(() => {
    const { start, days } = this.span();
    const tz = this.tz();
    const w = this.win();
    const z = this.zoom();
    const out: Tick[] = [];
    const px = (t: number) => ((t - w.start) / HOUR) * this.pph();
    for (let i = 0; i < days; i++) {
      const key = addDays(start, i);
      const ds = dayStart(key, tz);
      if (z === 'day') {
        for (let h = 0; h < 24; h++) out.push({ left: px(ds + h * HOUR), label: hourLabel(ds + h * HOUR, tz), major: h % 6 === 0 });
      } else if (z === 'week') {
        out.push({ left: px(ds), label: formatWeekday(ds, tz), major: true });
        for (const h of [6, 12, 18]) out.push({ left: px(ds + h * HOUR), label: String(h).padStart(2, '0'), major: false });
      } else {
        out.push({ left: px(ds), label: String(Number(key.slice(8))), major: true });
      }
    }
    return out;
  });

  // --- opzioni filtri ---
  protected readonly orderOptions = computed(() =>
    [...this.knownOrders()].map(([id, code]) => ({ id: String(id), code })).sort((a, b) => a.code.localeCompare(b.code)),
  );
  protected readonly machineOptions = computed(() => (this.data()?.equipment ?? []).map((e) => ({ id: String(e.id), label: `${e.code} · ${e.name}` })));
  protected readonly machineLabels = computed<ReadonlyMap<number, string>>(() => new Map((this.data()?.equipment ?? []).map((e) => [e.id, `${e.code} · ${e.name}`])));
  protected readonly orderLabel = (v: string): string => (v === ALL ? 'Tutti gli ordini' : (this.knownOrders().get(Number(v)) ?? `Ordine ${v}`));
  protected readonly statusLabel = (v: string): string => (v === ALL ? 'Tutti gli stati' : workStatus(v).label);
  protected readonly machineLabel = (v: string): string => (v === ALL ? 'Tutte le macchine' : (this.machineLabels().get(Number(v)) ?? v));
  protected readonly filtersActive = computed(() => this.orderFilter() !== ALL || this.statusFilter() !== ALL || this.machineFilter() !== ALL || !!this.search().trim());

  // --- modello righe ---
  private readonly matches = (w: TimelineWorkOrderModel, q: string): boolean => !q || w.code.toLowerCase().includes(q) || w.productionOrderCode.toLowerCase().includes(q);
  protected readonly matchCount = computed(() => {
    const q = this.search().trim().toLowerCase();
    return (this.data()?.workOrders ?? []).filter((w) => this.matches(w, q)).length;
  });
  protected readonly undated = computed(() => (this.data()?.workOrders ?? []).filter((w) => !this.range(w)).length);

  private range(w: TimelineWorkOrderModel): { s: number; e: number } | null {
    const running = w.status === 'Running' || w.status === 'Ended';
    const s0 = (running && w.actualStartAt) || w.plannedStartAt;
    if (!s0) return null;
    const s = new Date(s0).getTime();
    const e0 = (w.status === 'Ended' && w.actualEndAt) || w.plannedEndAt || w.actualEndAt;
    const e = e0 ? new Date(e0).getTime() : s + 30 * 60_000;
    return { s, e: Math.max(e, s + 60_000) };
  }

  protected readonly allRows = computed<RowVm[]>(() => {
    const d = this.data();
    if (!d) return [];
    const tz = d.timeZoneId;
    const w = this.win();
    const pph = this.pph();
    const q = this.search().trim().toLowerCase();
    const mf = this.machineFilter();
    const px = (t: number) => ((t - w.start) / HOUR) * pph;
    const rowSpec = d.equipment
      .filter((e) => mf === ALL || String(e.id) === mf)
      .map((e) => ({ key: e.id, code: e.code, name: e.name, capacity: e.capacity, inactive: !e.isActive }));
    if (mf === ALL) rowSpec.push({ key: NONE_ROW, code: 'Senza macchina', name: 'Lavori non assegnati', capacity: 1, inactive: false });

    const byRow = new Map<number, TimelineWorkOrderModel[]>();
    for (const wo of d.workOrders) {
      const k = wo.equipmentId ?? NONE_ROW;
      const list = byRow.get(k) ?? [];
      list.push(wo);
      byRow.set(k, list);
    }
    const nw = d.nonWorking.filter((n) => new Date(n.end).getTime() > w.start && new Date(n.start).getTime() < w.end);
    const loads = new Map<number, TimelineLoadModel[]>();
    for (const l of d.load) {
      const list = loads.get(l.equipmentId) ?? [];
      list.push(l);
      loads.set(l.equipmentId, list);
    }

    return rowSpec.map((r) => {
      const items = (byRow.get(r.key) ?? []).filter((x) => this.range(x));
      const lanes = Math.max(1, ...items.map((x) => x.lane + 1));
      const bars = items
        .map((x): BarVm => {
          const rg = this.range(x) as { s: number; e: number };
          const left = px(rg.s);
          const width = Math.max(MIN_W, ((rg.e - rg.s) / HOUR) * pph);
          const st = workStatus(x.status);
          const overload = r.key !== NONE_ROW && x.lane >= r.capacity;
          const late = x.isLate;
          const stale = x.planStatus === 'Stale';
          const dim = !!q && !this.matches(x, q);
          const eqName = r.key === NONE_ROW ? 'nessuna macchina' : r.code;
          const aria = `${x.processSegmentName} ${x.code} su ${eqName}, dal ${formatDateTime(rg.s, tz)} al ${formatDateTime(rg.e, tz)}, ${st.label.toLowerCase()}${late ? ', in ritardo' : ''}${stale ? ', piano obsoleto' : ''}${overload ? ', sovraccarico' : ''}`;
          const accent = overload ? 'border-l-destructive' : ACCENTS[Math.abs(x.productionOrderId) % ACCENTS.length];
          const cls = `flex items-center gap-1 overflow-hidden rounded-sm border border-l-4 px-1 text-xs whitespace-nowrap hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none ${accent} ${overload ? 'bg-destructive/10 border-destructive' : 'bg-card'} ${x.status === 'Running' ? 'font-semibold' : ''} ${dim ? 'opacity-30' : ''} ${this.selectedId() === x.id ? 'ring-2 ring-primary' : ''}`;
          return { w: x, left, width, lane: x.lane, rowKey: r.key, overload, dim, late, stale, statusIcon: st.icon, tone: st.tone, text: `${x.productionOrderCode} · ${x.processSegmentName}`, aria, cls };
        })
        .sort((a, b) => a.left - b.left);
      const bands = r.key === NONE_ROW ? [] : nw.filter((n) => n.equipmentId == null || n.equipmentId === r.key).map((n) => this.band(n, px, w, pph));
      const rl = (loads.get(r.key) ?? []).filter((l) => new Date(l.periodEnd).getTime() > w.start && new Date(l.periodStart).getTime() < w.end);
      const strips = rl.map((l): StripVm => {
        const s = Math.max(new Date(l.periodStart).getTime(), w.start);
        const e = Math.min(new Date(l.periodEnd).getTime(), w.end);
        const pct = Math.round(l.loadPercent);
        return { left: px(s), width: Math.max(2, px(e) - px(s) - 1), pct, fill: Math.min(100, Math.max(6, pct)), over: pct > 100, title: `Carico ${pct}%${pct > 100 ? ' (sovraccarico)' : ''} · ${l.busyMinutes}/${l.capacityMinutes} min` };
      });
      const cap = rl.reduce((a, l) => a + l.capacityMinutes, 0);
      const busy = rl.reduce((a, l) => a + l.busyMinutes, 0);
      const summary = r.key === NONE_ROW || !cap ? null : Math.round((busy / cap) * 100);
      return { ...r, height: lanes * LANE_H + STRIP_H + 4, bars, bands, strips, summary };
    });
  });

  private band(n: TimelineNonWorkingModel, px: (t: number) => number, w: { start: number; end: number }, pph: number): BandVm {
    const s = Math.max(new Date(n.start).getTime(), w.start);
    const e = Math.min(new Date(n.end).getTime(), w.end);
    const hatch = 'bg-[repeating-linear-gradient(45deg,transparent,transparent_4px,var(--border)_4px,var(--border)_5px)]';
    const cls = n.kind === 'Weekly' ? 'bg-muted/60' : n.kind === 'CalendarException' ? `bg-muted/40 ${hatch}` : `bg-muted ${hatch} border-x`;
    const label = n.kind === 'Weekly' ? 'Non lavorativo' : n.kind === 'CalendarException' ? 'Eccezione calendario' : 'Eccezione macchina';
    void pph;
    return { left: px(s), width: Math.max(1, px(e) - px(s)), reason: n.reason ? `${label}: ${n.reason}` : label, cls };
  }

  /** Righe con soli i lavori che intersecano il viewport orizzontale (virtualizzazione). */
  protected readonly rows = computed<RowVm[]>(() => {
    const { from, to } = this.vp();
    const keep = this.selectedId();
    return this.allRows().map((r) => ({ ...r, bars: r.bars.filter((b) => (b.left + b.width >= from && b.left <= to) || b.w.id === this.focusId() || b.w.id === keep) }));
  });

  protected readonly tabStop = computed(() => {
    const f = this.focusId();
    if (f != null && this.allRows().some((r) => r.bars.some((b) => b.w.id === f))) return f;
    return this.allRows().find((r) => r.bars.length)?.bars[0].w.id ?? null;
  });

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    if (id == null) return null;
    return this.data()?.workOrders.find((w) => w.id === id) ?? this.lastSelected();
  });

  // --- caricamento ---
  private seq = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private first = true;

  constructor() {
    const tick = setInterval(() => this.now.set(Date.now()), 60_000);
    this.destroyRef.onDestroy(() => {
      clearInterval(tick);
      clearTimeout(this.timer);
    });

    effect(() => {
      const w = this.win();
      const q = { order: this.orderFilter(), status: this.statusFilter() };
      untracked(() => {
        clearTimeout(this.timer);
        this.timer = setTimeout(() => void this.load(w.start, w.end, q), this.first ? 0 : 250);
        this.first = false;
      });
    });

    effect(() => {
      const z = this.zoom();
      const params = {
        zoom: z === 'week' ? null : z,
        from: this.cursor() === dayKey(Date.now(), this.tz()) ? null : this.cursor(),
        order: this.orderFilter() === ALL ? null : this.orderFilter(),
        status: this.statusFilter() === ALL ? null : this.statusFilter(),
        machine: this.machineFilter() === ALL ? null : this.machineFilter(),
        q: this.search().trim() || null,
      };
      untracked(() => void this.router.navigate([], { relativeTo: this.route, queryParams: params, queryParamsHandling: 'merge', replaceUrl: true }));
    });

    // Posizione iniziale dello scroll quando cambia la finestra (Giorno: 06:00; altrimenti "adesso" se visibile).
    effect(() => {
      this.win();
      const el = this.scroller()?.nativeElement;
      if (!el) return;
      const z = untracked(this.zoom);
      const np = untracked(this.nowPx);
      setTimeout(() => {
        el.scrollLeft = z === 'day' ? 6 * PPH.day : np != null ? Math.max(0, np - 120) : 0;
        this.onScroll();
      }, 0);
    });
  }

  protected onScroll(): void {
    const el = this.scroller()?.nativeElement;
    if (!el) return;
    const bucket = 300;
    const from = Math.floor(Math.max(0, el.scrollLeft - 400) / bucket) * bucket;
    const to = Math.ceil((el.scrollLeft + el.clientWidth + 400) / bucket) * bucket;
    const cur = this.vp();
    if (cur.from !== from || cur.to !== to) this.vp.set({ from, to });
  }

  protected reload(): void {
    const w = this.win();
    void this.load(w.start, w.end, { order: this.orderFilter(), status: this.statusFilter() });
  }

  private async load(start: number, end: number, q: { order: string; status: string }): Promise<void> {
    const n = ++this.seq;
    this.loading.set(true);
    this.error.set(null);
    try {
      const d = await firstValueFrom(
        this.api.getPlanningTimeline({
          from: new Date(start - 24 * HOUR).toISOString(),
          to: new Date(end + 24 * HOUR).toISOString(),
          ...(q.order !== ALL ? { productionOrderId: Number(q.order) } : {}),
          ...(q.status !== ALL ? { status: q.status as WorkOrderStatus } : {}),
        }),
      );
      if (n !== this.seq) return;
      this.data.set(d);
      const known = new Map(this.knownOrders());
      for (const w of d.workOrders) known.set(w.productionOrderId, w.productionOrderCode);
      this.knownOrders.set(known);
      const sel = this.selectedId();
      const found = sel != null ? d.workOrders.find((w) => w.id === sel) : null;
      if (found) this.lastSelected.set(found);
      this.onScroll();
    } catch (e) {
      if (n === this.seq) this.error.set(toFailure(e).message);
    } finally {
      if (n === this.seq) this.loading.set(false);
    }
  }

  // --- navigazione ---
  protected setZoom(z: Zoom): void {
    this.zoom.set(z);
  }
  protected shift(dir: 1 | -1): void {
    const z = this.zoom();
    const s = this.span().start;
    this.cursor.set(z === 'day' ? addDays(s, dir) : z === 'week' ? addDays(s, 7 * dir) : addMonths(s, dir));
  }
  protected today(): void {
    this.cursor.set(dayKey(Date.now(), this.tz()));
  }
  protected clearFilters(): void {
    this.orderFilter.set(ALL);
    this.statusFilter.set(ALL);
    this.machineFilter.set(ALL);
    this.search.set('');
  }
  protected select(w: TimelineWorkOrderModel): void {
    this.lastSelected.set(w);
    this.selectedId.set(w.id);
  }

  // --- tastiera ---
  protected onKey(ev: KeyboardEvent, b: BarVm): void {
    const rows = this.allRows().filter((r) => r.bars.length);
    const ri = rows.findIndex((r) => r.key === b.rowKey);
    if (ri < 0) return;
    let target: BarVm | undefined;
    if (ev.key === 'ArrowRight' || ev.key === 'ArrowLeft') {
      const bars = rows[ri].bars;
      target = bars[bars.findIndex((x) => x.w.id === b.w.id) + (ev.key === 'ArrowRight' ? 1 : -1)];
    } else if (ev.key === 'ArrowDown' || ev.key === 'ArrowUp') {
      const next = rows[ri + (ev.key === 'ArrowDown' ? 1 : -1)];
      target = next?.bars.reduce<BarVm | undefined>((best, x) => (!best || Math.abs(x.left - b.left) < Math.abs(best.left - b.left) ? x : best), undefined);
    } else {
      return;
    }
    ev.preventDefault();
    if (target) this.focusBar(target);
  }

  private focusBar(b: BarVm): void {
    const el = this.scroller()?.nativeElement;
    if (!el) return;
    if (b.left < el.scrollLeft || b.left + b.width > el.scrollLeft + el.clientWidth - LEFT) {
      el.scrollLeft = Math.max(0, b.left - 80);
      this.onScroll();
    }
    this.focusId.set(b.w.id);
    setTimeout(() => el.querySelector<HTMLElement>(`[data-bar="${b.w.id}"]`)?.focus(), 30);
  }
}
