import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toast } from '@spartan-ng/brain/sonner';
import type { ProductionOrderModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { toFailure } from '../materials/materials.service';
import { unitLabel } from '../materials/material-labels';
import { fromLocalInput, toLocalInput } from './production-order-labels';
import { ProductionOrdersStore } from './production-orders.store';

/** Dialog di creazione e modifica (il materiale non è modificabile). */
@Component({
  selector: 'app-production-order-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmSelectImports, HlmButton, HlmInput, HlmLabel, HlmTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ order() ? 'Modifica ordine ' + order()!.code : 'Nuovo ordine di produzione' }}</h2>
              <p hlmDialogDescription>I campi contrassegnati con * sono obbligatori.</p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
                {{ formError() }}
              </p>
            }

            <div class="grid gap-1.5">
              <label hlmLabel for="po-material">Materiale *</label>
              @if (order(); as o) {
                <input hlmInput id="po-material" [value]="o.materialCode + ' - ' + o.materialName" disabled />
              } @else {
                <hlm-select formControlName="materialId" [itemToString]="materialLabel">
                  <hlm-select-trigger class="w-full" buttonId="po-material">
                    <hlm-select-value [placeholder]="store.materialsLoading() ? 'Caricamento…' : 'Seleziona'" />
                  </hlm-select-trigger>
                  <hlm-select-content *hlmSelectPortal>
                    @for (m of store.materials(); track m.id) {
                      <hlm-select-item [value]="m.id">{{ m.code }} - {{ m.name }}</hlm-select-item>
                    }
                  </hlm-select-content>
                </hlm-select>
                @if (fieldError('materialId'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              }
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div class="grid gap-1.5">
                <label hlmLabel for="po-qty">Quantità * @if (unit()) { ({{ unit() }}) }</label>
                <input hlmInput id="po-qty" type="number" step="any" min="0" formControlName="quantity" [attr.aria-invalid]="fieldError('quantity') ? true : null" />
                @if (fieldError('quantity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="po-priority">Priorità</label>
                <input hlmInput id="po-priority" type="number" step="1" formControlName="priority" />
                @if (fieldError('priority'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div class="grid gap-1.5">
                <label hlmLabel for="po-start">Inizio richiesto</label>
                <input hlmInput id="po-start" type="datetime-local" formControlName="requestedStart" />
                @if (fieldError('requestedStart'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="po-end">Fine richiesta</label>
                <input hlmInput id="po-end" type="datetime-local" formControlName="requestedEnd" />
                @if (fieldError('requestedEnd'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </div>

            <div class="grid gap-1.5">
              <label hlmLabel for="po-notes">Note</label>
              <textarea hlmTextarea id="po-notes" formControlName="notes" maxlength="1000"></textarea>
              @if (fieldError('notes'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="saving()">{{ saving() ? 'Salvataggio…' : 'Salva' }}</button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class ProductionOrderFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly store = inject(ProductionOrdersStore);

  readonly order = input<ProductionOrderModel | null>(null);
  readonly saved = output<number>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});

  protected readonly materialLabel = (id: number): string => {
    const m = this.store.materials().find((x) => x.id === id);
    return m ? `${m.code} - ${m.name}` : String(id);
  };

  protected readonly form = this.fb.group({
    materialId: this.fb.control<number | null>(null, Validators.required),
    quantity: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.0001)]),
    priority: this.fb.control<number>(0),
    requestedStart: [''],
    requestedEnd: [''],
    notes: ['', Validators.maxLength(1000)],
  });

  protected readonly unit = computed(() => {
    const o = this.order();
    return o ? unitLabel(o.unitOfMeasure) : '';
  });

  ngOnInit(): void {
    const o = this.order();
    if (o) {
      this.form.controls.materialId.clearValidators();
      this.form.patchValue({
        materialId: o.materialId,
        quantity: o.quantity,
        priority: o.priority,
        requestedStart: toLocalInput(o.requestedStart),
        requestedEnd: toLocalInput(o.requestedEnd),
        notes: o.notes ?? '',
      });
    } else {
      void this.store.loadMaterials().catch((e) => this.formError.set(toFailure(e).message));
    }
  }

  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    if (c.hasError('min')) return 'La quantità deve essere maggiore di zero';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const base = {
      quantity: v.quantity,
      priority: v.priority ?? 0,
      requestedStart: fromLocalInput(v.requestedStart),
      requestedEnd: fromLocalInput(v.requestedEnd),
      notes: v.notes.trim() || null,
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const o = this.order();
      const result = o
        ? await this.store.update(o.id, base)
        : await this.store.create({ ...base, materialId: v.materialId });
      toast.success(o ? 'Ordine aggiornato' : `Ordine ${result.code} creato`);
      this.saved.emit(result.id);
    } catch (e) {
      const f = toFailure(e);
      if (f.status === 422 && Object.keys(f.fieldErrors).length) {
        this.serverErrors.set(f.fieldErrors);
      } else {
        this.formError.set(f.message);
      }
    } finally {
      this.saving.set(false);
    }
  }
}
