import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideTrash2 } from '@ng-icons/lucide';
import type { EquipmentModel, ScrapEntryRequest, ScrapReasonModel, WorkOrderModel, WorkOrderSessionModel } from '@micromes/api-client/models';
import { ScrapReasonsService } from '@micromes/api-client/services/scrap-reasons';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { firstValueFrom } from 'rxjs';
import { unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { machineLabel, WorkOrderMachinesService } from './work-order-machines';
import { formatQty } from '../production-orders/production-order-labels';
import { WorkOrdersStore } from './work-orders.store';

interface Row {
  key: number;
  reason: string;
  quantity: number | null;
  /** Etichetta dell'istantanea storica, per motivi non più nel catalogo attivo. */
  snapshot?: string;
}

const NONE = 'none';

@Component({
  selector: 'app-work-order-correction-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmSelectImports, HlmButton, HlmInput, HlmLabel, HlmTextarea, NgIcon],
  providers: [provideIcons({ lucidePlus, lucideTrash2 })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="max-h-[90vh] overflow-auto sm:max-w-xl">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>Correggi registrazione · {{ work().code }}</h2>
              <p hlmDialogDescription>
                I valori precedenti restano nello storico delle correzioni. La nota è obbligatoria.
              </p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
            }

            <div class="grid grid-cols-2 gap-4">
              <div class="grid gap-1.5">
                <label hlmLabel for="co-good">Quantità buona ({{ unit() }})</label>
                <input hlmInput id="co-good" type="number" step="any" min="0" formControlName="goodQuantity" [attr.aria-invalid]="fieldError('goodQuantity') ? true : null" />
                @if (fieldError('goodQuantity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="co-scrap">Scarto (somma dei motivi)</label>
                <input hlmInput id="co-scrap" type="text" readonly class="tabular-nums" [value]="qty(scrapTotal())" />
              </div>
            </div>

            @if (machineOptions().length) {
              <div class="grid gap-1.5">
                <span id="co-machine-label" class="text-sm font-medium">Macchina</span>
                <hlm-select [value]="machine()" [itemToString]="machineItemLabel" (valueChange)="machine.set($event ?? machine())">
                  <hlm-select-trigger size="sm" class="w-full" aria-labelledby="co-machine-label">
                    <hlm-select-value />
                  </hlm-select-trigger>
                  <hlm-select-content *hlmSelectPortal>
                    @for (o of machineOptions(); track o.value) {
                      <hlm-select-item [value]="o.value">{{ o.label }}</hlm-select-item>
                    }
                  </hlm-select-content>
                </hlm-select>
                <p class="text-muted-foreground text-xs">Cambiarla aggiorna la macchina della sessione e resta nello storico.</p>
              </div>
            }

            <fieldset class="grid gap-2">
              <legend class="text-sm font-medium">Motivi di scarto</legend>
              @for (r of rows(); track r.key; let i = $index) {
                <div class="flex items-end gap-2">
                  <div class="grid flex-1 gap-1.5">
                    <span class="text-muted-foreground text-xs" [id]="'co-r-l-' + r.key">Motivo {{ i + 1 }}</span>
                    <hlm-select [value]="r.reason" [itemToString]="reasonLabel" (valueChange)="setReason(r.key, $event ?? NONE)">
                      <hlm-select-trigger size="sm" class="w-full" [attr.aria-labelledby]="'co-r-l-' + r.key">
                        <hlm-select-value />
                      </hlm-select-trigger>
                      <hlm-select-content *hlmSelectPortal>
                        <hlm-select-item [value]="NONE">Senza motivo</hlm-select-item>
                        @for (o of options(); track o.value) {
                          <hlm-select-item [value]="o.value">{{ o.label }}</hlm-select-item>
                        }
                      </hlm-select-content>
                    </hlm-select>
                  </div>
                  <div class="grid w-28 gap-1.5">
                    <label class="text-muted-foreground text-xs" [for]="'co-r-q-' + r.key">Quantità</label>
                    <input hlmInput class="h-8" [id]="'co-r-q-' + r.key" type="number" step="any" min="0" [value]="r.quantity ?? ''" (input)="setQuantity(r.key, $any($event.target).value)" />
                  </div>
                  <button hlmBtn type="button" variant="ghost" size="icon-sm" [attr.aria-label]="'Rimuovi motivo ' + (i + 1)" (click)="removeRow(r.key)">
                    <ng-icon name="lucideTrash2" />
                  </button>
                </div>
              } @empty {
                <p class="text-muted-foreground text-xs">Nessuno scarto.</p>
              }
              <div>
                <button hlmBtn type="button" variant="outline" size="sm" (click)="addRow()"><ng-icon name="lucidePlus" /> Aggiungi motivo</button>
              </div>
            </fieldset>

            <div class="grid gap-1.5">
              <label hlmLabel for="co-notes">Nota di correzione *</label>
              <textarea hlmTextarea id="co-notes" formControlName="notes" maxlength="1000" [attr.aria-invalid]="fieldError('notes') ? true : null"></textarea>
              @if (fieldError('notes'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="saving()">{{ saving() ? 'Salvataggio…' : 'Salva correzione' }}</button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class WorkOrderCorrectionDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(WorkOrdersStore);
  private readonly reasonsApi = inject(ScrapReasonsService);
  private readonly machines = inject(WorkOrderMachinesService);

  readonly work = input.required<WorkOrderModel>();
  readonly session = input.required<WorkOrderSessionModel>();
  readonly done = output<void>();
  readonly closed = output<void>();

  protected readonly NONE = NONE;
  protected readonly qty = formatQty;
  protected readonly unit = computed(() => unitLabel(this.work().unitOfMeasure));
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});
  protected readonly rows = signal<Row[]>([]);
  private readonly candidates = signal<EquipmentModel[]>([]);
  /** Id della macchina scelta (stringa) o '' se la sessione non ne ha. */
  protected readonly machine = signal('');

  /** Macchine della fase + quella già usata dalla sessione (anche se non più abilitata, con l'istantanea storica). */
  protected readonly machineOptions = computed(() => {
    const map = new Map<string, string>();
    const s = this.session();
    if (s.equipmentId != null) map.set(String(s.equipmentId), `${s.equipmentCode ?? s.equipmentId} · ${s.equipmentName ?? ''}`.trim());
    for (const c of this.candidates()) map.set(String(c.id), machineLabel(c));
    return [...map].map(([value, label]) => ({ value, label }));
  });
  protected readonly machineItemLabel = (value: string): string => this.machineOptions().find((o) => o.value === value)?.label ?? '';

  private readonly catalog = signal<ScrapReasonModel[]>([]);
  private nextKey = 1;

  protected readonly scrapTotal = computed(() => this.rows().reduce((a, r) => a + (r.quantity && r.quantity > 0 ? r.quantity : 0), 0));

  /** Motivi della fase + quelli già usati dalla sessione (anche se non più nel catalogo attivo). */
  protected readonly options = computed(() => {
    const map = new Map<string, string>();
    for (const o of this.catalog()) map.set(String(o.id), `${o.code} · ${o.name}`);
    for (const r of this.rows()) if (r.reason !== NONE && !map.has(r.reason)) map.set(r.reason, r.snapshot ?? r.reason);
    return [...map].map(([value, label]) => ({ value, label }));
  });
  protected readonly reasonLabel = (value: string): string =>
    value === NONE ? 'Senza motivo' : (this.options().find((o) => o.value === value)?.label ?? value);

  protected readonly form = this.fb.group({
    goodQuantity: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    notes: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  async ngOnInit(): Promise<void> {
    const s = this.session();
    this.form.patchValue({ goodQuantity: s.goodQuantity });
    this.machine.set(s.equipmentId != null ? String(s.equipmentId) : '');
    void this.machines
      .candidates(this.work().processSegmentId)
      .then((c) => this.candidates.set(c))
      .catch((e) => this.formError.set(`Macchine non caricate: ${toFailure(e).message}`));
    const rows: Row[] = s.scrapEntries.map((e) => ({
      key: this.nextKey++,
      reason: e.scrapReasonId == null ? NONE : String(e.scrapReasonId),
      quantity: e.quantity,
      snapshot: e.reasonName ? `${e.reasonCode ?? ''} · ${e.reasonName}` : undefined,
    }));
    if (!rows.length && s.scrapQuantity > 0) rows.push({ key: this.nextKey++, reason: NONE, quantity: s.scrapQuantity });
    this.rows.set(rows);
    try {
      this.catalog.set(await firstValueFrom(this.reasonsApi.getScrapReasons({ processSegmentId: this.work().processSegmentId })));
    } catch (e) {
      this.formError.set(`Motivi non caricati: ${toFailure(e).message}`);
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
    if (c.hasError('required')) return name === 'notes' ? 'La nota di correzione è obbligatoria' : 'Campo obbligatorio';
    if (c.hasError('min')) return 'La quantità non può essere negativa';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    const v = this.form.getRawValue();
    if (this.form.invalid || !v.notes.trim()) {
      if (!v.notes.trim()) this.serverErrors.set({ notes: 'La nota di correzione è obbligatoria' });
      return;
    }
    const scrapEntries: ScrapEntryRequest[] = this.rows()
      .filter((r) => (r.quantity ?? 0) > 0)
      .map((r) => ({ scrapReasonId: r.reason === NONE ? null : Number(r.reason), quantity: r.quantity ?? 0 }));
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      await this.store.correct(this.work().id, this.session().id, {
        goodQuantity: v.goodQuantity ?? 0,
        scrapQuantity: this.scrapTotal(),
        scrapEntries,
        // la macchina si invia solo se cambiata rispetto a quella della sessione
        ...(this.machine() && this.machine() !== String(this.session().equipmentId ?? '') ? { equipmentId: Number(this.machine()) } : {}),
        notes: v.notes.trim(),
      });
      toast.success('Registrazione corretta');
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
