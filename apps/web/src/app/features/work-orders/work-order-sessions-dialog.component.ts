import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import type { WorkOrderDetailModel, WorkOrderModel, WorkOrderSessionModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { formatDate, formatQty } from '../production-orders/production-order-labels';
import { WorkOrderCorrectionDialogComponent } from './work-order-correction-dialog.component';
import { WorkOrdersStore } from './work-orders.store';

/** Registrazioni (sessioni) di un lavoro, con storico delle correzioni e azione «Correggi». */
@Component({
  selector: 'app-work-order-sessions-dialog',
  imports: [HlmDialogImports, HlmButton, HlmBadge, HlmSkeleton, WorkOrderCorrectionDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="max-h-[90vh] overflow-auto sm:max-w-3xl">
          <hlm-dialog-header>
            <h2 hlmDialogTitle>Registrazioni · {{ work().code }}</h2>
            <p hlmDialogDescription>
              Sessioni di lavoro di {{ work().processSegmentName }} su {{ work().materialCode }}.
              @if (!canCorrect()) {
                Le registrazioni non sono correggibili: l'ordine di produzione è {{ orderStatusLabel() }}.
              }
            </p>
          </hlm-dialog-header>

          @if (loading()) {
            <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento registrazioni">
              <hlm-skeleton class="h-16 w-full" />
              <hlm-skeleton class="h-16 w-full" />
            </div>
          } @else if (error()) {
            <p class="text-destructive text-sm" role="alert">{{ error() }}</p>
            <div><button hlmBtn variant="outline" type="button" (click)="load()">Riprova</button></div>
          } @else if (!sessions().length) {
            <p class="text-muted-foreground text-sm">Nessuna registrazione: il lavoro non è ancora stato avviato.</p>
          } @else {
            <ul class="grid gap-3">
              @for (s of sessions(); track s.id; let i = $index) {
                <li class="rounded-md border p-3">
                  <div class="flex flex-wrap items-center gap-x-4 gap-y-1">
                    <span class="font-medium">Sessione {{ i + 1 }}</span>
                    <span class="text-muted-foreground text-sm tabular-nums">
                      {{ date(s.startedAt) }} → {{ s.endedAt ? date(s.endedAt) : 'in corso' }}
                    </span>
                    @if (s.isCorrected) {
                      <span hlmBadge variant="secondary">Corretta</span>
                    }
                    @if (canCorrect() && s.endedAt) {
                      <button hlmBtn class="ml-auto" size="sm" variant="outline" type="button" (click)="correcting.set(s)" [attr.aria-label]="'Correggi sessione ' + (i + 1)">
                        Correggi
                      </button>
                    }
                  </div>
                  <dl class="mt-2 grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
                    <div>
                      <dt class="text-muted-foreground text-xs">Buono</dt>
                      <dd class="tabular-nums">{{ qty(s.goodQuantity) }} {{ unit() }}</dd>
                    </div>
                    <div>
                      <dt class="text-muted-foreground text-xs">Scarto</dt>
                      <dd class="tabular-nums">{{ qty(s.scrapQuantity) }} {{ unit() }}</dd>
                    </div>
                    <div>
                      <dt class="text-muted-foreground text-xs">Note</dt>
                      <dd>{{ s.notes || '—' }}</dd>
                    </div>
                  </dl>
                  @if (s.scrapEntries.length) {
                    <ul class="mt-2 flex flex-wrap gap-1.5" aria-label="Scarti per motivo">
                      @for (e of s.scrapEntries; track e.id) {
                        <li hlmBadge variant="outline">{{ e.reasonName ?? 'Senza motivo' }}: {{ qty(e.quantity) }}</li>
                      }
                    </ul>
                  }
                  @if (s.corrections.length) {
                    <div class="mt-3 border-t pt-2">
                      <h3 class="text-muted-foreground mb-1 text-xs font-medium">Storico correzioni</h3>
                      <ol class="grid gap-2 text-sm">
                        @for (c of s.corrections; track c.id) {
                          <li>
                            <div class="text-muted-foreground text-xs">{{ date(c.correctedAt) }}</div>
                            <div class="tabular-nums">
                              Buono {{ qty(c.previousGoodQuantity) }} → {{ qty(c.newGoodQuantity) }};
                              scarto {{ qty(c.previousScrapQuantity) }} → {{ qty(c.newScrapQuantity) }}
                            </div>
                            @if (c.previousScrapDetail || c.newScrapDetail) {
                              <div class="text-muted-foreground text-xs">
                                Motivi: {{ c.previousScrapDetail || 'senza motivo' }} → {{ c.newScrapDetail || 'senza motivo' }}
                              </div>
                            }
                            <div>Nota: {{ c.notes }}</div>
                          </li>
                        }
                      </ol>
                    </div>
                  }
                </li>
              }
            </ul>
          }

          <hlm-dialog-footer>
            <button hlmBtn type="button" variant="outline" hlmDialogClose>Chiudi</button>
          </hlm-dialog-footer>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>

    @if (correcting(); as s) {
      <app-work-order-correction-dialog
        [work]="work()"
        [session]="s"
        (done)="correcting.set(null); changed.emit(); load()"
        (closed)="correcting.set(null)"
      />
    }
  `,
})
export class WorkOrderSessionsDialogComponent implements OnInit {
  private readonly store = inject(WorkOrdersStore);

  readonly work = input.required<WorkOrderModel>();
  readonly changed = output<void>();
  readonly closed = output<void>();

  protected readonly qty = formatQty;
  protected readonly date = formatDate;
  protected readonly unit = computed(() => unitLabel(this.work().unitOfMeasure));
  protected readonly detail = signal<WorkOrderDetailModel | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly correcting = signal<WorkOrderSessionModel | null>(null);

  protected readonly sessions = computed(() => this.detail()?.sessions ?? []);
  /** Non offerta con ordine Completed/Cancelled (il 409 del backend resta la rete di sicurezza). */
  protected readonly canCorrect = computed(() => {
    const st = this.detail()?.productionOrderStatus ?? this.work().productionOrderStatus;
    return st !== 'Completed' && st !== 'Cancelled';
  });
  protected readonly orderStatusLabel = computed(() =>
    (this.detail()?.productionOrderStatus ?? this.work().productionOrderStatus) === 'Completed' ? 'completato' : 'annullato',
  );

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(!this.detail());
    this.error.set(null);
    try {
      this.detail.set(await this.store.getDetail(this.work().id));
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }
}
