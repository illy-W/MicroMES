import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, ElementRef, inject, signal, untracked, viewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideAlarmClock,
  lucideCalendarX,
  lucideChevronLeft,
  lucideChevronRight,
  lucideCircleAlert,
  lucideCircleCheck,
  lucideCircleDot,
  lucideClock,
  lucideFoldHorizontal,
  lucideInfo,
  lucideMinus,
  lucideOctagonX,
  lucidePause,
  lucidePlay,
  lucidePlus,
  lucideRefreshCw,
  lucideSearch,
  lucideTriangleAlert,
  lucideWorkflow,
} from '@ng-icons/lucide';
import { WorkOrderStatus } from '@micromes/api-client/models';
import type { TimelineLoadModel, TimelineModel, TimelineNonWorkingModel, TimelineWorkOrderModel } from '@micromes/api-client/models';
import { PlanningService } from '@micromes/api-client/services/planning';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmPopoverImports } from '@spartan-ng/helm/popover';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSheetImports } from '@spartan-ng/helm/sheet';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTooltip } from '@spartan-ng/helm/tooltip';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';
import { WORK_STATUS, workStatus } from '../production-orders/production-order-labels';
import { buildGraph, chainOf } from './dependency-graph';
import { isOffRange, routeConnector, stubRoute } from './dependency-route';
import { WorkOrdersStore } from '../work-orders/work-orders.store';
import { type DepLink, PlanningWorkPanelComponent } from './planning-work-panel.component';
import { PlanningStore } from './planning.store';
import { CHIP_EXTRA_CODE, CHIP_EXTRA_FULL, estimateTextWidth, fitLeftChip, LABEL_INSIDE_MIN, placeLabel } from './bar-label';
import { type Axis, buildAxis, carveAroundInstants, commonClosedRanges, type Range } from './timeline-axis';
import { detailLevel, operationNumber, orderColor, STATUS_STYLE } from './timeline-style';
import {
  addDays, addMonths, dayKey, daysInMonth, dayStart, formatDateTime, formatDay, formatDuration, formatMonthTitle, formatWeekday, HOUR, hourLabel, mondayOf, monthStart,
} from './timeline-time';

type Zoom = 'day' | 'week' | 'month';
const ZOOMS: { value: Zoom; label: string }[] = [
  { value: 'day', label: 'Giorno' },
  { value: 'week', label: 'Settimana' },
  { value: 'month', label: 'Mese' },
];
/** Pixel per ora (di asse, cioè lavorativa quando il tempo chiuso è compresso) di default per ogni zoom. */
const DEFAULT_PPH: Record<Zoom, number> = { day: 90, week: 28, month: 10 };
const PPH_MIN = 6;
const PPH_MAX = 80;
const LEFT = 224;
/** Altezza di una corsia (barra 44 + 4 di spazio). */
const LANE_H = 48;
const BAR_H = LANE_H - 4;
const ROW_PAD = 4;
const ROW_MIN = 56;
/** Spazio sopra le corsie di una riga con marcatori di carico alto. */
const MARK_PAD = 16;
/** Larghezza del separatore che sostituisce un intervallo chiuso compresso. */
const SEP_W = 12;
const DAY_H = 24;
const HOUR_H = 20;
const MIN_W = 28;
const ALL = 'all';
/** Larghezza minima del contenitore per il pannello affiancato. */
const WIDE_MIN = 1100;
const NONE_ROW = -1;

/** Rettangolo di una barra nelle coordinate dell'area righe (x dall'inizio della finestra, y dalla prima riga). Serve all'overlay dipendenze. */
export interface BarRect {
  x: number;
  y: number;
  w: number;
  h: number;
  rowKey: number;
}
interface BarTip {
  phase: string;
  workOrder: string;
  order: string;
  machine: string;
  period: string;
  duration: string;
  status: string;
  plan: string;
  predecessors: number;
}
interface BarVm {
  w: TimelineWorkOrderModel;
  left: number;
  width: number;
  /** Posizione verticale nell'area righe (riga + corsia). */
  y: number;
  height: number;
  lane: number;
  rowKey: number;
  overload: boolean;
  dim: boolean;
  late: boolean;
  stale: boolean;
  level: 0 | 1 | 2 | 3;
  /** Testo dentro la barra (false se l'etichetta è fuori). */
  inside: boolean;
  chip: ChipVm | null;
  statusIcon: string;
  name: string;
  segCode: string;
  sub: string;
  color: string;
  padRight: number;
  cls: string;
  aria: string;
  tip: BarTip;
}
interface BandVm { left: number; width: number; reason: string; cls: string }
interface LoadMarkVm { left: number; width: number; pct: number; title: string }
/** Etichetta fuori barra (stile Gantt): chip a destra/sinistra della barra o solo codice fase. */
interface ChipVm {
  left: number;
  end: boolean;
  text: string;
  sub: string | null;
  /** Larghezza stimata dello chip completo e del solo codice (per il clamp sotto la colonna sticky). */
  fullW: number;
  codeW: number;
  code: string;
}
interface RowVm {
  key: number;
  code: string;
  name: string;
  capacity: number;
  inactive: boolean;
  /** Offset verticale della riga nell'area righe. */
  top: number;
  height: number;
  bars: BarVm[];
  bands: BandVm[];
  marks: LoadMarkVm[];
  summary: number | null;
  summaryFill: number;
}
interface DepEdge {
  key: string;
  predId: number;
  succId: number;
  d: string;
  stroke: string;
  dash: boolean;
  conflict: boolean;
  warn: { x: number; y: number } | null;
  label: { x: number; y: number; text: string } | null;
  minX: number;
  maxX: number;
}
interface DepDrawn extends DepEdge { opacity: number; width: number }
interface DayCell { left: number; width: number; label: string }
interface HourTick { left: number; label: string }
interface GridLine { left: number; major: boolean }
interface SepVm { left: number; width: number; title: string }

const bootstrapTz = (): string => Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
const clampPph = (n: number): number => Math.min(PPH_MAX, Math.max(PPH_MIN, Math.round(n)));

/** Timeline di pianificazione a griglia CSS (nessuna libreria), con pannello di dettaglio e filtri. */
@Component({
  selector: 'app-planning-timeline',
  imports: [HlmButton, HlmEmptyImports, HlmInput, HlmPopoverImports, HlmSelectImports, HlmSheetImports, HlmSkeleton, HlmTooltip, NgIcon, PlanningWorkPanelComponent],
  providers: [
    provideIcons({
      lucideAlarmClock, lucideCalendarX, lucideChevronLeft, lucideChevronRight, lucideCircleAlert, lucideCircleCheck, lucideCircleDot, lucideClock,
      lucideFoldHorizontal, lucideInfo, lucideMinus, lucideOctagonX, lucidePause, lucidePlay, lucidePlus, lucideRefreshCw, lucideSearch, lucideTriangleAlert, lucideWorkflow,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-card text-card-foreground flex min-h-0 flex-1 flex-col gap-2 rounded-xl border p-2" aria-label="Timeline di pianificazione">
      <div class="flex flex-wrap items-center gap-x-2 gap-y-1.5">
        <div class="bg-muted inline-flex gap-0.5 rounded-md p-0.5" role="group" aria-label="Zoom">
          @for (z of zooms; track z.value) {
            <button hlmBtn type="button" size="sm" class="h-7" [variant]="zoom() === z.value ? 'default' : 'ghost'" [attr.aria-pressed]="zoom() === z.value" (click)="setZoom(z.value)">{{ z.label }}</button>
          }
        </div>
        <div class="flex items-center gap-1">
          <button hlmBtn type="button" size="icon-sm" variant="outline" aria-label="Periodo precedente" hlmTooltip="Periodo precedente" (click)="shift(-1)"><ng-icon name="lucideChevronLeft" aria-hidden="true" /></button>
          <button hlmBtn type="button" size="sm" variant="outline" (click)="today()">Oggi</button>
          <button hlmBtn type="button" size="icon-sm" variant="outline" aria-label="Periodo successivo" hlmTooltip="Periodo successivo" (click)="shift(1)"><ng-icon name="lucideChevronRight" aria-hidden="true" /></button>
        </div>
        <p class="min-w-32 text-sm font-medium capitalize" aria-live="polite">{{ title() }}</p>
        <button hlmBtn type="button" size="icon-sm" variant="ghost" aria-label="Ricarica timeline" hlmTooltip="Ricarica timeline" (click)="reload()"><ng-icon name="lucideRefreshCw" aria-hidden="true" /></button>

        <div class="ml-auto flex items-center gap-1" role="group" aria-label="Opzioni di visualizzazione">
          <button hlmBtn type="button" size="icon-sm" variant="outline" aria-label="Riduci densità" [hlmTooltip]="'Riduci densità (' + pph() + ' px/h)'" [disabled]="pph() <= PPH_MIN" (click)="density(-1)"><ng-icon name="lucideMinus" aria-hidden="true" /></button>
          <button hlmBtn type="button" size="icon-sm" variant="outline" aria-label="Aumenta densità" [hlmTooltip]="'Aumenta densità (' + pph() + ' px/h)'" [disabled]="pph() >= PPH_MAX" (click)="density(1)"><ng-icon name="lucidePlus" aria-hidden="true" /></button>
          <button hlmBtn type="button" size="icon-sm" [variant]="collapse() ? 'secondary' : 'outline'" aria-label="Comprimi tempo non lavorativo" hlmTooltip="Comprimi tempo non lavorativo" [attr.aria-pressed]="collapse()" (click)="collapse.set(!collapse())">
            <ng-icon name="lucideFoldHorizontal" aria-hidden="true" />
          </button>
          <button hlmBtn type="button" size="icon-sm" [variant]="depsAll() ? 'secondary' : 'outline'" aria-label="Mostra tutte le dipendenze" hlmTooltip="Mostra tutte le dipendenze (altrimenti solo quelle del lavoro selezionato o sotto il mouse)" [attr.aria-pressed]="depsAll()" (click)="depsAll.set(!depsAll())">
            <ng-icon name="lucideWorkflow" aria-hidden="true" />
          </button>
          <hlm-popover align="end">
            <button hlmBtn hlmPopoverTrigger type="button" size="icon-sm" variant="outline" aria-label="Legenda" hlmTooltip="Legenda"><ng-icon name="lucideInfo" aria-hidden="true" /></button>
            <hlm-popover-content *hlmPopoverPortal="let ctx" class="max-h-[70vh] w-[min(26rem,calc(100vw-2rem))] gap-3 overflow-y-auto" role="dialog" aria-label="Legenda della timeline">
              <section class="flex flex-col gap-1.5" aria-label="Stati">
                <p class="text-muted-foreground text-xs font-medium tracking-wide uppercase">Stato del lavoro</p>
                <ul class="grid grid-cols-2 gap-x-3 gap-y-1.5">
                  @for (s of legend; track s.label) {
                    <li class="inline-flex items-center gap-1.5">
                      <span class="relative inline-block h-3.5 w-6 shrink-0 rounded-sm" [class]="s.box + ' ' + s.strip" style="--oc: var(--muted-foreground)" aria-hidden="true"></span>
                      <ng-icon [name]="s.icon" size="12" aria-hidden="true" /> {{ s.label }}
                    </li>
                  }
                </ul>
              </section>
              <section class="flex flex-col gap-1.5" aria-label="Segnali">
                <p class="text-muted-foreground text-xs font-medium tracking-wide uppercase">Segnali sulla barra</p>
                <ul class="flex flex-col gap-1.5">
                  <li class="inline-flex items-center gap-1.5"><ng-icon name="lucideAlarmClock" size="14" class="text-destructive" aria-hidden="true" /> In ritardo</li>
                  <li class="inline-flex items-center gap-1.5"><ng-icon name="lucideTriangleAlert" size="14" aria-hidden="true" /> Piano obsoleto</li>
                  <li class="inline-flex items-center gap-1.5"><ng-icon name="lucideCircleAlert" size="14" class="text-destructive" aria-hidden="true" /> Sovraccarico della macchina</li>
                </ul>
              </section>
              <section class="flex flex-col gap-1.5" aria-label="Tempo">
                <p class="text-muted-foreground text-xs font-medium tracking-wide uppercase">Calendario</p>
                <ul class="flex flex-col gap-1.5">
                  <li class="inline-flex items-center gap-1.5"><span class="bg-muted/40 inline-block h-3 w-6 shrink-0 border" aria-hidden="true"></span> Non lavorativo</li>
                  <li class="inline-flex items-center gap-1.5"><span class="bg-muted/30 inline-block h-3 w-6 shrink-0 border bg-[repeating-linear-gradient(45deg,transparent,transparent_3px,var(--border)_3px,var(--border)_4px)]" aria-hidden="true"></span> Eccezione di calendario o macchina</li>
                  <li class="inline-flex items-center gap-1.5"><span class="bg-muted inline-block h-3 w-3 shrink-0 border-x" aria-hidden="true"></span> Tempo non lavorativo compresso</li>
                </ul>
              </section>
              <section class="flex flex-col gap-1.5" aria-label="Dipendenze">
                <p class="text-muted-foreground text-xs font-medium tracking-wide uppercase">Dipendenze</p>
                <ul class="flex flex-col gap-1.5">
                  <li class="inline-flex items-center gap-1.5"><svg width="24" height="8" class="shrink-0" aria-hidden="true"><line x1="0" y1="4" x2="24" y2="4" class="stroke-foreground" stroke-width="2" /></svg> Nello stesso ordine</li>
                  <li class="inline-flex items-center gap-1.5"><svg width="24" height="8" class="shrink-0" aria-hidden="true"><line x1="0" y1="4" x2="24" y2="4" class="stroke-foreground" stroke-width="2" stroke-dasharray="6 4" /></svg> Tra ordini (figlio → padre)</li>
                  <li class="inline-flex items-center gap-1.5"><svg width="24" height="8" class="shrink-0" aria-hidden="true"><line x1="0" y1="4" x2="24" y2="4" class="stroke-destructive" stroke-width="2" /></svg> Conflitto (il successore parte prima)</li>
                </ul>
              </section>
            </hlm-popover-content>
          </hlm-popover>
        </div>
      </div>
      <div class="flex items-center gap-2">
        <div class="relative shrink-0">
          <ng-icon name="lucideSearch" class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2" aria-hidden="true" />
          <input hlmInput type="search" class="h-8 w-36 pl-8" placeholder="Cerca lavoro" aria-label="Cerca per codice lavoro o ordine" [value]="search()" (input)="search.set($any($event.target).value)" />
        </div>
        <hlm-select [value]="orderFilter()" [itemToString]="orderLabel" (valueChange)="orderFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-32 shrink-0" aria-label="Filtra per ordine"><hlm-select-value /></hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="all">Tutti gli ordini</hlm-select-item>
            @for (o of orderOptions(); track o.id) { <hlm-select-item [value]="o.id">{{ o.code }}</hlm-select-item> }
          </hlm-select-content>
        </hlm-select>
        <hlm-select [value]="statusFilter()" [itemToString]="statusLabel" (valueChange)="statusFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-32 shrink-0" aria-label="Filtra per stato"><hlm-select-value /></hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="all">Tutti gli stati</hlm-select-item>
            @for (s of statuses; track s.value) { <hlm-select-item [value]="s.value">{{ s.label }}</hlm-select-item> }
          </hlm-select-content>
        </hlm-select>
        <hlm-select [value]="machineFilter()" [itemToString]="machineLabel" (valueChange)="machineFilter.set($event ?? 'all')">
          <hlm-select-trigger size="sm" class="w-36 shrink-0" aria-label="Filtra per macchina"><hlm-select-value /></hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-item value="all">Tutte le macchine</hlm-select-item>
            @for (m of machineOptions(); track m.id) { <hlm-select-item [value]="m.id">{{ m.label }}</hlm-select-item> }
          </hlm-select-content>
        </hlm-select>
        @if (filtersActive()) {
          <button hlmBtn type="button" size="sm" variant="ghost" class="shrink-0" (click)="clearFilters()">Azzera filtri</button>
        }
        @if (search().trim()) {
          <span class="text-muted-foreground shrink-0 text-xs whitespace-nowrap" role="status">{{ matchCount() }} trovati</span>
        }
        @if (legendOrders().length) {
          <div class="bg-border hidden h-5 w-px shrink-0 sm:block" aria-hidden="true"></div>
          <div class="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto px-0.5 py-1 whitespace-nowrap" role="group" aria-label="Ordini: clic per evidenziare">
            @for (o of legendOrders(); track o.id) {
              <button
                type="button"
                class="hover:bg-muted focus-visible:ring-ring flex h-6 shrink-0 items-center gap-1.5 rounded-md border px-1.5 text-xs whitespace-nowrap focus-visible:ring-2 focus-visible:outline-none"
                [class.bg-muted]="highlight() === o.id"
                [class.ring-2]="highlight() === o.id"
                [class.ring-primary]="highlight() === o.id"
                [attr.aria-pressed]="highlight() === o.id"
                [attr.aria-label]="'Evidenzia ordine ' + o.code + ' ' + o.material"
                [title]="o.code + ' · ' + o.material"
                (click)="toggleHighlight(o.id)"
                (mouseenter)="hoverOrder.set(o.id)"
                (mouseleave)="hoverOrder.set(null)"
              >
                <span class="size-2.5 shrink-0 rounded-full bg-(--oc)" [style.--oc]="o.color" aria-hidden="true"></span>
                <span class="font-medium">{{ o.short }}</span>
                <span class="text-muted-foreground max-w-[14ch] truncate">{{ o.material }}</span>
              </button>
            }
            @if (highlight() !== null) {
              <button hlmBtn type="button" size="sm" variant="ghost" class="h-6 shrink-0 px-1.5 text-xs" (click)="highlight.set(null)">Rimuovi evidenziazione</button>
            }
          </div>
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
          @for (i of [1, 2, 3, 4]; track i) { <hlm-skeleton class="h-14 w-full" /> }
        </div>
      } @else {
        @if (windowEmpty()) {
          <div class="bg-muted/50 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-md border px-3 py-1.5 text-sm" role="status">
            <ng-icon name="lucideCalendarX" aria-hidden="true" class="text-muted-foreground" />
            <span class="font-medium">Nessun lavoro in questa finestra</span>
            @if (jump(); as j) {
              <button hlmBtn type="button" size="sm" variant="outline" (click)="goTo(j.ms)">{{ j.label }}</button>
            }
          </div>
        }
        <div class="flex min-h-0 flex-1 gap-2" [attr.aria-busy]="loading()">
          <div class="flex min-h-0 min-w-0 flex-1 flex-col">
            <div
              #scroller
              role="region"
              aria-label="Timeline delle macchine"
              class="relative min-h-0 flex-1 overflow-auto rounded-md border"
              [class.opacity-70]="loading()"
              (scroll)="onScroll()"
            >
              <div class="relative" [style.width.px]="LEFT + totalPx()">
                <div class="bg-card sticky top-0 z-20 flex border-b">
                  <div class="bg-card sticky left-0 z-10 flex shrink-0 items-center border-r px-2 text-xs font-medium" [style.width.px]="LEFT" [style.height.px]="headH()">Macchina</div>
                  <div class="relative" [style.width.px]="totalPx()" [style.height.px]="headH()">
                    @for (d of dayCells(); track d.left) {
                      <div class="absolute top-0 overflow-hidden border-l" [style.left.px]="d.left" [style.width.px]="d.width" [style.height.px]="DAY_H">
                        <span class="sticky inline-block px-1.5 text-xs leading-6 font-medium whitespace-nowrap" [style.left.px]="LEFT">{{ d.label }}</span>
                      </div>
                    }
                    @for (t of hourTicks(); track t.left) {
                      <span class="text-muted-foreground absolute flex items-center border-l pl-1 text-[11px] tabular-nums" [style.top.px]="DAY_H" [style.height.px]="HOUR_H" [style.left.px]="t.left">{{ t.label }}</span>
                    }
                    @for (sp of seps(); track sp.left) {
                      <div class="bg-muted absolute inset-y-0 border-x" [style.left.px]="sp.left" [style.width.px]="sp.width" [title]="sp.title"></div>
                    }
                    @if (nowPx(); as np) {
                      <span class="bg-primary text-primary-foreground pointer-events-none absolute bottom-0 -translate-x-1/2 rounded px-1 text-[10px] leading-4 font-medium" [style.left.px]="np" aria-hidden="true">Adesso</span>
                    }
                  </div>
                </div>

                <div class="pointer-events-none absolute bottom-0" [style.left.px]="LEFT" [style.top.px]="headH()" [style.width.px]="totalPx()" aria-hidden="true">
                  @for (g of gridLines(); track $index) {
                    <div class="absolute inset-y-0 border-l" [class]="g.major ? 'border-border' : 'border-border/40'" [style.left.px]="g.left"></div>
                  }
                  @for (sp of seps(); track sp.left) {
                    <div class="bg-muted/80 pointer-events-auto absolute inset-y-0 z-0 border-x" [style.left.px]="sp.left" [style.width.px]="sp.width" [title]="sp.title"></div>
                  }
                  @if (nowPx(); as np) {
                    <div class="bg-primary absolute inset-y-0 z-[5] w-0.5" [style.left.px]="np" title="Adesso"></div>
                  }
                </div>

                @for (r of rows(); track r.key) {
                  <div class="relative flex border-b" role="group" [attr.aria-label]="'Macchina ' + r.code" [attr.data-row]="r.key">
                    <div class="bg-card sticky left-0 z-10 flex shrink-0 flex-col justify-center gap-0.5 border-r px-2 py-1" [style.width.px]="LEFT">
                      <div class="flex items-center gap-1.5 text-xs">
                        <span class="truncate font-semibold">{{ r.code }}</span>
                        @if (r.capacity > 1) { <span class="bg-secondary text-secondary-foreground shrink-0 rounded px-1" [title]="'Capacità ' + r.capacity">×{{ r.capacity }}</span> }
                        @if (r.key === NONE_ROW) {
                          <span tabindex="0" role="img" class="text-muted-foreground inline-flex shrink-0" aria-label="Fasi senza macchine collegate" hlmTooltip="Fasi senza macchine collegate"><ng-icon name="lucideInfo" size="14" aria-hidden="true" /></span>
                        }
                        @if (r.summary !== null) {
                          <span class="ml-auto flex shrink-0 items-center gap-1 tabular-nums" [class.text-destructive]="r.summary > 100" [title]="'Carico ' + r.summary + '%' + (r.summary > 100 ? ' (sovraccarico)' : '')">
                            @if (r.summary > 100) { <ng-icon name="lucideCircleAlert" size="12" aria-hidden="true" /> }
                            <span class="bg-muted inline-block h-1.5 w-10 overflow-hidden rounded-full" aria-hidden="true">
                              <span class="block h-full rounded-full" [class]="r.summary > 100 ? 'bg-destructive' : 'bg-primary'" [style.width.%]="r.summaryFill"></span>
                            </span>
                            <span>{{ r.summary }}%</span>
                            <span class="sr-only">di carico{{ r.summary > 100 ? ', sovraccarico' : '' }}</span>
                          </span>
                        }
                      </div>
                      <span class="text-muted-foreground truncate text-[11px]">{{ r.name }}{{ r.inactive ? ' (non attiva)' : '' }}</span>
                    </div>
                    <div class="relative shrink-0" [style.width.px]="totalPx()" [style.height.px]="r.height">
                      @for (b of r.bands; track $index) {
                        <div class="absolute top-0 h-full" [class]="b.cls" [style.left.px]="b.left" [style.width.px]="b.width" [title]="b.reason" aria-hidden="true"></div>
                      }
                      @for (b of r.bars; track b.w.id) {
                        <ng-template #tip>
                          <div class="flex max-w-80 flex-col gap-1 text-left">
                            <span class="text-sm font-medium">{{ b.tip.phase }}</span>
                            <div class="grid grid-cols-[auto_1fr] gap-x-2 gap-y-0.5">
                              <span class="opacity-70">Lavoro</span><span>{{ b.tip.workOrder }}</span>
                              <span class="opacity-70">Ordine</span><span>{{ b.tip.order }}</span>
                              <span class="opacity-70">Macchina</span><span>{{ b.tip.machine }}</span>
                              <span class="opacity-70">Periodo</span><span class="tabular-nums">{{ b.tip.period }}</span>
                              <span class="opacity-70">Durata</span><span>{{ b.tip.duration }}</span>
                              <span class="opacity-70">Stato</span><span>{{ b.tip.status }}</span>
                              <span class="opacity-70">Piano</span><span>{{ b.tip.plan }}</span>
                              <span class="opacity-70">Predecessori</span><span>{{ b.tip.predecessors }}</span>
                            </div>
                          </div>
                        </ng-template>
                        <button
                          type="button"
                          [class]="b.cls"
                          [class.opacity-25]="dimmed(b)"
                          [class.ring-2]="selectedId() === b.w.id"
                          [class.ring-1]="chainRing(b)"
                          [class.ring-primary]="selectedId() === b.w.id || chainRing(b)"
                          [class.opacity-35]="chainDim(b)"
                          [style.left.px]="b.left"
                          [style.width.px]="b.width"
                          [style.top.px]="b.y - r.top"
                          [style.height.px]="b.height"
                          [style.padding-right.px]="b.padRight"
                          [style.--oc]="b.color"
                          [attr.data-bar]="b.w.id"
                          [attr.aria-label]="b.aria"
                          [attr.aria-pressed]="selectedId() === b.w.id"
                          [attr.tabindex]="b.w.id === tabStop() ? 0 : -1"
                          [hlmTooltip]="tip"
                          position="top"
                          [showDelay]="200"
                          (click)="select(b.w)"
                          (focus)="focusId.set(b.w.id); focusedId.set(b.w.id)"
                          (blur)="focusedId.set(null)"
                          (mouseenter)="hoverOrder.set(b.w.productionOrderId); hoverId.set(b.w.id)"
                          (mouseleave)="hoverOrder.set(null); hoverId.set(null)"
                          (keydown)="onKey($event, b)"
                        >
                          @if (b.level >= 1 && b.inside) {
                            <span class="flex min-w-0 items-center gap-1">
                              @if (b.level >= 3) { <ng-icon [name]="b.statusIcon" size="14" class="shrink-0" aria-hidden="true" /> }
                              <span class="truncate" [class.font-medium]="b.level >= 2">{{ b.level === 1 ? b.segCode : b.name }}</span>
                            </span>
                            @if (b.level >= 3) { <span class="truncate text-[11px] opacity-75">{{ b.sub }}</span> }
                          }
                          @if (b.late || b.stale || b.overload) {
                            <span class="absolute top-0.5 right-0.5 flex gap-0.5" [class.flex-col]="b.width < 56">
                              @if (b.late) { <span class="bg-destructive text-destructive-foreground flex size-4 items-center justify-center rounded-full" title="In ritardo"><ng-icon name="lucideAlarmClock" size="11" aria-hidden="true" /></span> }
                              @if (b.stale) { <span class="bg-secondary text-secondary-foreground ring-border flex size-4 items-center justify-center rounded-full ring-1" title="Piano obsoleto"><ng-icon name="lucideTriangleAlert" size="11" aria-hidden="true" /></span> }
                              @if (b.overload) { <span class="bg-destructive text-destructive-foreground flex size-4 items-center justify-center rounded-full" title="Sovraccarico"><ng-icon name="lucideCircleAlert" size="11" aria-hidden="true" /></span> }
                            </span>
                          }
                        </button>
                      }
                      @for (c of r.bars; track c.w.id) {
                        @if (c.chip; as ch0) {
                          @if (chipView(ch0); as ch) {
                            <div class="pointer-events-none absolute z-[2] flex items-center" [class.-translate-x-full]="ch0.end" [style.left.px]="ch0.left" [style.top.px]="c.y - r.top" [style.height.px]="c.height" aria-hidden="true">
                              <span class="bg-background ring-border flex items-center gap-1 rounded border-l-[3px] border-(color:--oc) px-1.5 py-0.5 text-xs leading-tight whitespace-nowrap ring-1" [style.--oc]="c.color" [class.opacity-25]="dimmed(c)">
                                @if (ch.sub) { <ng-icon [name]="c.statusIcon" size="12" class="shrink-0" /> }
                                <span class="flex flex-col" [class.text-right]="ch0.end">
                                  <span [class.font-medium]="ch.sub">{{ ch.text }}</span>
                                  @if (ch.sub) { <span class="text-muted-foreground text-[11px]">{{ ch.sub }}</span> }
                                </span>
                              </span>
                            </div>
                          }
                        }
                      }
                      @for (m of r.marks; track $index) {
                        <div class="absolute top-0 z-[3] flex items-center justify-center" [style.left.px]="m.left" [style.width.px]="m.width">
                          <span class="bg-destructive/15 text-destructive flex items-center gap-0.5 rounded-b px-1 text-[10px] leading-3.5 font-medium tabular-nums" [title]="m.title">
                            <ng-icon name="lucideCircleAlert" size="10" aria-hidden="true" />{{ m.pct }}%
                          </span>
                        </div>
                      }
                    </div>
                  </div>
                }

                <svg class="pointer-events-none absolute z-[6] overflow-hidden" [style.left.px]="LEFT" [style.top.px]="headH() + 1" [attr.width]="totalPx()" [attr.height]="rowsH()" aria-hidden="true">
                  <defs>
                    <marker id="dep-arrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto"><path d="M0 0 L8 4 L0 8 z" fill="context-stroke" /></marker>
                  </defs>
                  @for (e of drawn(); track e.key) {
                    <g [attr.opacity]="e.opacity">
                      <path [attr.d]="e.d" fill="none" stroke-linejoin="round" marker-end="url(#dep-arrow)" [style.stroke]="e.stroke" [attr.stroke-width]="e.width" [attr.stroke-dasharray]="e.dash ? '6 4' : null" />
                      @if (e.label; as lb) {
                        <text [attr.x]="lb.x" [attr.y]="lb.y" text-anchor="end" class="fill-muted-foreground stroke-card text-[10px]" stroke-width="3" paint-order="stroke">{{ lb.text }}</text>
                      }
                      @if (e.warn; as w) {
                        <circle [attr.cx]="w.x" [attr.cy]="w.y" r="6" class="fill-destructive" />
                        <text [attr.x]="w.x" [attr.y]="w.y + 3.5" text-anchor="middle" class="fill-destructive-foreground text-[10px] font-bold">!</text>
                      }
                    </g>
                  }
                </svg>
                <!-- Elemento HTML dopo l'svg: riporta il namespace a HTML, altrimenti i dialog CDK creati dopo (sheet) nascono come SVG e restano a dimensione zero. -->
                <span class="hidden" aria-hidden="true"></span>
              </div>
            </div>
            @if (undated() > 0) { <p class="text-muted-foreground mt-1 text-xs">{{ undated() }} lavori senza data non mostrati</p> }
          </div>

          @if (wide() && selected(); as sel) {
            <div class="bg-card min-h-0 w-[22rem] shrink-0 self-stretch overflow-y-auto rounded-md border">
              <app-planning-work-panel [item]="sel" [timeZone]="tz()" [machineLabels]="machineLabels()" [links]="links()" (selectLink)="focusWorkOrder($event)" (closed)="selectedId.set(null)" (changed)="reload()" />
            </div>
          }
        </div>
        @if (!wide()) {
          <hlm-sheet side="right" [state]="selected() ? 'open' : 'closed'" (stateChanged)="onSheetState($event)">
            <hlm-sheet-content *hlmSheetPortal="let ctx" class="bg-background data-[side=right]:w-[min(24rem,90vw)] data-[side=right]:sm:max-w-none gap-0 overflow-y-auto p-0">
              <hlm-sheet-header class="bg-background sticky top-0 z-10 border-b py-3 pr-12 pl-3">
                <h2 hlmSheetTitle>Dettaglio lavoro</h2>
                <p hlmSheetDescription class="sr-only">Stato, dipendenze e segnalazioni del lavoro selezionato.</p>
              </hlm-sheet-header>
              @if (selected(); as sel) {
                <app-planning-work-panel [item]="sel" [timeZone]="tz()" [machineLabels]="machineLabels()" [links]="links()" (selectLink)="focusWorkOrder($event)" (closed)="selectedId.set(null)" (changed)="reload()" />
              }
            </hlm-sheet-content>
          </hlm-sheet>
        }
      }
    </section>
  `,
})
export class PlanningTimelineComponent {
  private readonly api = inject(PlanningService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly store = inject(PlanningStore);
  private readonly orders = inject(WorkOrdersStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');

  protected readonly LEFT = LEFT;
  protected readonly DAY_H = DAY_H;
  protected readonly HOUR_H = HOUR_H;
  protected readonly NONE_ROW = NONE_ROW;
  protected readonly PPH_MIN = PPH_MIN;
  protected readonly PPH_MAX = PPH_MAX;
  protected readonly zooms = ZOOMS;
  protected readonly statuses = (Object.keys(WORK_STATUS) as WorkOrderStatus[]).map((value) => ({ value, label: WORK_STATUS[value].label }));
  protected readonly legend = (Object.keys(WORK_STATUS) as WorkOrderStatus[]).map((s) => ({ icon: WORK_STATUS[s].icon, label: WORK_STATUS[s].label, ...STATUS_STYLE[s] }));

  // --- stato (inizializzato dai query params) ---
  private readonly qp = this.route.snapshot.queryParamMap;
  protected readonly zoom = signal<Zoom>((['day', 'week', 'month'] as const).find((z) => z === this.qp.get('zoom')) ?? 'week');
  protected readonly cursor = signal<string>(/^\d{4}-\d{2}-\d{2}$/.test(this.qp.get('from') ?? '') ? (this.qp.get('from') as string) : dayKey(Date.now(), bootstrapTz()));
  protected readonly orderFilter = signal(this.qp.get('order') ?? ALL);
  protected readonly statusFilter = signal(this.qp.get('status') ?? ALL);
  protected readonly machineFilter = signal(this.qp.get('machine') ?? ALL);
  protected readonly search = signal(this.qp.get('q') ?? '');
  protected readonly selectedId = signal<number | null>(null);
  /** Larghezza del contenitore: da 1100 px il pannello sta accanto alla timeline, sotto è un foglio laterale. */
  private readonly hostW = signal(0);
  protected readonly wide = computed(() => this.hostW() >= WIDE_MIN);
  protected readonly focusId = signal<number | null>(null);
  /** Densità orizzontale (px per ora d'asse). */
  protected readonly pph = signal(DEFAULT_PPH[this.zoom()]);
  protected readonly collapse = signal(true);
  /** Ordine evidenziato dalla legenda (clic) e ordine sotto il mouse: gli altri vengono attenuati. */
  protected readonly highlight = signal<number | null>(null);
  protected readonly hoverOrder = signal<number | null>(null);
  protected readonly hoverId = signal<number | null>(null);
  protected readonly focusedId = signal<number | null>(null);
  /** Modalità dipendenze: `false` = solo catena del lavoro attivo; `true` = tutte (query `deps=all`). */
  protected readonly depsAll = signal(this.qp.get('deps') === 'all');

  protected readonly data = signal<TimelineModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly knownOrders = signal<ReadonlyMap<number, string>>(new Map());
  /**
   * Tonalità per ordine (indice 0..7 di `--chart-n`), assegnata alla prima apparizione nei dati caricati e mai riassegnata
   * durante la sessione: ordini adiacenti nel caricamento hanno tonalità diverse. Il modello timeline non espone l'ordine padre,
   * quindi ogni ordine (anche figlio) ha la propria tonalità; col wrap (>8 ordini) due ordini possono condividerla, il codice ordine resta sempre nella barra.
   */
  private readonly orderHue = signal<ReadonlyMap<number, number>>(new Map());
  private readonly lastSelected = signal<TimelineWorkOrderModel | null>(null);
  private readonly now = signal(Date.now());
  private readonly vp = signal({ from: 0, to: 1600 });
  /** Lavori in una finestra ampia (senza filtri) per saltare al lavoro più vicino e mostrare lavori fuori finestra. */
  private readonly probe = signal<TimelineWorkOrderModel[]>([]);
  private pendingScroll: number | null = null;
  private autoStartDone = false;
  /** Selezione da ripristinare (parametro `wo` o collegamento fuori finestra) quando i dati sono caricati. */
  private pendingSelect: number | null = Number(this.qp.get('wo')) || null;

  protected readonly tz = computed(() => this.data()?.timeZoneId ?? bootstrapTz());

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

  /** Intervalli compressi: chiusi per TUTTE le macchine mostrate e senza inizio/fine di lavori al loro interno. */
  private readonly closed = computed<Range[]>(() => {
    const d = this.data();
    if (!d || !this.collapse()) return [];
    const w = this.win();
    const mf = this.machineFilter();
    const machines = d.equipment.filter((e) => e.isActive && (mf === ALL || String(e.id) === mf));
    const nw = d.nonWorking.map((n) => ({ eq: n.equipmentId ?? null, start: new Date(n.start).getTime(), end: new Date(n.end).getTime() }));
    const perMachine = machines.map((m) => nw.filter((n) => n.eq == null || n.eq === m.id));
    const edges = d.workOrders
      .filter((x) => mf === ALL || String(x.equipmentId) === mf)
      .flatMap((x) => {
        const r = this.range(x);
        return r ? [r.s, r.e] : [];
      });
    const ranges = carveAroundInstants(commonClosedRanges(perMachine, w), edges);
    const total = ranges.reduce((a, r) => a + (r.end - r.start), 0);
    // Finestra quasi tutta chiusa (es. domenica nella vista Giorno): non comprimere.
    return total >= (w.end - w.start) * 0.9 ? [] : ranges;
  });
  protected readonly axis = computed<Axis>(() => buildAxis(this.win(), this.closed(), this.pph(), SEP_W));
  protected readonly totalPx = computed(() => this.axis().width);
  protected readonly headH = computed(() => DAY_H + (this.zoom() === 'month' ? 0 : HOUR_H));
  protected readonly nowPx = computed(() => {
    const n = this.now();
    const w = this.win();
    return n >= w.start && n <= w.end ? this.axis().toX(n) : null;
  });
  protected readonly seps = computed<SepVm[]>(() => {
    const tz = this.tz();
    return this.axis().segments
      .filter((sg) => sg.collapsed)
      .map((sg) => ({
        left: sg.x0,
        width: sg.x1 - sg.x0,
        title: `Chiuso: ${sg.end - sg.start <= 18 * HOUR ? 'notte' : 'weekend/ferie'} (${formatDateTime(sg.start, tz)} – ${formatDateTime(sg.end, tz)})`,
      }));
  });

  protected readonly title = computed(() => {
    const w = this.win();
    const tz = this.tz();
    const z = this.zoom();
    if (z === 'month') return formatMonthTitle(w.start, tz);
    if (z === 'day') return `${formatWeekday(w.start, tz)} ${formatDay(w.start, tz, true).replace(/^\d+\s/, '')}`;
    return `${formatDay(w.start, tz)} – ${formatDay(w.end - 1, tz, true)}`;
  });

  /** Intestazione giorni (etichetta sticky). */
  protected readonly dayCells = computed<DayCell[]>(() => {
    const { start, days } = this.span();
    const tz = this.tz();
    const ax = this.axis();
    const out: DayCell[] = [];
    for (let i = 0; i < days; i++) {
      const ds = dayStart(addDays(start, i), tz);
      const de = dayStart(addDays(start, i + 1), tz);
      const left = ax.toX(ds);
      const width = ax.toX(de) - left;
      if (width < 1) continue;
      out.push({ left, width, label: this.zoom() === 'day' ? `${formatWeekday(ds, tz)} ${formatDay(ds, tz).replace(/^\d+\s/, '')}` : formatWeekday(ds, tz) });
    }
    return out;
  });

  /** Tacche orarie: ogni 1 h (Giorno), 2 h (Settimana), nessuna (Mese); mai dentro il tempo compresso. */
  protected readonly hourTicks = computed<HourTick[]>(() => {
    const z = this.zoom();
    if (z === 'month') return [];
    const { start, days } = this.span();
    const tz = this.tz();
    const ax = this.axis();
    const np = this.nowPx();
    const out: HourTick[] = [];
    for (let i = 0; i < days; i++) {
      const ds = dayStart(addDays(start, i), tz);
      const de = dayStart(addDays(start, i + 1), tz);
      for (let t = ds; t < de; t += HOUR) {
        if (ax.isCollapsed(t)) continue;
        const label = hourLabel(t, tz);
        if (z === 'week' && Number(label) % 2 !== 0) continue;
        const l = ax.toX(t);
        // Il chip "Adesso" copre le tacche vicine: niente etichetta oraria lì sotto.
        out.push({ left: l, label: np != null && l > np - 40 && l < np + 22 ? '' : label });
      }
    }
    return out;
  });

  /** Linee verticali della griglia: giorni (marcate) e ore. */
  protected readonly gridLines = computed<GridLine[]>(() => {
    const { start, days } = this.span();
    const tz = this.tz();
    const ax = this.axis();
    const out: GridLine[] = [];
    for (let i = 0; i < days; i++) {
      const ds = dayStart(addDays(start, i), tz);
      if (!ax.isCollapsed(ds)) out.push({ left: ax.toX(ds), major: true });
    }
    for (const t of this.hourTicks()) if (!out.some((g) => Math.abs(g.left - t.left) < 1)) out.push({ left: t.left, major: false });
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

  protected readonly windowEmpty = computed(() => {
    const d = this.data();
    if (!d || this.loading()) return false;
    const w = this.win();
    return !d.workOrders.some((x) => {
      const r = this.range(x);
      return r && r.e > w.start && r.s < w.end;
    });
  });
  /** Lavoro pianificato più vicino alla finestra: prima il successivo, altrimenti l'ultimo precedente. */
  protected readonly jump = computed<{ ms: number; label: string } | null>(() => {
    const w = this.win();
    const tz = this.tz();
    const starts = this.probe().flatMap((x) => (x.plannedStartAt ? [new Date(x.plannedStartAt).getTime()] : []));
    const next = Math.min(...starts.filter((t) => t >= w.end));
    if (Number.isFinite(next)) return { ms: next, label: `Vai al primo lavoro pianificato (${this.ddmm(next, tz)})` };
    const prev = Math.max(...starts.filter((t) => t < w.start));
    return Number.isFinite(prev) ? { ms: prev, label: `Vai all'ultimo lavoro pianificato (${this.ddmm(prev, tz)})` } : null;
  });
  private ddmm(ms: number, tz: string): string {
    return formatDateTime(ms, tz).slice(0, 5);
  }

  private range(w: TimelineWorkOrderModel): { s: number; e: number } | null {
    const running = w.status === 'Running' || w.status === 'Ended';
    const s0 = (running && w.actualStartAt) || w.plannedStartAt;
    if (!s0) return null;
    const s = new Date(s0).getTime();
    const e0 = (w.status === 'Ended' && w.actualEndAt) || w.plannedEndAt || w.actualEndAt;
    const e = e0 ? new Date(e0).getTime() : s + 30 * 60_000;
    return { s, e: Math.max(e, s + 60_000) };
  }

  /** Tutti i lavori noti (finestra corrente e finestra ampia), per id. */
  private readonly pool = computed<ReadonlyMap<number, TimelineWorkOrderModel>>(() => {
    const m = new Map<number, TimelineWorkOrderModel>();
    for (const w of this.probe()) m.set(w.id, w);
    for (const w of this.data()?.workOrders ?? []) m.set(w.id, w);
    return m;
  });
  private readonly graph = computed(() => buildGraph(this.pool().values()));

  protected readonly allRows = computed<RowVm[]>(() => {
    const d = this.data();
    if (!d) return [];
    const tz = d.timeZoneId;
    const w = this.win();
    const ax = this.axis();
    const hues = this.orderHue();
    const q = this.search().trim().toLowerCase();
    const mf = this.machineFilter();
    const px = (t: number) => ax.toX(t);
    const pool = this.pool();
    const machineName = new Map(d.equipment.map((e) => [e.id, `${e.code} · ${e.name}`]));
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

    let top = 0;
    return rowSpec.map((r) => {
      const rl = (loads.get(r.key) ?? []).filter((l) => new Date(l.periodEnd).getTime() > w.start && new Date(l.periodStart).getTime() < w.end);
      // Carico per periodo: solo da 90% in su, come piccolo marcatore in cima alla riga (sotto 90% basta la barra nell'etichetta).
      const marks = r.key === NONE_ROW ? [] : rl.flatMap((l): LoadMarkVm[] => {
        const pct = Math.round(l.loadPercent);
        if (pct < 90) return [];
        const s0 = Math.max(new Date(l.periodStart).getTime(), w.start);
        const e0 = Math.min(new Date(l.periodEnd).getTime(), w.end);
        return [{ left: px(s0), width: Math.max(24, px(e0) - px(s0)), pct, title: `Carico ${pct}%: ${pct > 100 ? 'sovraccarico' : 'quasi saturo'} · ${l.busyMinutes}/${l.capacityMinutes} min` }];
      });
      const rowPad = marks.length ? MARK_PAD : ROW_PAD;
      const items = (byRow.get(r.key) ?? []).filter((x) => this.range(x));
      const lanes = Math.max(1, ...items.map((x) => x.lane + 1));
      const height = Math.max(ROW_MIN, rowPad + lanes * LANE_H);
      const rowTop = top;
      top += height + 1; // +1: bordo inferiore della riga
      const bars = items
        .map((x): BarVm => {
          const rg = this.range(x) as { s: number; e: number };
          const left = px(rg.s);
          const width = Math.max(MIN_W, px(rg.e) - left);
          const st = workStatus(x.status);
          const overload = r.key !== NONE_ROW && x.lane >= r.capacity;
          const late = x.isLate;
          const stale = x.planStatus === 'Stale';
          const dim = !!q && !this.matches(x, q);
          const level = detailLevel(width);
          const op = operationNumber(x.code);
          const name = x.processSegmentName || x.processSegmentCode;
          const sub = `${x.productionOrderCode}${op ? ` · Op. ${op}` : ''}`;
          const machine = r.key === NONE_ROW ? 'nessuna macchina' : (machineName.get(r.key) ?? r.code);
          const status = st.label;
          const plan = late ? `In ritardo${x.lateDays ? ` di ${x.lateDays} g` : ''}` : stale ? 'Piano obsoleto' : x.planStatus === 'Planned' ? 'Pianificato' : 'Non pianificato';
          const period = `${formatDateTime(rg.s, tz)} – ${formatDateTime(rg.e, tz)}`;
          const markers = (late ? 1 : 0) + (stale ? 1 : 0) + (overload ? 1 : 0);
          const aria = `${name}, ordine ${x.productionOrderCode}, lavoro ${x.code} su ${machine}, dal ${formatDateTime(rg.s, tz)} al ${formatDateTime(rg.e, tz)}, ${status.toLowerCase()}${late ? ', in ritardo' : ''}${stale ? ', piano obsoleto' : ''}${overload ? ', sovraccarico' : ''}${x.predecessorIds.length === 1 ? `, dopo ${pool.get(x.predecessorIds[0])?.code ?? 'un altro lavoro'}` : x.predecessorIds.length > 1 ? `, dopo ${x.predecessorIds.length} lavori` : ''}`;
          const style = STATUS_STYLE[x.status];
          const cls = `absolute flex flex-col justify-center overflow-hidden rounded-md pl-2.5 text-left text-xs leading-tight whitespace-nowrap transition-opacity before:rounded-l-md hover:brightness-95 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none ${style.box} ${style.strip}${overload ? ' outline outline-1 -outline-offset-1 outline-destructive' : ''}`;
          return {
            w: x, left, width, y: rowTop + rowPad + x.lane * LANE_H, height: BAR_H, lane: x.lane, rowKey: r.key, overload, dim, late, stale, level, inside: true, chip: null,
            statusIcon: st.icon, name, segCode: x.processSegmentCode || name, sub, color: orderColor(hues.get(x.productionOrderId) ?? 0),
            padRight: width >= 56 && markers ? markers * 18 + 4 : 4, cls, aria,
            tip: {
              phase: name, workOrder: x.code, order: `${x.productionOrderCode} · ${x.materialName}`, machine, period,
              duration: formatDuration(rg.e - rg.s), status, plan, predecessors: x.predecessorIds.length,
            },
          };
        })
        .sort((a, b) => a.left - b.left);
      const bands = r.key === NONE_ROW ? [] : nw.filter((n) => n.equipmentId == null || n.equipmentId === r.key).flatMap((n) => this.band(n, ax, w));
      this.placeChips(bars, ax.width);
      const cap = rl.reduce((a, l) => a + l.capacityMinutes, 0);
      const busy = rl.reduce((a, l) => a + l.busyMinutes, 0);
      const summary = r.key === NONE_ROW || !cap ? null : Math.round((busy / cap) * 100);
      return { ...r, top: rowTop, height, bars, bands, marks, summary, summaryFill: Math.min(100, summary ?? 0) };
    });
  });

  /** Rettangoli di tutte le barre (non limitate al viewport) per id lavoro: base per l'overlay delle dipendenze (blocco 2b). */
  readonly barGeometry = computed<ReadonlyMap<number, BarRect>>(() => {
    const m = new Map<number, BarRect>();
    for (const r of this.allRows()) for (const b of r.bars) m.set(b.w.id, { x: b.left, y: b.y, w: b.width, h: b.height, rowKey: b.rowKey });
    return m;
  });

  protected readonly rowsH = computed(() => {
    const r = this.allRows();
    const last = r[r.length - 1];
    return last ? last.top + last.height : 0;
  });

  /** Lavoro "attivo" per le dipendenze: sotto il mouse, col focus, altrimenti selezionato. */
  private readonly activeId = computed(() => this.hoverId() ?? this.focusedId() ?? this.selectedId());
  private readonly chain = computed<{ up: Set<number>; down: Set<number>; all: Set<number> } | null>(() => {
    const id = this.activeId();
    if (id == null) return null;
    const c = chainOf(this.graph(), id);
    return { ...c, all: new Set([id, ...c.up, ...c.down]) };
  });

  /** Connettori di tutti i lavori caricati (geometria memoizzata; il filtro per viewport/modalità è in `drawn`). */
  private readonly depEdges = computed<DepEdge[]>(() => {
    const d = this.data();
    if (!d) return [];
    const geo = this.barGeometry();
    const pool = this.pool();
    const hues = this.orderHue();
    const out: DepEdge[] = [];
    for (const succ of d.workOrders) {
      const s = geo.get(succ.id);
      if (!s) continue;
      for (const pid of succ.predecessorIds) {
        const p = geo.get(pid);
        const pw = pool.get(pid);
        const cross = !!pw && pw.productionOrderId !== succ.productionOrderId;
        const stroke = cross ? orderColor(hues.get(succ.productionOrderId) ?? 0) : 'var(--foreground)';
        const key = `${pid}-${succ.id}`;
        if (isOffRange(p)) {
          const r = stubRoute(s);
          out.push({ key, predId: pid, succId: succ.id, d: r.d, stroke, dash: cross, conflict: false, warn: null, label: { x: r.points[0].x - 3, y: r.points[0].y + 3.5, text: pw?.code ?? `#${pid}` }, minX: r.points[0].x, maxX: s.x });
          continue;
        }
        const r = routeConnector(p as BarRect, s);
        const pr = pw ? this.range(pw) : null;
        const sr = this.range(succ);
        const conflict = !!pr && !!sr && sr.s < pr.e;
        const xs = r.points.map((q) => q.x);
        const mid = r.points[Math.min(2, r.points.length - 1)];
        out.push({
          key, predId: pid, succId: succ.id, d: r.d, stroke: conflict ? 'var(--destructive)' : stroke, dash: cross, conflict,
          warn: conflict ? { x: mid.x, y: mid.y } : null, label: null, minX: Math.min(...xs), maxX: Math.max(...xs),
        });
      }
    }
    return out;
  });

  protected readonly drawn = computed<DepDrawn[]>(() => {
    const { from, to } = this.vp();
    const act = this.activeId();
    const ch = this.chain();
    const all = this.depsAll();
    const out: DepDrawn[] = [];
    for (const e of this.depEdges()) {
      if (e.maxX < from || e.minX > to) continue;
      const inChain = !!ch && ch.all.has(e.predId) && ch.all.has(e.succId);
      if (!all && !inChain) continue;
      const direct = act != null && (e.predId === act || e.succId === act);
      out.push({ ...e, opacity: all ? (inChain ? 1 : 0.5) : direct ? 1 : 0.6, width: all ? (inChain ? 2.25 : 1) : direct ? 2 : 1.5 });
    }
    // Le linee evidenziate sopra le altre.
    return out.sort((a, b) => a.opacity - b.opacity);
  });

  protected chainDim(b: BarVm): boolean {
    const ch = this.chain();
    return !!ch && !this.depsAll() && !ch.all.has(b.w.id);
  }
  protected chainRing(b: BarVm): boolean {
    const ch = this.chain();
    return !!ch && ch.all.size > 1 && ch.all.has(b.w.id) && this.selectedId() !== b.w.id;
  }

  /** Predecessori e successori diretti del lavoro selezionato per il pannello, con scarto fine→inizio. */
  protected readonly links = computed<{ preds: DepLink[]; succs: DepLink[]; up: number; down: number } | null>(() => {
    const sel = this.selected();
    if (!sel) return null;
    const pool = this.pool();
    const g = this.graph();
    const me = this.range(sel);
    const mk = (id: number, kind: 'pred' | 'succ'): DepLink => {
      const o = pool.get(id);
      const or = o ? this.range(o) : null;
      const from = kind === 'pred' ? (or?.e ?? null) : (me?.e ?? null);
      const to = kind === 'pred' ? (me?.s ?? null) : (or?.s ?? null);
      const gap = from != null && to != null ? Math.round((from - to) / 60_000) : 0;
      return {
        id, code: o?.code ?? `#${id}`, status: o?.status ?? null, phase: o ? (o.processSegmentName || o.processSegmentCode) : null,
        startMs: or?.s ?? null, endMs: or?.e ?? null, fromMs: from, toMs: to, conflictMin: gap > 0 ? gap : null,
      };
    };
    const c = chainOf(g, sel.id);
    return {
      preds: (g.preds.get(sel.id) ?? sel.predecessorIds).map((i) => mk(i, 'pred')),
      succs: (g.succs.get(sel.id) ?? []).map((i) => mk(i, 'succ')),
      up: c.up.size,
      down: c.down.size,
    };
  });

  /** Etichette fuori barra (Gantt) per le barre strette: mai sopra altre barre o etichette della stessa corsia. */
  private placeChips(bars: BarVm[], maxRight: number): void {
    const lanes = new Map<number, BarVm[]>();
    for (const b of bars) lanes.set(b.lane, [...(lanes.get(b.lane) ?? []), b]);
    for (const list of lanes.values()) {
      let used = 0;
      list.forEach((b, i) => {
        const next = list[i + 1];
        const labelW = Math.max(estimateTextWidth(b.name, 6.8), estimateTextWidth(b.sub, 6)) + CHIP_EXTRA_FULL;
        const codeW = estimateTextWidth(b.segCode, 6.8) + CHIP_EXTRA_CODE;
        const res = placeLabel({
          left: b.left, width: b.width, prevEnd: used, nextStart: next ? next.left : Infinity, labelW,
          codeW, maxRight, insideMin: LABEL_INSIDE_MIN,
        });
        used = Math.max(used, b.left + b.width);
        if (res.placement === 'inside' || res.placement === 'none' || !res.side) return;
        const w = res.placement === 'code' ? codeW : labelW;
        const chip: ChipVm = { left: res.side === 'right' ? b.left + b.width + 4 : b.left - 4, end: res.side === 'left', text: res.placement === 'code' ? b.segCode : b.name, sub: res.placement === 'code' ? null : b.sub, fullW: labelW, codeW, code: b.segCode };
        b.chip = chip;
        b.inside = false;
        if (res.side === 'right') used = Math.max(used, chip.left + w);
      });
    }
  }

  private band(n: TimelineNonWorkingModel, ax: Axis, w: { start: number; end: number }): BandVm[] {
    const s = Math.max(new Date(n.start).getTime(), w.start);
    const e = Math.min(new Date(n.end).getTime(), w.end);
    const left = ax.toX(s);
    const width = ax.toX(e) - left;
    // Fascia interamente dentro un intervallo compresso: coperta dal separatore.
    if (width < 1 || (width <= SEP_W + 0.01 && ax.isCollapsed(s + 1) && ax.isCollapsed(e - 1))) return [];
    const hatch = 'bg-[repeating-linear-gradient(45deg,transparent,transparent_5px,var(--border)_5px,var(--border)_6px)]';
    const cls = n.kind === 'Weekly' ? 'bg-muted/50' : n.kind === 'CalendarException' ? `bg-muted/30 ${hatch}` : `bg-muted/40 ${hatch} border-x border-dashed`;
    const label = n.kind === 'Weekly' ? 'Non lavorativo' : n.kind === 'CalendarException' ? 'Eccezione calendario' : 'Eccezione macchina';
    return [{ left, width, reason: n.reason ? `${label}: ${n.reason}` : label, cls }];
  }

  protected readonly legendOrders = computed(() => {
    const d = this.data();
    if (!d) return [];
    const hues = this.orderHue();
    const seen = new Map<number, { id: number; code: string; short: string; material: string; idx: number; color: string }>();
    for (const w of d.workOrders) {
      if (seen.has(w.productionOrderId)) continue;
      const idx = hues.get(w.productionOrderId) ?? 0;
      seen.set(w.productionOrderId, { id: w.productionOrderId, code: w.productionOrderCode, short: w.productionOrderCode.replace(/^OP-\d{4}-/, ''), material: w.materialName, idx, color: orderColor(idx) });
    }
    return [...seen.values()].sort((a, b) => a.code.localeCompare(b.code));
  });

  /** Attenua la barra se non corrisponde alla ricerca o a un ordine evidenziato/sotto il mouse. */
  protected dimmed(b: BarVm): boolean {
    const focus = this.hoverOrder() ?? this.highlight();
    return b.dim || (focus != null && b.w.productionOrderId !== focus);
  }
  protected toggleHighlight(id: number): void {
    this.highlight.update((h) => (h === id ? null : id));
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

    const ro = new ResizeObserver(([e]) => this.hostW.set(Math.round(e.contentRect.width)));
    ro.observe(this.host.nativeElement);
    this.hostW.set(this.host.nativeElement.clientWidth);
    this.destroyRef.onDestroy(() => ro.disconnect());

    void this.loadProbe(true);

    effect(() => {
      const req = this.store.focusRequest();
      if (req) untracked(() => this.focusRequested(req.workOrderId, req.productionOrderId));
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
        deps: this.depsAll() ? 'all' : null,
        wo: this.selectedId(),
      };
      untracked(() => void this.router.navigate([], { relativeTo: this.route, queryParams: params, queryParamsHandling: 'merge', replaceUrl: true }));
    });

    // Posizione dello scroll solo quando cambia la finestra (o compare l'area scorrevole la prima volta): mai per la selezione.
    effect(() => {
      this.win();
      const el = this.scroller()?.nativeElement;
      if (!el) return;
      this.scrollInitial = true;
      setTimeout(() => this.applyInitialScroll(), 0);
    });
  }

  private scrollInitial = false;

  /** Prima posizione di scroll di una finestra: adesso, altrimenti il primo lavoro pianificato (dopo il caricamento: l'asse cambia coi dati). */
  private applyInitialScroll(): void {
    const el = this.scroller()?.nativeElement;
    if (!el || this.pendingScroll != null) return;
    const ax = this.axis();
    const np = this.nowPx();
    let left = 0;
    if (this.zoom() === 'day') left = Math.max(0, ax.toX(ax.win.start + 6 * HOUR) - 8);
    else if (np != null) left = Math.max(0, np - 120);
    else {
      const first = this.allRows().flatMap((r) => r.bars).reduce((m, b) => Math.min(m, b.left), Infinity);
      if (Number.isFinite(first)) left = Math.max(0, first - 80);
    }
    el.scrollLeft = left;
    this.onScroll();
  }

  /** Scorrimento orizzontale corrente: serve a non lasciare etichette a sinistra sotto la colonna macchina sticky. */
  private readonly scrollX = signal(0);

  /** Chip effettivamente mostrabile: a sinistra non finisce sotto la colonna macchina sticky (scende a solo codice o sparisce). */
  protected chipView(ch: ChipVm): { text: string; sub: string | null } | null {
    if (!ch.end) return { text: ch.text, sub: ch.sub };
    const fit = fitLeftChip(ch.left, ch.sub ? ch.fullW : ch.codeW, ch.codeW, this.scrollX());
    if (fit === 'full') return { text: ch.text, sub: ch.sub };
    return fit === 'code' ? { text: ch.code, sub: null } : null;
  }

  protected onScroll(): void {
    const el = this.scroller()?.nativeElement;
    if (!el) return;
    this.scrollX.set(el.scrollLeft);
    const bucket = 300;
    const from = Math.floor(Math.max(0, el.scrollLeft - 400) / bucket) * bucket;
    const to = Math.ceil((el.scrollLeft + el.clientWidth + 400) / bucket) * bucket;
    const cur = this.vp();
    if (cur.from !== from || cur.to !== to) this.vp.set({ from, to });
  }

  /** Finestra ampia (-30/+90 giorni) in due richieste entro il limite di 62 giorni. */
  private async loadProbe(initial = false): Promise<void> {
    const tz = this.tz();
    const today = dayStart(dayKey(Date.now(), tz), tz);
    const at = (days: number) => new Date(today + days * 24 * HOUR).toISOString();
    const parts = await Promise.allSettled([
      firstValueFrom(this.api.getPlanningTimeline({ from: at(-30), to: at(30) })),
      firstValueFrom(this.api.getPlanningTimeline({ from: at(30), to: at(90) })),
    ]);
    const all = parts.flatMap((p) => (p.status === 'fulfilled' ? p.value.workOrders : []));
    // La seconda richiesta può ripetere lavori a cavallo del confine.
    this.probe.set([...new Map(all.map((w) => [w.id, w])).values()]);
    // Posizione iniziale: una sola volta all'avvio, mai per selezione o parametri URL.
    if (initial && !this.autoStartDone) {
      this.autoStartDone = true;
      if (!this.qp.get('from')) this.autoStart(today);
    }
  }

  /** Al primo caricamento: se la finestra corrente è vuota ma c'è lavoro nei 14 giorni seguenti, parte da lì. */
  private autoStart(today: number): void {
    const w = this.win();
    const inWin = this.probe().some((x) => {
      const r = this.range(x);
      return r && r.e > w.start && r.s < w.end;
    });
    if (inWin) return;
    const limit = today + 14 * 24 * HOUR;
    const starts = this.probe().flatMap((x) => (x.plannedStartAt ? [new Date(x.plannedStartAt).getTime()] : [])).filter((t) => t >= w.end && t <= limit);
    if (starts.length) this.cursor.set(dayKey(Math.min(...starts), this.tz()));
  }

  protected goTo(ms: number): void {
    this.cursor.set(dayKey(ms, this.tz()));
  }

  /** Seleziona un lavoro (anche fuori finestra) e sposta la finestra solo se il lavoro non è visibile. */
  focusWorkOrder(id: number): void {
    const known = this.pool().get(id);
    if (known && this.range(known)) {
      this.focusRequested(id, null);
      return;
    }
    void this.orders.getDetail(id).then((d) => {
      const iso = d.status === 'Running' || d.status === 'Ended' ? (d.startedAt ?? d.plannedStartAt) : d.plannedStartAt;
      if (!iso) return;
      this.pendingSelect = id;
      const before = this.win().start;
      this.cursor.set(dayKey(new Date(iso).getTime(), this.tz()));
      if (this.win().start === before) this.reload();
    }, () => undefined);
  }

  private focusRequested(workOrderId: number | null, orderId: number | null): void {
    const pool = [...(this.data()?.workOrders ?? []), ...this.probe()];
    const dated = (x: TimelineWorkOrderModel) => !!this.range(x);
    const target =
      (workOrderId != null ? pool.find((x) => x.id === workOrderId && dated(x)) : undefined) ??
      (orderId != null ? pool.filter((x) => x.productionOrderId === orderId && dated(x)).sort((a, b) => (this.range(a)?.s ?? 0) - (this.range(b)?.s ?? 0))[0] : undefined);
    if (!target) return;
    const filtersChanged = this.filtersActive();
    if (filtersChanged) this.clearFilters();
    this.select(target);
    const r = this.range(target);
    const w = this.win();
    const visible = !!r && r.e > w.start && r.s < w.end;
    this.pendingScroll = target.id;
    // Lo zoom resta: la finestra cambia solo se il lavoro non è in quella corrente.
    if (!visible && r) this.cursor.set(dayKey(r.s, this.tz()));
    if (!filtersChanged && visible) this.scrollToPending();
  }

  protected onSheetState(state: string): void {
    if (state !== 'closed') return;
    const id = this.selectedId();
    this.selectedId.set(null);
    // Il focus torna alla barra che aveva aperto il pannello.
    if (id != null) setTimeout(() => this.scroller()?.nativeElement.querySelector<HTMLElement>(`[data-bar="${id}"]`)?.focus(), 30);
  }

  private scrollToPending(): void {
    const id = this.pendingScroll;
    if (id == null) return;
    setTimeout(() => {
      const el = this.scroller()?.nativeElement;
      const w = this.data()?.workOrders.find((x) => x.id === id) ?? this.probe().find((x) => x.id === id);
      const r = w ? this.range(w) : null;
      if (!el || !r) return;
      this.pendingScroll = null;
      const left = this.axis().toX(r.s);
      el.scrollLeft = Math.max(0, left - 80);
      el.querySelector<HTMLElement>(`[data-row="${w!.equipmentId ?? NONE_ROW}"]`)?.scrollIntoView({ block: 'nearest' });
      this.onScroll();
    }, 60);
  }

  protected reload(): void {
    void this.loadProbe();
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
      const hues = new Map(this.orderHue());
      for (const w of d.workOrders) if (!hues.has(w.productionOrderId)) hues.set(w.productionOrderId, hues.size % 8);
      this.orderHue.set(hues);
      this.data.set(d);
      if (this.scrollInitial) {
        this.scrollInitial = false;
        setTimeout(() => this.applyInitialScroll(), 0);
      }
      const known = new Map(this.knownOrders());
      for (const w of d.workOrders) known.set(w.productionOrderId, w.productionOrderCode);
      this.knownOrders.set(known);
      if (this.pendingSelect != null) {
        const target = d.workOrders.find((w) => w.id === this.pendingSelect);
        if (target) {
          this.pendingScroll = target.id;
          this.select(target);
          this.pendingSelect = null;
        }
      }
      const sel = this.selectedId();
      const found = sel != null ? d.workOrders.find((w) => w.id === sel) : null;
      if (found) this.lastSelected.set(found);
      this.onScroll();
      if (this.pendingScroll != null) this.scrollToPending();
    } catch (e) {
      if (n === this.seq) this.error.set(toFailure(e).message);
    } finally {
      if (n === this.seq) this.loading.set(false);
    }
  }

  // --- navigazione ---
  protected setZoom(z: Zoom): void {
    this.zoom.set(z);
    this.pph.set(DEFAULT_PPH[z]);
  }
  protected density(dir: 1 | -1): void {
    this.pph.update((p) => clampPph(dir > 0 ? p * 1.25 : p / 1.25));
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
