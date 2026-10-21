import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideTrash2, lucideTriangleAlert } from '@ng-icons/lucide';
import type { ScrapEntryRequest, ScrapReasonModel } from '@micromes/api-client/models';
import { ScrapReasonsService } from '@micromes/api-client/services/scrap-reasons';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { firstValueFrom } from 'rxjs';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toast } from '@spartan-ng/brain/sonner';
import type { WorkOrderModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { formatQty } from '../production-orders/production-order-labels';
import { WorkOrdersStore } from './work-orders.store';

export type CloseMode = 'Pause' | 'Stop';

const COPY: Record<CloseMode, { title: string; submit: string; pending: string; done: string }> = {
  Pause: { title: 'Metti in pausa', submit: 'Metti in pausa', pending: 'Salvataggio…', done: 'Lavoro messo in pausa' },
  Stop: { title: 'Chiudi lavoro', submit: 'Chiudi lavoro', pending: 'Chiusura…', done: 'Lavoro chiuso' },
};

interface Row {
  key: number;
  reason: string;
  quantity: number | null;
}

const NONE = 'none';

/** Dialog di pausa / chiusura: registra buono, scarto e note della sessione; mostra gli errori del backend. */
@Component({
  selector: 'app-work-order-close-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmSelectImports, HlmButton, HlmInput, HlmLabel, HlmTextarea, NgIcon],
  providers: [provideIcons({ lucidePlus, lucideTrash2, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-xl">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ copy().title }} · {{ work().code }}</h2>
              <p hlmDialogDescription>Registra le quantità prodotte in questa sessione di lavoro.</p>
            </hlm-dialog-header>

            <dl class="bg-muted grid grid-cols-3 gap-2 rounded-md p-3 text-sm">
              <div>
                <dt class="text-muted-foreground text-xs">Pianificata</dt>
                <dd class="tabular-nums">{{ qty(work().plannedQuantity) }} {{ unit() }}</dd>
              </div>
              <div>
                <dt class="text-muted-foreground text-xs">Già buona</dt>
                <dd class="tabular-nums">{{ qty(work().goodQuantity) }} {{ unit() }}</dd>
              </div>
              <div>
                <dt class="text-muted-foreground text-xs">Già scarto</dt>
                <dd class="tabular-nums">{{ qty(work().scrapQuantity) }} {{ unit() }}</dd>
              </div>
            </dl>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
                {{ formError() }}
              </p>
            }

            <div class="grid grid-cols-2 gap-4">
              <div class="grid gap-1.5">
                <label hlmLabel for="wo-good">Quantità buona</label>
                <input hlmInput id="wo-good" type="number" step="any" min="0" formControlName="goodQuantity" [attr.aria-invalid]="fieldError('goodQuantity') ? true : null" />
                @if (fieldError('goodQuantity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="wo-scrap">Scarto{{ rows().length ? ' (somma dei motivi)' : '' }}</label>
                @if (rows().length) {
                  <input hlmInput id="wo-scrap" type="text" readonly [value]="qty(scrapTotal())" class="tabular-nums" />
                } @else {
                  <input hlmInput id="wo-scrap" type="number" step="any" min="0" formControlName="scrapQuantity" [attr.aria-invalid]="fieldError('scrapQuantity') ? true : null" />
                }
                @if (fieldError('scrapQuantity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </div>

            <fieldset class="grid gap-2">
              <legend class="text-sm font-medium">Motivi di scarto</legend>
              @if (!rows().length) {
                <p class="text-muted-foreground text-xs">
                  Facoltativo. Senza motivi, tutto lo scarto viene registrato «Senza motivo».
                </p>
              }
              @if (reasonsError()) {
                <p class="text-destructive text-xs" role="alert">{{ reasonsError() }}</p>
              }
              @for (r of rows(); track r.key; let i = $index) {
                <div class="flex items-end gap-2">
                  <div class="grid flex-1 gap-1.5">
                    <span class="text-muted-foreground text-xs" [id]="'wo-r-l-' + r.key">Motivo {{ i + 1 }}</span>
                    <hlm-select [value]="r.reason" [itemToString]="reasonLabel" (valueChange)="setReason(r.key, $event ?? NONE)">
                      <hlm-select-trigger size="sm" class="w-full" [attr.aria-labelledby]="'wo-r-l-' + r.key">
                        <hlm-select-value />
                      </hlm-select-trigger>
                      <hlm-select-content *hlmSelectPortal>
                        <hlm-select-item [value]="NONE">Senza motivo</hlm-select-item>
                        @for (o of reasons(); track o.id) {
                          <hlm-select-item [value]="'' + o.id">{{ o.code }} · {{ o.name }}</hlm-select-item>
                        }
                      </hlm-select-content>
                    </hlm-select>
                  </div>
                  <div class="grid w-28 gap-1.5">
                    <label class="text-muted-foreground text-xs" [for]="'wo-r-q-' + r.key">Quantità</label>
                    <input hlmInput class="h-8" [id]="'wo-r-q-' + r.key" type="number" step="any" min="0" [value]="r.quantity ?? ''" (input)="setQuantity(r.key, $any($event.target).value)" />
                  </div>
                  <button hlmBtn type="button" variant="ghost" size="icon-sm" [attr.aria-label]="'Rimuovi motivo ' + (i + 1)" (click)="removeRow(r.key)">
                    <ng-icon name="lucideTrash2" />
                  </button>
                </div>
              }
              <div>
                <button hlmBtn type="button" variant="outline" size="sm" (click)="addRow()">
                  <ng-icon name="lucidePlus" /> Aggiungi motivo
                </button>
              </div>
            </fieldset>

            @if (shortfall() > 0) {
              <p class="bg-muted flex items-start gap-2 rounded-md px-3 py-2 text-sm" role="status">
                <ng-icon name="lucideTriangleAlert" class="mt-0.5 shrink-0" aria-hidden="true" />
                <span>{{ shortfall() === 1 ? 'Manca' : 'Mancano' }} {{ qty(shortfall()) }} {{ unit() }}: i passi successivi ne riceveranno meno.</span>
              </p>
            }

            <div class="grid gap-1.5">
              <label hlmLabel for="wo-notes">Note</label>
              <textarea hlmTextarea id="wo-notes" formControlName="notes" maxlength="1000"></textarea>
              @if (fieldError('notes'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="saving()">{{ saving() ? copy().pending : copy().submit }}</button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class WorkOrderCloseDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(WorkOrdersStore);

  readonly work = input.required<WorkOrderModel>();
  readonly mode = input.required<CloseMode>();
  readonly done = output<void>();
  readonly closed = output<void>();

  protected readonly qty = formatQty;
  protected readonly copy = computed(() => COPY[this.mode()]);
  protected readonly unit = computed(() => unitLabel(this.work().unitOfMeasure));
  private readonly reasonsApi = inject(ScrapReasonsService);
  protected readonly NONE = NONE;
  protected readonly reasons = signal<ScrapReasonModel[]>([]);
  protected readonly reasonsError = signal<string | null>(null);
  protected readonly rows = signal<Row[]>([]);
  private nextKey = 1;
  protected readonly scrapTotal = computed(() => this.rows().reduce((a, r) => a + (r.quantity && r.quantity > 0 ? r.quantity : 0), 0));
  protected readonly reasonLabel = (value: string): string => {
    if (value === NONE) return 'Senza motivo';
    const r = this.reasons().find((o) => String(o.id) === value);
    return r ? `${r.code} · ${r.name}` : value;
  };
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});

  protected readonly form = this.fb.group({
    goodQuantity: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    scrapQuantity: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    notes: ['', Validators.maxLength(1000)],
  });

  private readonly goodValue = toSignal(this.form.controls.goodQuantity.valueChanges, { initialValue: 0 });
  /** Pezzi che mancano al pianificato dopo questa sessione (avviso non bloccante). */
  protected readonly shortfall = computed(() =>
    Math.max(0, this.work().plannedQuantity - this.work().goodQuantity - Math.max(0, this.goodValue() ?? 0)),
  );

  async ngOnInit(): Promise<void> {
    try {
      // Il backend filtra per fase (motivo senza fasi = valido ovunque) e per attivi.
      this.reasons.set(await firstValueFrom(this.reasonsApi.getScrapReasons({ processSegmentId: this.work().processSegmentId })));
    } catch (e) {
      this.reasonsError.set(`Motivi non caricati: ${toFailure(e).message}. Lo scarto può essere registrato senza motivo.`);
    }
  }

  protected addRow(): void {
    this.rows.update((r) => [...r, { key: this.nextKey++, reason: NONE, quantity: null }]);
  }

  protected removeRow(key: number): void {
    this.rows.update((r) => r.filter((x) => x.key !== key));
  }

  protected setReason(key: number, reason: string): void {
    this.rows.update((r) => r.map((x) => (x.key === key ? { ...x, reason } : x)));
  }

  protected setQuantity(key: number, raw: string): void {
    const quantity = raw === '' ? null : Number(raw);
    this.rows.update((r) => r.map((x) => (x.key === key ? { ...x, quantity } : x)));
  }

  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    if (c.hasError('min')) return 'La quantità non può essere negativa';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const entries: ScrapEntryRequest[] = this.rows()
      .filter((r) => (r.quantity ?? 0) > 0)
      .map((r) => ({ scrapReasonId: r.reason === NONE ? null : Number(r.reason), quantity: r.quantity ?? 0 }));
    const useRows = this.rows().length > 0;
    const req = {
      goodQuantity: v.goodQuantity ?? 0,
      scrapQuantity: useRows ? this.scrapTotal() : (v.scrapQuantity ?? 0),
      notes: v.notes.trim() || null,
      ...(useRows ? { scrapEntries: entries } : {}),
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const id = this.work().id;
      if (this.mode() === 'Pause') await this.store.pause(id, req);
      else await this.store.stop(id, req);
      toast.success(this.copy().done);
      this.done.emit();
    } catch (e) {
      const f = toFailure(e);
      const detail = Object.values(f.fieldErrors).join(' ');
      if (f.status === 422 && detail) {
        this.serverErrors.set(f.fieldErrors);
        this.formError.set(detail);
      } else this.formError.set(f.message);
    } finally {
      this.saving.set(false);
    }
  }
}
