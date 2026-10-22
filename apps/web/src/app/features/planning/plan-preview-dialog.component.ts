import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowRight, lucideCircleCheck, lucideClock, lucideLock, lucideMove, lucidePlus, lucideMinus, lucideSearch, lucideTrendingDown, lucideTrendingUp, lucideTriangleAlert } from '@ng-icons/lucide';
import type { PlanChangeKind, PlanningConflictModel, PlanningItemModel, PlanningPreviewModel, PlanningScope } from '@micromes/api-client/models';
import { PlanningService } from '@micromes/api-client/services/planning';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmTooltipImports } from '@spartan-ng/helm/tooltip';
import { firstValueFrom } from 'rxjs';
import { CalendarsStore } from '../calendars/calendars.store';
import { toFailure } from '../materials/materials.service';
import { formatEstimatedEnd } from '../production-orders/estimated-end.component';
import { setupDelta } from './setup';

const CHANGE: Record<PlanChangeKind, { label: string; icon: string }> = {
  New: { label: 'Nuovo', icon: 'lucidePlus' },
  Moved: { label: 'Spostato', icon: 'lucideMove' },
  Unchanged: { label: 'Invariato', icon: 'lucideCircleCheck' },
};

/** Anteprima e applicazione del piano. Riusabile dal dettaglio ordine con scope=Order. */
@Component({
  selector: 'app-plan-preview-dialog',
  imports: [HlmDialogImports, HlmButton, HlmBadge, HlmInput, HlmLabel, HlmSkeleton, HlmSwitch, HlmTableImports, HlmTooltipImports, NgIcon],
  providers: [provideIcons({ lucideArrowRight, lucideCircleCheck, lucideClock, lucideLock, lucideMinus, lucideMove, lucidePlus, lucideSearch, lucideTrendingDown, lucideTrendingUp, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="max-h-[90vh] overflow-auto sm:max-w-6xl">
          <hlm-dialog-header>
            <h2 hlmDialogTitle>{{ title() }}</h2>
            <p hlmDialogDescription>
              Anteprima: nulla viene modificato finché non applichi il piano.
              @if (scope() === 'All') {
                Tutti i lavori aperti saranno ripianificati e le date attuali sostituite.
              }
            </p>
          </hlm-dialog-header>

          @if (loading()) {
            <div class="flex flex-col gap-2" aria-busy="true" aria-label="Calcolo dell'anteprima">
              <hlm-skeleton class="h-6 w-2/3" />
              <hlm-skeleton class="h-40 w-full" />
            </div>
          } @else if (error() && !preview()) {
            <p class="text-destructive text-sm" role="alert">{{ error() }}</p>
            <div><button hlmBtn variant="outline" type="button" (click)="load()">Riprova</button></div>
          } @else if (preview(); as p) {
            <p class="text-sm" data-testid="plan-summary">
              <strong>{{ p.summary.planned }}</strong> lavori pianificati ·
              <strong>{{ p.summary.moved }}</strong> spostati · <strong>{{ p.summary.new }}</strong> nuovi ·
              <span [class.text-destructive]="p.summary.lateOrders > 0">
                <strong>{{ p.summary.lateOrders }}</strong> ordini in ritardo
                @if (p.summary.lateOrders > 0) { (max {{ p.summary.maxLateDays }} giorni) }
              </span>
              · <strong>{{ p.summary.unplanned }}</strong> non pianificabili
              @if ((p.summary.lockedCount ?? 0) > 0) {
                · <span hlmBadge variant="outline" class="gap-1 px-1.5" data-testid="locked-chip"><ng-icon name="lucideLock" size="12" aria-hidden="true" /> {{ p.summary.lockedCount }} fissati</span>
              }
              @if ((p.summary.conflictCount ?? 0) > 0) {
                · <span hlmBadge variant="outline" class="text-destructive gap-1 px-1.5" data-testid="conflict-chip"><ng-icon name="lucideTriangleAlert" size="12" aria-hidden="true" /> {{ p.summary.conflictCount }} conflitti</span>
              }
              @if (setup(); as sd) {
                · <span hlmBadge variant="outline" class="gap-1 px-1.5" [class.text-destructive]="sd.direction === 'up'" data-testid="setup-chip">
                  <ng-icon [name]="sd.icon" size="12" aria-hidden="true" /> <span aria-hidden="true">{{ sd.text }}</span><span class="sr-only">{{ sd.sr }}</span>
                </span>
              }
              @if (p.summary.lastEnd) { · fine ultimo lavoro <strong class="tabular-nums">{{ fmt(p.summary.lastEnd) }}</strong> }
            </p>

            <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
              <div class="relative w-full max-w-64">
                <ng-icon name="lucideSearch" class="text-muted-foreground absolute top-1/2 left-2.5 -translate-y-1/2" size="16" aria-hidden="true" />
                <input hlmInput class="pl-8" type="search" aria-label="Cerca per lavoro, ordine o fase" placeholder="Cerca lavoro, ordine, fase" [value]="search()" (input)="search.set($any($event.target).value)" />
              </div>
              <div class="flex items-center gap-2">
                <hlm-switch inputId="plan-only-changes" [checked]="onlyChanges()" (checkedChange)="onlyChanges.set($event)" />
                <label hlmLabel for="plan-only-changes">Mostra solo cambiamenti</label>
              </div>
            </div>

            @if (!rows().length) {
              <p class="text-muted-foreground text-sm">Nessun lavoro da mostrare con i filtri attuali.</p>
            } @else {
              <div class="max-h-[420px] w-full overflow-auto rounded-md border">
                <div hlmTableContainer>
                  <table hlmTable>
                    <thead hlmTHead class="bg-muted sticky top-0 z-10">
                      <tr hlmTr>
                        <th hlmTh>Lavoro</th>
                        <th hlmTh>Fase</th>
                        <th hlmTh>Macchina proposta</th>
                        <th hlmTh>Inizio</th>
                        <th hlmTh>Fine</th>
                        <th hlmTh>Attuale</th>
                        <th hlmTh>Attrezzaggio</th>
                        <th hlmTh>Esito</th>
                        <th hlmTh>Motivo dell'attesa</th>
                      </tr>
                    </thead>
                    <tbody hlmTBody>
                      @for (g of rows(); track g.orderId) {
                        <tr hlmTr>
                          <th hlmTh colspan="9" scope="colgroup" class="bg-muted/50 text-left">Ordine {{ g.orderCode }}</th>
                        </tr>
                        @for (i of g.items; track i.workOrderId) {
                          <tr hlmTr>
                            <td hlmTd class="font-medium">{{ i.code }}</td>
                            <td hlmTd>{{ i.processSegmentName }}</td>
                            <td hlmTd>
                              @if (i.proposedEquipment; as eq) {
                                <span class="font-medium">{{ eq.code }}</span>
                                @if (i.assignsEquipment) { <span class="text-muted-foreground block text-xs">assegnata dal piano</span> }
                              } @else { <span class="text-muted-foreground">—</span> }
                            </td>
                            <td hlmTd class="tabular-nums">{{ fmt(i.proposedStart) }}</td>
                            <td hlmTd class="tabular-nums">
                              {{ fmt(i.proposedEnd) }}
                              @if (i.isLate) {
                                <span hlmBadge variant="outline" class="text-destructive ml-1 px-1.5">
                                  <ng-icon name="lucideTriangleAlert" size="12" aria-hidden="true" /> +{{ i.lateDays }} g
                                  <span class="sr-only"> di ritardo</span>
                                </span>
                              }
                            </td>
                            <td hlmTd class="text-muted-foreground text-xs tabular-nums">
                              @if (i.currentStart && i.currentEnd) {
                                {{ i.currentEquipment?.code ?? 'senza macchina' }} · {{ fmt(i.currentStart) }} → {{ fmt(i.currentEnd) }}
                              } @else { — }
                            </td>
                            <td hlmTd class="max-w-64 text-sm">
                              @if ((i.setupMinutes ?? 0) > 0) {
                                <span class="inline-flex flex-wrap items-center gap-1">
                                  <span class="font-medium tabular-nums">Attrezzaggio {{ i.setupMinutes }} min</span>
                                  @if (i.isFullSetup) { <span hlmBadge variant="secondary" class="px-1.5">Completo</span> }
                                </span>
                                @if (i.setupReasons?.length) {
                                  <details class="text-muted-foreground text-xs">
                                    <summary class="cursor-pointer">Motivi ({{ i.setupReasons!.length }})</summary>
                                    <ul class="list-disc pl-4">
                                      @for (r of i.setupReasons; track $index) { <li>{{ r }}</li> }
                                    </ul>
                                  </details>
                                }
                              } @else { <span class="text-muted-foreground">—</span> }
                            </td>
                            <td hlmTd>
                              <span hlmBadge [variant]="i.change === 'Unchanged' ? 'outline' : 'secondary'" class="gap-1 px-1.5">
                                <ng-icon [name]="change(i.change).icon" size="12" aria-hidden="true" /> {{ change(i.change).label }}
                              </span>
                            </td>
                            <td hlmTd class="max-w-64 text-sm">
                              @if (i.reasons.length) {
                                <span class="line-clamp-2" tabindex="0" [hlmTooltip]="reasonsText(i)" [attr.aria-label]="reasonsText(i)">{{ reasonShort(i) }}</span>
                              } @else { <span class="text-muted-foreground">—</span> }
                            </td>
                          </tr>
                        }
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }

            @if (p.unplanned.length) {
              <section aria-labelledby="plan-unplanned-title" class="grid gap-2">
                <h3 id="plan-unplanned-title" class="text-sm font-semibold">Non pianificabili ({{ p.unplanned.length }})</h3>
                <ul class="grid gap-1.5">
                  @for (u of p.unplanned; track u.workOrderId) {
                    <li class="flex items-start gap-2 rounded-md border p-2 text-sm">
                      <ng-icon name="lucideTriangleAlert" class="mt-0.5 shrink-0" size="16" aria-hidden="true" />
                      <span><span class="font-medium">{{ u.code }}</span> ({{ u.productionOrderCode }}): {{ u.message }}</span>
                    </li>
                  }
                </ul>
              </section>
            }

            @if (p.conflicts?.length) {
              <section aria-labelledby="plan-conflicts-title" class="grid gap-2" data-testid="plan-conflicts">
                <h3 id="plan-conflicts-title" class="text-sm font-semibold">Conflitti ({{ p.conflicts!.length }})</h3>
                <p class="text-muted-foreground text-xs">I conflitti non bloccano l'applicazione del piano: i lavori fissati restano dove sono.</p>
                <ul class="grid gap-1.5">
                  @for (c of p.conflicts!; track $index) {
                    <li class="border-destructive/40 flex items-start gap-2 rounded-md border p-2 text-sm">
                      <ng-icon name="lucideTriangleAlert" class="text-destructive mt-0.5 shrink-0" size="16" aria-hidden="true" />
                      <span>
                        <span class="sr-only">Conflitto: </span>{{ c.message }}
                        @if (c.minutes > 0) { <span class="text-muted-foreground tabular-nums"> ({{ c.minutes }} min)</span> }
                      </span>
                    </li>
                  }
                </ul>
              </section>
            }

            @if (p.notMoved?.length) {
              <section aria-labelledby="plan-notmoved-title" class="grid gap-2" data-testid="plan-not-moved">
                <h3 id="plan-notmoved-title" class="text-sm font-semibold">
                  <button type="button" class="flex items-center gap-1.5" [attr.aria-expanded]="notMovedOpen(p.notMoved!.length)" aria-controls="plan-notmoved-list" (click)="notMovedToggle.set(!notMovedOpen(p.notMoved!.length))">
                    <ng-icon name="lucideLock" size="14" aria-hidden="true" /> Non spostati (fissati) ({{ p.notMoved!.length }})
                    <span class="text-muted-foreground text-xs font-normal">{{ notMovedOpen(p.notMoved!.length) ? 'Comprimi' : 'Espandi' }}</span>
                  </button>
                </h3>
                @if (notMovedOpen(p.notMoved!.length)) {
                  <ul id="plan-notmoved-list" class="grid gap-1.5">
                    @for (n of p.notMoved!; track n.workOrderId) {
                      <li class="flex items-start gap-2 rounded-md border p-2 text-sm">
                        <ng-icon name="lucideLock" class="text-muted-foreground mt-0.5 shrink-0" size="16" aria-hidden="true" />
                        <span>
                          <span class="font-medium">{{ n.code }}</span> · {{ n.processSegmentName }}
                          @if (n.equipment) { · {{ n.equipment.code }} }
                          · <span class="tabular-nums">{{ fmt(n.start) }} → {{ fmt(n.end) }}</span>
                          <span class="text-muted-foreground block text-xs">{{ n.message }}</span>
                        </span>
                      </li>
                    }
                  </ul>
                }
              </section>
            }

            @if (error()) {
              <p class="text-destructive text-sm" role="alert">{{ error() }}</p>
            }
          }

          <hlm-dialog-footer>
            <button hlmBtn type="button" variant="outline" (click)="closed.emit()">Annulla</button>
            <button hlmBtn type="button" [variant]="scope() === 'All' ? 'destructive' : 'default'" [disabled]="!preview() || busy()" (click)="apply()">
              {{ busy() ? 'Applicazione…' : 'Applica piano' }}
            </button>
          </hlm-dialog-footer>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class PlanPreviewDialogComponent implements OnInit {
  private readonly api = inject(PlanningService);
  private readonly calendars = inject(CalendarsStore);

  readonly scope = input.required<PlanningScope>();
  readonly productionOrderId = input<number | null>(null);
  readonly applied = output<void>();
  readonly closed = output<void>();

  protected readonly preview = signal<PlanningPreviewModel | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly onlyChanges = signal(true);
  protected readonly search = signal('');
  protected readonly notMovedToggle = signal<boolean | null>(null);
  protected notMovedOpen(count: number): boolean {
    return this.notMovedToggle() ?? count <= 5;
  }

  protected readonly setup = computed(() => {
    const s = this.preview()?.summary;
    return s ? setupDelta(s.setupMinutesBefore, s.setupMinutesAfter) : null;
  });

  protected readonly title = computed(() =>
    this.scope() === 'All' ? 'Ripianifica tutto' : this.scope() === 'Order' ? 'Pianifica ordine' : 'Pianifica non pianificati',
  );

  protected readonly rows = computed(() => {
    const p = this.preview();
    if (!p) return [];
    const q = this.search().trim().toLowerCase();
    const groups = new Map<number, { orderId: number; orderCode: string; items: PlanningItemModel[] }>();
    for (const i of p.items) {
      if (this.onlyChanges() && i.change === 'Unchanged') continue;
      if (q && ![i.code, i.productionOrderCode, i.processSegmentName, i.processSegmentCode].some((s) => s.toLowerCase().includes(q))) continue;
      const g = groups.get(i.productionOrderId) ?? { orderId: i.productionOrderId, orderCode: i.productionOrderCode, items: [] };
      g.items.push(i);
      groups.set(i.productionOrderId, g);
    }
    return [...groups.values()]
      .map((g) => ({ ...g, items: g.items.sort((a, b) => a.proposedStart.localeCompare(b.proposedStart)) }))
      .sort((a, b) => a.orderCode.localeCompare(b.orderCode));
  });

  ngOnInit(): void {
    void this.calendars.loadTimeZone();
    void this.load();
  }

  protected fmt(iso: string): string {
    return formatEstimatedEnd(iso, this.calendars.timeZone());
  }
  protected change(k: PlanChangeKind) {
    return CHANGE[k];
  }
  protected reasonShort(i: PlanningItemModel): string {
    const r = i.reasons[0];
    return r.until ? `${r.message} (fino al ${this.fmt(r.until)})` : r.message;
  }
  protected reasonsText(i: PlanningItemModel): string {
    return i.reasons.map((r) => (r.until ? `${r.message} (fino al ${this.fmt(r.until)})` : r.message)).join('. ');
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.preview.set(await firstValueFrom(this.api.previewPlan({ scope: this.scope(), productionOrderId: this.productionOrderId() })));
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  protected async apply(): Promise<void> {
    const p = this.preview();
    if (!p) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const r = await firstValueFrom(
        this.api.applyPlan({ scope: this.scope(), productionOrderId: this.productionOrderId(), previewToken: p.token }),
      );
      toast.success(`Piano applicato: ${r.summary.planned} lavori pianificati (${r.summary.new} nuovi, ${r.summary.moved} spostati)`);
      if ((r.summary.conflictCount ?? 0) > 0) {
        toast.warning(`${r.summary.conflictCount} conflitti con lavori fissati`, {
          description: (r.conflicts ?? []).slice(0, 3).map((c: PlanningConflictModel) => c.message).join(' · '),
        });
      }
      if ((r.summary.lockedCount ?? 0) > 0) {
        toast.info(`${r.summary.lockedCount} lavori fissati non spostati`, { description: (r.notMoved ?? []).map((n) => n.code).join(', ') });
      }
      if (r.differsFromPreview) {
        toast.warning("Il piano è cambiato rispetto all'anteprima", { description: 'Controlla le date applicate.' });
      }
      this.applied.emit();
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.busy.set(false);
    }
  }
}
