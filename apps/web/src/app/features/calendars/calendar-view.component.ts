import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, Injector, input, afterNextRender, signal, untracked, OnInit } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideBan, lucideCalendarOff, lucideChevronLeft, lucideChevronRight, lucideCircleCheck, lucideClock, lucideTriangleAlert, lucideWrench } from '@ng-icons/lucide';
import type { CalendarDayModel, CalendarDayStatus } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { EquipmentStore } from '../equipment/equipment.store';
import { toFailure } from '../materials/materials.service';
import { formatHours, fromIsoDate, slotModelsToRanges, summarizeRanges, toIsoDate, toMinutes, toHHmm } from './calendar-time';
import { CalendarsStore } from './calendars.store';

const COMPANY = 0;
const AXIS_START = 6 * 60;
const AXIS_END = 22 * 60;
const AXIS_HOURS = [6, 8, 10, 12, 14, 16, 18, 20, 22];

const addDays = (d: Date, n: number): Date => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);
const mondayOf = (d: Date): Date => addDays(d, -((d.getDay() + 6) % 7));
const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);
const LONG_DAY = new Intl.DateTimeFormat('it-IT', { weekday: 'long', day: 'numeric', month: 'long' });
const FULL_DAY = new Intl.DateTimeFormat('it-IT', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
const MONTH_FMT = new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric' });
const MONTH_ONLY = new Intl.DateTimeFormat('it-IT', { month: 'long' });
const SHORT_DAYS = ['Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab', 'Dom'];

interface Cell {
  iso: string;
  date: Date;
  inMonth: boolean;
  day: CalendarDayModel | undefined;
}

type Presentation = { label: string; icon: string; box: string };

const PRESENTATION: Record<CalendarDayStatus, Presentation> = {
  Working: { label: 'Lavorativo', icon: 'lucideCircleCheck', box: 'bg-card' },
  NonWorking: { label: 'Non lavorativo', icon: 'lucideCalendarOff', box: 'bg-muted text-muted-foreground' },
  Closed: { label: 'Chiuso', icon: 'lucideBan', box: 'border-destructive/40 bg-destructive/10 text-destructive' },
  Custom: { label: 'Orario speciale', icon: 'lucideClock', box: 'border-primary/40 bg-primary/10 text-primary' },
};

/** Vista a calendario in sola lettura (mese/settimana) di un calendario aziendale o di una macchina. */
@Component({
  selector: 'app-calendar-view',
  imports: [HlmButton, HlmLabel, HlmSelectImports, HlmSkeleton, HlmTabsImports, NgIcon],
  providers: [provideIcons({ lucideBan, lucideCalendarOff, lucideChevronLeft, lucideChevronRight, lucideCircleCheck, lucideClock, lucideTriangleAlert, lucideWrench })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-3">
      <div class="flex flex-wrap items-end gap-3">
        @if (!lockScope()) {
          <div class="grid gap-1.5">
            <label hlmLabel for="cv-scope">Ambito</label>
            <hlm-select [value]="scope()" [itemToString]="scopeLabel" (valueChange)="setScope($event ?? 0)">
              <hlm-select-trigger id="cv-scope" size="sm" class="w-64" aria-label="Ambito del calendario">
                <hlm-select-value />
              </hlm-select-trigger>
              <hlm-select-content *hlmSelectPortal>
                <hlm-select-item [value]="0">Calendario aziendale</hlm-select-item>
                @for (m of machines(); track m.id) {
                  <hlm-select-item [value]="m.id">{{ m.code }} — {{ m.name }}</hlm-select-item>
                }
              </hlm-select-content>
            </hlm-select>
          </div>
        }
        <div class="flex items-center gap-1" role="group" aria-label="Navigazione">
          <button hlmBtn variant="outline" size="icon-sm" type="button" [attr.aria-label]="mode() === 'month' ? 'Mese precedente' : 'Settimana precedente'" (click)="step(-1)"><ng-icon name="lucideChevronLeft" /></button>
          <button hlmBtn variant="outline" size="sm" type="button" (click)="today()">Oggi</button>
          <button hlmBtn variant="outline" size="icon-sm" type="button" [attr.aria-label]="mode() === 'month' ? 'Mese successivo' : 'Settimana successiva'" (click)="step(1)"><ng-icon name="lucideChevronRight" /></button>
        </div>
        <h3 class="min-w-44 text-base font-semibold" aria-live="polite">{{ title() }}</h3>
        <hlm-tabs class="ml-auto" [tab]="mode()" (tabActivated)="setMode($any($event))">
          <hlm-tabs-list aria-label="Tipo di vista">
            <button hlmTabsTrigger="month" type="button">Mese</button>
            <button hlmTabsTrigger="week" type="button">Settimana</button>
          </hlm-tabs-list>
        </hlm-tabs>
      </div>

      @if (scopeInfo(); as i) {
        <p class="text-muted-foreground text-sm">{{ i }}</p>
      }

      @if (error()) {
        <p class="text-destructive border-destructive/40 flex items-center gap-2 rounded-md border px-3 py-2 text-sm" role="alert">
          <ng-icon name="lucideTriangleAlert" aria-hidden="true" /> {{ error() }}
          <button hlmBtn class="ml-auto" size="sm" variant="outline" type="button" (click)="reload()">Riprova</button>
        </p>
      }

      @if (mode() === 'month') {
        <div class="rounded-md border" [attr.aria-busy]="loading()">
          <div class="grid grid-cols-7 border-b" aria-hidden="true">
            @for (d of shortDays; track d) {
              <div class="text-muted-foreground px-2 py-1.5 text-xs font-medium">{{ d }}</div>
            }
          </div>
          <div class="grid grid-cols-7" role="group" [attr.aria-label]="title()" (keydown)="onKey($event)">
            @for (c of cells(); track c.iso) {
              @let p = presentation(c);
              <button
                type="button"
                class="focus-visible:ring-ring/50 relative flex min-h-24 flex-col items-start gap-1 border-r border-b p-1.5 text-left text-xs outline-none focus-visible:z-10 focus-visible:ring-[3px] [&:nth-child(7n)]:border-r-0"
                [class]="p.box"
                [class.opacity-60]="!c.inMonth"
                [class.ring-2]="c.iso === selectedIso()"
                [class.ring-ring]="c.iso === selectedIso()"
                [attr.data-date]="c.iso"
                [attr.tabindex]="c.iso === focusedIso() ? 0 : -1"
                [attr.aria-label]="cellLabel(c)"
                [attr.aria-pressed]="c.iso === selectedIso()"
                [attr.aria-current]="c.iso === todayIso ? 'date' : null"
                (click)="select(c.iso)"
                (focus)="focusedIso.set(c.iso)"
              >
                <span class="flex w-full items-center justify-between">
                  <span class="text-sm font-semibold tabular-nums" [class.bg-primary]="c.iso === todayIso" [class.text-primary-foreground]="c.iso === todayIso" [class.rounded-full]="c.iso === todayIso" [class.px-1.5]="c.iso === todayIso">{{ c.date.getDate() }}</span>
                  @if (c.day) {
                    <span class="flex items-center gap-1">
                      @if (c.day.exceptionScope === 'Equipment') {
                        <ng-icon name="lucideWrench" class="size-3.5" aria-hidden="true" />
                      }
                      <ng-icon [name]="p.icon" class="size-3.5" aria-hidden="true" />
                    </span>
                  }
                </span>
                @if (c.day; as d) {
                  <span class="line-clamp-2 leading-tight font-medium">{{ cellText(d) }}</span>
                  @if (d.exceptionScope === 'Equipment') {
                    <span class="leading-tight opacity-80">Macchina</span>
                  }
                } @else if (loading()) {
                  <hlm-skeleton class="h-3 w-3/4" />
                }
              </button>
            }
          </div>
        </div>
      } @else {
        <div class="grid grid-cols-[2.5rem_repeat(7,minmax(0,1fr))] gap-x-1 rounded-md border p-2" [attr.aria-busy]="loading()">
          <div></div>
          @for (c of weekCells(); track c.iso) {
            @let p = presentation(c);
            <div class="flex flex-col gap-0.5 rounded-t-md px-1.5 py-1 text-xs" [class]="p.box">
              <span class="flex items-center justify-between gap-1 font-semibold">
                <span [class.underline]="c.iso === todayIso">{{ weekHead(c) }}</span>
                @if (c.day) { <ng-icon [name]="p.icon" class="size-3.5" aria-hidden="true" /> }
              </span>
              @if (c.day; as d) {
                <span class="line-clamp-2 leading-tight">{{ cellText(d) }}</span>
                @if (d.exceptionScope === 'Equipment') {
                  <span class="inline-flex items-center gap-1 opacity-80"><ng-icon name="lucideWrench" class="size-3" aria-hidden="true" /> Macchina</span>
                }
              } @else {
                <span>&nbsp;</span>
              }
            </div>
          }
          <div class="relative h-96" aria-hidden="true">
            @for (h of axis; track h) {
              <span class="text-muted-foreground absolute right-1 -translate-y-1/2 text-[10px] tabular-nums" [style.top.%]="((h * 60 - axisStart) / axisSpan) * 100">{{ h }}:00</span>
            }
          </div>
          @for (c of weekCells(); track c.iso) {
            <div class="bg-muted/30 relative h-96 rounded-b-md border" role="img" [attr.aria-label]="weekLabel(c)">
              @for (h of axis; track h) {
                <div class="border-border/60 absolute inset-x-0 border-t" [style.top.%]="((h * 60 - axisStart) / axisSpan) * 100"></div>
              }
              @if (c.day) {
                @for (b of bars(c.day); track b.key) {
                  <div class="border-primary bg-primary/20 text-primary absolute inset-x-1 overflow-hidden rounded-sm border px-1 text-[10px] font-medium tabular-nums" [style.top.%]="b.top" [style.height.%]="b.height">{{ b.text }}</div>
                }
              }
            </div>
          }
        </div>
      }

      <p class="text-muted-foreground flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
        <span class="inline-flex items-center gap-1"><ng-icon name="lucideCircleCheck" aria-hidden="true" /> Lavorativo</span>
        <span class="inline-flex items-center gap-1"><ng-icon name="lucideCalendarOff" aria-hidden="true" /> Non lavorativo</span>
        <span class="inline-flex items-center gap-1"><ng-icon name="lucideBan" aria-hidden="true" /> Chiuso</span>
        <span class="inline-flex items-center gap-1"><ng-icon name="lucideClock" aria-hidden="true" /> Orario speciale</span>
        <span class="inline-flex items-center gap-1"><ng-icon name="lucideWrench" aria-hidden="true" /> Eccezione della macchina</span>
      </p>

      @if (mode() === 'month') {
        @if (selectedDay(); as d) {
          <section class="flex flex-col gap-1.5 rounded-md border p-4" aria-labelledby="cv-day-title" aria-live="polite">
            <h4 id="cv-day-title" class="text-sm font-semibold">{{ fullDate(d.date) }}</h4>
            <p class="flex items-center gap-1.5 text-sm">
              <ng-icon [name]="presentationOf(d).icon" aria-hidden="true" /> {{ presentationOf(d).label }}
              @if (d.exceptionScope) {
                <span class="text-muted-foreground">· {{ d.exceptionScope === 'Equipment' ? 'eccezione della macchina' : 'eccezione del calendario' }}</span>
              }
            </p>
            @if (d.reason) {
              <p class="text-sm">Motivo: <span class="font-medium">{{ d.reason }}</span></p>
            }
            <p class="text-sm tabular-nums">
              @if (d.slots.length) {
                Fasce: {{ slotsText(d) }} ({{ hours(d) }})
              } @else {
                <span class="text-muted-foreground">Nessuna fascia di lavoro.</span>
              }
            </p>
          </section>
        } @else {
          <p class="text-muted-foreground text-sm">Seleziona un giorno per vedere fasce ed eccezioni.</p>
        }
      }
    </div>
  `,
})
export class CalendarViewComponent implements OnInit {
  private readonly store = inject(CalendarsStore);
  private readonly equipment = inject(EquipmentStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** Calendario aziendale mostrato quando l'ambito è «Calendario aziendale». */
  readonly calendarId = input<number | null>(null);
  /** Macchina iniziale (se impostata l'ambito parte dalla macchina). */
  readonly equipmentId = input<number | null>(null);
  /** Nasconde il selettore d'ambito (vista dedicata a una macchina). */
  readonly lockScope = input(false);

  protected readonly shortDays = SHORT_DAYS;
  protected readonly axis = AXIS_HOURS;
  protected readonly axisStart = AXIS_START;
  protected readonly axisSpan = AXIS_END - AXIS_START;
  protected readonly todayIso = toIsoDate(new Date());

  protected readonly scope = signal(COMPANY);
  protected readonly mode = signal<'month' | 'week'>('month');
  protected readonly anchor = signal(new Date());
  protected readonly focusedIso = signal(this.todayIso);
  protected readonly selectedIso = signal<string | null>(null);
  protected readonly days = signal<Map<string, CalendarDayModel>>(new Map());
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly machines = computed(() => this.equipment.items().filter((m) => m.isActive));
  protected readonly scopeLabel = (v: number | null): string => {
    if (!v) return 'Calendario aziendale';
    const m = this.equipment.items().find((x) => x.id === v);
    return m ? `${m.code} — ${m.name}` : '';
  };

  protected readonly title = computed(() => {
    const a = this.anchor();
    if (this.mode() === 'month') return cap(MONTH_FMT.format(a));
    const mon = mondayOf(a);
    const sun = addDays(mon, 6);
    return mon.getMonth() === sun.getMonth()
      ? `${mon.getDate()} – ${sun.getDate()} ${MONTH_ONLY.format(sun)} ${sun.getFullYear()}`
      : `${mon.getDate()} ${MONTH_ONLY.format(mon)} – ${sun.getDate()} ${MONTH_ONLY.format(sun)} ${sun.getFullYear()}`;
  });

  protected readonly scopeInfo = computed(() => {
    const id = this.scope();
    if (!id) return 'Calendario aziendale con le sue eccezioni.';
    const m = this.equipment.items().find((x) => x.id === id);
    const c = m?.effectiveCalendar;
    const base = c ? `Calendario effettivo: ${c.name}${c.isInherited ? ' (predefinito aziendale)' : ''}.` : '';
    return `${base} Le eccezioni aziendali e quelle della macchina sono distinte; quelle della macchina prevalgono.`.trim();
  });

  private readonly range = computed(() => {
    const a = this.anchor();
    if (this.mode() === 'month') {
      const first = mondayOf(new Date(a.getFullYear(), a.getMonth(), 1));
      const last = addDays(mondayOf(new Date(a.getFullYear(), a.getMonth() + 1, 0)), 6);
      return { from: first, to: last };
    }
    const mon = mondayOf(a);
    return { from: mon, to: addDays(mon, 6) };
  });

  private cellsOf(from: Date, to: Date, month: number | null): Cell[] {
    const out: Cell[] = [];
    const map = this.days();
    for (let d = from; d <= to; d = addDays(d, 1)) {
      const iso = toIsoDate(d);
      out.push({ iso, date: d, inMonth: month === null || d.getMonth() === month, day: map.get(iso) });
    }
    return out;
  }

  protected readonly cells = computed(() => this.cellsOf(this.range().from, this.range().to, this.anchor().getMonth()));
  protected readonly weekCells = computed(() => this.cellsOf(this.range().from, this.range().to, null));
  protected readonly selectedDay = computed(() => {
    const iso = this.selectedIso();
    return iso ? this.days().get(iso) ?? null : null;
  });

  private seq = 0;

  constructor() {
    effect(() => {
      const r = this.range();
      this.scope();
      this.calendarId();
      untracked(() => void this.load(r.from, r.to));
    });
  }

  ngOnInit(): void {
    const eq = this.equipmentId();
    if (eq) this.scope.set(eq);
    if (!this.equipment.items().length) void this.equipment.load();
  }

  reload(): void {
    void this.load(this.range().from, this.range().to);
  }

  private async load(from: Date, to: Date): Promise<void> {
    const n = ++this.seq;
    const scope = this.scope();
    const cal = this.calendarId();
    if (!scope && cal === null) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const f = toIsoDate(from);
      const t = toIsoDate(to);
      const list = scope ? await this.store.equipmentDays(scope, f, t) : await this.store.days(cal as number, f, t);
      if (n === this.seq) this.days.set(new Map(list.map((d) => [d.date, d])));
    } catch (e) {
      if (n === this.seq) {
        this.days.set(new Map());
        this.error.set(toFailure(e).message);
      }
    } finally {
      if (n === this.seq) this.loading.set(false);
    }
  }

  protected setScope(v: number): void {
    this.scope.set(v);
    this.selectedIso.set(null);
  }

  protected setMode(m: 'month' | 'week'): void {
    if (m === this.mode()) return;
    const f = fromIsoDate(this.focusedIso());
    this.mode.set(m);
    this.anchor.set(f);
  }

  protected step(dir: 1 | -1): void {
    const a = this.anchor();
    const next = this.mode() === 'month' ? new Date(a.getFullYear(), a.getMonth() + dir, 1) : addDays(a, 7 * dir);
    this.anchor.set(next);
    this.focusedIso.set(toIsoDate(next));
  }

  protected today(): void {
    const t = new Date();
    this.anchor.set(t);
    this.focusedIso.set(toIsoDate(t));
  }

  protected select(iso: string): void {
    this.focusedIso.set(iso);
    this.selectedIso.set(iso);
  }

  protected onKey(ev: KeyboardEvent): void {
    const cur = fromIsoDate(this.focusedIso());
    let next: Date | null = null;
    switch (ev.key) {
      case 'ArrowLeft': next = addDays(cur, -1); break;
      case 'ArrowRight': next = addDays(cur, 1); break;
      case 'ArrowUp': next = addDays(cur, -7); break;
      case 'ArrowDown': next = addDays(cur, 7); break;
      case 'Home': next = mondayOf(cur); break;
      case 'End': next = addDays(mondayOf(cur), 6); break;
      case 'PageUp': next = new Date(cur.getFullYear(), cur.getMonth() - 1, Math.min(cur.getDate(), 28)); break;
      case 'PageDown': next = new Date(cur.getFullYear(), cur.getMonth() + 1, Math.min(cur.getDate(), 28)); break;
      default: return;
    }
    ev.preventDefault();
    this.focusedIso.set(toIsoDate(next));
    if (next.getMonth() !== this.anchor().getMonth() || next.getFullYear() !== this.anchor().getFullYear()) this.anchor.set(next);
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(`[data-date="${toIsoDate(next as Date)}"]`)?.focus(), { injector: this.injector });
  }

  protected presentationOf(d: CalendarDayModel): Presentation {
    return PRESENTATION[d.status];
  }

  protected presentation(c: Cell): Presentation {
    return c.day ? PRESENTATION[c.day.status] : { label: '', icon: '', box: 'bg-card' };
  }

  protected hours(d: CalendarDayModel): string {
    return formatHours(d.slots.reduce((s, x) => s + toMinutes(x.endTime) - toMinutes(x.startTime), 0));
  }

  protected slotsText(d: CalendarDayModel): string {
    return summarizeRanges(slotModelsToRanges(d.slots));
  }

  protected cellText(d: CalendarDayModel): string {
    switch (d.status) {
      case 'Working': return this.hours(d);
      case 'NonWorking': return 'Non lavorativo';
      case 'Closed': return d.reason || 'Chiuso';
      case 'Custom': return `${this.hours(d)} · ${d.reason || 'orario speciale'}`;
    }
  }

  protected cellLabel(c: Cell): string {
    const base = LONG_DAY.format(c.date);
    const d = c.day;
    if (!d) return base;
    const p = PRESENTATION[d.status].label.toLowerCase();
    const where = d.exceptionScope === 'Equipment' ? ' (eccezione della macchina)' : '';
    const detail = d.status === 'Working' ? `, ${this.hours(d)}` : d.status === 'Custom' ? `, ${this.hours(d)}${d.reason ? ': ' + d.reason : ''}` : d.reason ? `: ${d.reason}` : '';
    return `${base}, ${p}${detail}${where}`;
  }

  protected weekLabel(c: Cell): string {
    const d = c.day;
    return d ? `${this.cellLabel(c)}${d.slots.length ? '. Fasce ' + this.slotsText(d) : ''}` : LONG_DAY.format(c.date);
  }

  protected weekHead(c: Cell): string {
    return `${SHORT_DAYS[(c.date.getDay() + 6) % 7]} ${c.date.getDate()}`;
  }

  protected fullDate(iso: string): string {
    return cap(FULL_DAY.format(fromIsoDate(iso)));
  }

  protected bars(d: CalendarDayModel) {
    return d.slots.map((s, i) => {
      const a = Math.max(toMinutes(s.startTime), AXIS_START);
      const b = Math.min(toMinutes(s.endTime), AXIS_END);
      return { key: i, top: ((a - AXIS_START) / this.axisSpan) * 100, height: Math.max(((b - a) / this.axisSpan) * 100, 1), text: `${toHHmm(s.startTime)}–${toHHmm(s.endTime)}` };
    });
  }
}
