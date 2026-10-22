import { ChangeDetectionStrategy, Component, computed, effect, inject, input, OnDestroy, OnInit, output, signal, untracked } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideAlarmClock, lucideCircleAlert, lucideLoaderCircle, lucideTriangleAlert, lucideWrench } from '@ng-icons/lucide';
import type { EquipmentModel, MoveConflictModel, MovePreviewModel } from '@micromes/api-client/models';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import type { Subscription } from 'rxjs';
import { CalendarsStore } from '../calendars/calendars.store';
import { toFailure } from '../materials/materials.service';
import { machineLabel, WorkOrderMachinesService } from '../work-orders/work-order-machines';
import { WorkOrdersStore } from '../work-orders/work-orders.store';
import { ceilTo5Min, conflictLine, instantToWall, parseSuggestion, wallToInstant } from './plan-lock';
import { formatDateTime } from './timeline-time';

/** Dati minimi del lavoro da spostare (timeline e pagina Ordini di lavoro li ricavano da modelli diversi). */
export interface MoveTarget {
  id: number;
  code: string;
  processSegmentId: number;
  plannedStartAt?: string | null;
  equipmentId?: number | null;
}

const DEBOUNCE_MS = 300;

/** «Sposta…»: sceglie nuovo inizio (ora aziendale) e macchina, mostra l'anteprima dal server e applica fissando il lavoro. */
@Component({
  selector: 'app-plan-move-dialog',
  imports: [HlmDialogImports, HlmSelectImports, HlmButton, HlmInput, HlmLabel, HlmSkeleton, NgIcon],
  providers: [provideIcons({ lucideAlarmClock, lucideCircleAlert, lucideLoaderCircle, lucideTriangleAlert, lucideWrench })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form class="grid gap-4" (submit)="$event.preventDefault(); apply()">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>Sposta lavoro · {{ work().code }}</h2>
              <p hlmDialogDescription>Scegli il nuovo inizio (ora aziendale, {{ timeZone() }}). Il lavoro viene fissato: il ripianifica non lo sposterà più.</p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
            }

            <div class="grid grid-cols-2 gap-3">
              <div class="grid gap-1.5">
                <label hlmLabel for="mv-date">Data</label>
                <input hlmInput id="mv-date" class="h-9" type="date" required [value]="date()" [attr.aria-invalid]="previewError() ? 'true' : null" [attr.aria-describedby]="describedBy()" (input)="date.set($any($event.target).value)" />
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="mv-time">Ora</label>
                <input hlmInput id="mv-time" class="h-9" type="time" step="300" required [value]="time()" [attr.aria-invalid]="previewError() ? 'true' : null" [attr.aria-describedby]="describedBy()" (input)="time.set($any($event.target).value)" />
              </div>
              @if (previewError(); as err) {
                <div class="col-span-2 flex flex-col items-start gap-2">
                  <p id="mv-date-error" class="text-destructive flex items-start gap-2 text-sm" role="alert">
                    <ng-icon name="lucideCircleAlert" class="mt-0.5 shrink-0" aria-hidden="true" /> <span>{{ err }}</span>
                  </p>
                  @if (suggestion(); as s) {
                    <button hlmBtn type="button" variant="outline" size="sm" (click)="useSuggestion(s)">Usa il prossimo orario disponibile ({{ s.time }} del {{ s.date.slice(8) }}/{{ s.date.slice(5, 7) }})</button>
                  }
                </div>
              }
            </div>

            @if (candidates().length) {
              <div class="grid gap-1.5">
                <span id="mv-machine-label" class="text-sm font-medium">Macchina</span>
                <hlm-select [value]="machine()" [itemToString]="itemLabel" (valueChange)="machine.set($event ?? null)">
                  <hlm-select-trigger class="w-full" aria-labelledby="mv-machine-label"><hlm-select-value placeholder="Scegli la macchina" /></hlm-select-trigger>
                  <hlm-select-content *hlmSelectPortal>
                    @for (o of candidates(); track o.id) {
                      <hlm-select-item [value]="'' + o.id">{{ label(o) }}</hlm-select-item>
                    }
                  </hlm-select-content>
                </hlm-select>
              </div>
            } @else if (loading()) {
              <hlm-skeleton class="h-9 w-full" aria-busy="true" aria-label="Caricamento macchine" />
            }

            <section class="bg-muted/40 grid gap-2 rounded-md border p-3 text-sm" aria-label="Anteprima dello spostamento" aria-live="polite" [attr.aria-busy]="pending()">
              @if (pending()) {
                <p class="text-muted-foreground flex items-center gap-2"><ng-icon name="lucideLoaderCircle" class="animate-spin" aria-hidden="true" /> Calcolo l’anteprima…</p>
              } @else if (preview(); as p) {
                <dl class="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
                  <dt class="text-muted-foreground">Nuova fine</dt>
                  <dd class="font-medium tabular-nums">{{ fmt(p.newEnd) }}</dd>
                  <dt class="text-muted-foreground">Attrezzaggio</dt>
                  <dd>
                    @if ((p.setupMinutes ?? 0) > 0) {
                      <span class="font-medium tabular-nums">{{ p.setupMinutes }} min</span>
                      @if (p.isFullSetup) { <span class="text-muted-foreground"> (completo)</span> }
                      @if (p.setupReasons?.length) {
                        <ul class="text-muted-foreground list-disc pl-4 text-xs">
                          @for (r of p.setupReasons; track $index) { <li>{{ r }}</li> }
                        </ul>
                      }
                    } @else { <span class="text-muted-foreground">nessuno</span> }
                  </dd>
                  <dt class="text-muted-foreground">Durata</dt>
                  <dd>{{ duration(p.durationMinutes) }}</dd>
                </dl>
                @if (lines().length) {
                  <ul class="flex flex-col gap-1">
                    @for (l of lines(); track $index) {
                      <li class="flex items-start gap-2"><ng-icon [name]="l.icon" size="14" class="mt-0.5 shrink-0" aria-hidden="true" /> <span><span class="sr-only">Avviso: </span>{{ l.text }}</span></li>
                    }
                  </ul>
                } @else {
                  <p class="text-muted-foreground">Nessun conflitto.</p>
                }
              } @else if (previewError()) {
                <p class="text-muted-foreground">Correggi l’orario per vedere l’anteprima.</p>
              } @else {
                <p class="text-muted-foreground">Scegli data e ora per vedere l’anteprima.</p>
              }
            </section>

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="!canApply()">{{ busy() ? 'Applico…' : 'Applica e fissa' }}</button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class PlanMoveDialogComponent implements OnInit, OnDestroy {
  private readonly store = inject(WorkOrdersStore);
  private readonly machines = inject(WorkOrderMachinesService);
  private readonly calendars = inject(CalendarsStore);

  readonly work = input.required<MoveTarget>();
  readonly done = output<void>();
  readonly closed = output<void>();

  protected readonly timeZone = this.calendars.timeZone;
  protected readonly label = machineLabel;
  protected readonly date = signal('');
  protected readonly time = signal('');
  protected readonly machine = signal<string | null>(null);
  protected readonly candidates = signal<EquipmentModel[]>([]);
  protected readonly loading = signal(true);
  protected readonly preview = signal<MovePreviewModel | null>(null);
  protected readonly previewError = signal<string | null>(null);
  protected readonly pending = signal(false);
  protected readonly busy = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly itemLabel = (v: string): string => {
    const m = this.candidates().find((c) => String(c.id) === v);
    return m ? machineLabel(m) : '';
  };
  protected readonly describedBy = computed(() => (this.previewError() ? 'mv-date-error' : null));
  protected readonly lines = computed(() => (this.preview()?.conflicts ?? []).map((c: MoveConflictModel) => conflictLine(c)));
  protected readonly suggestion = computed(() => {
    const e = this.previewError();
    return e ? parseSuggestion(e, { date: this.date(), time: this.time() }) : null;
  });
  protected readonly canApply = computed(() => !!this.preview() && !this.previewError() && !this.pending() && !this.busy() && !this.loading());

  private sub: Subscription | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private seq = 0;

  constructor() {
    effect(() => {
      this.date();
      this.time();
      this.machine();
      if (this.loading()) return;
      untracked(() => this.schedule());
    });
  }

  ngOnInit(): void {
    void this.init();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
    this.sub?.unsubscribe();
  }

  private async init(): Promise<void> {
    const w = this.work();
    void this.calendars.loadTimeZone();
    const tz = this.calendars.timeZone();
    const start = w.plannedStartAt ? new Date(w.plannedStartAt).getTime() : ceilTo5Min(Date.now());
    const wall = instantToWall(start, tz);
    this.date.set(wall.date);
    this.time.set(wall.time);
    try {
      const list = await this.machines.candidates(w.processSegmentId);
      this.candidates.set(list);
      if (w.equipmentId != null && list.some((e) => e.id === w.equipmentId)) this.machine.set(String(w.equipmentId));
      else if (list.length === 1) this.machine.set(String(list[0].id));
    } catch (e) {
      this.formError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  protected fmt(iso: string): string {
    return formatDateTime(iso, this.timeZone());
  }

  protected duration(min: number): string {
    const h = Math.floor(min / 60);
    const m = min % 60;
    return h ? `${h} h${m ? ` ${m} min` : ''}` : `${m} min`;
  }

  protected useSuggestion(s: { date: string; time: string }): void {
    this.date.set(s.date);
    this.time.set(s.time);
  }

  private request(): { startAt: string; equipmentId?: number } | null {
    const ms = wallToInstant(this.date(), this.time(), this.timeZone());
    if (ms == null) return null;
    const m = this.machine();
    return { startAt: new Date(ms).toISOString(), ...(m ? { equipmentId: Number(m) } : {}) };
  }

  /** Anteprima con debounce di 300 ms; la richiesta precedente viene annullata. */
  private schedule(immediate = false): void {
    clearTimeout(this.timer);
    this.sub?.unsubscribe();
    this.sub = null;
    const req = this.request();
    this.preview.set(null);
    this.previewError.set(null);
    if (!req) {
      this.pending.set(false);
      return;
    }
    this.pending.set(true);
    this.timer = setTimeout(() => this.runPreview(req), immediate ? 0 : DEBOUNCE_MS);
  }

  private runPreview(req: { startAt: string; equipmentId?: number }): void {
    const n = ++this.seq;
    this.sub = this.store.previewMove(this.work().id, req).subscribe({
      next: (p) => {
        if (n !== this.seq) return;
        this.preview.set(p);
        this.pending.set(false);
      },
      error: (e) => {
        if (n !== this.seq) return;
        const f = toFailure(e);
        const detail = Object.values(f.fieldErrors).join(' ');
        this.previewError.set(f.status === 422 && detail ? detail : f.message);
        this.pending.set(false);
      },
    });
  }

  protected async apply(): Promise<void> {
    const p = this.preview();
    const req = this.request();
    if (!p || !req || !this.canApply()) return;
    this.busy.set(true);
    this.formError.set(null);
    try {
      const r = await this.store.move(this.work().id, { ...req, expectedUpdatedAt: p.expectedUpdatedAt });
      toast.success(r.conflicts.length ? `Lavoro spostato e fissato (${r.conflicts.length} avvisi)` : 'Lavoro spostato e fissato');
      this.done.emit();
    } catch (e) {
      const f = toFailure(e);
      const detail = Object.values(f.fieldErrors).join(' ');
      this.formError.set(f.status === 422 && detail ? detail : f.message);
      // 409: il lavoro è cambiato dopo l'anteprima → ricalcola subito.
      if (f.status === 409) this.schedule(true);
    } finally {
      this.busy.set(false);
    }
  }
}
