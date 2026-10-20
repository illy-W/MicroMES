import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { BomNodeModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { MaterialsStore, toFailure } from '../materials/materials.service';
import { BomStore } from './bom.store';

const MIN_QUANTITY = 0.0001;
const MAX_QUANTITY = 99999999999999;

/** Dialog unico: "Aggiungi componente" (senza `item`) o "Modifica quantità" (con `item`). */
@Component({
  selector: 'app-bom-item-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmSelectImports, HlmButton, HlmInput, HlmLabel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-md">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ item() ? 'Modifica quantità' : 'Aggiungi componente' }}</h2>
              <p hlmDialogDescription>
                @if (item(); as i) {
                  {{ i.code }} — {{ i.name }}
                } @else {
                  I campi contrassegnati con * sono obbligatori.
                }
              </p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
                {{ formError() }}
              </p>
            }

            @if (!item()) {
              <div class="grid gap-1.5">
                <label hlmLabel for="b-comp">Componente *</label>
                <hlm-select formControlName="componentId">
                  <hlm-select-trigger class="w-full" buttonId="b-comp">
                    <hlm-select-value [placeholder]="materials.loading() ? 'Caricamento…' : 'Seleziona'" />
                  </hlm-select-trigger>
                  <hlm-select-content *hlmSelectPortal>
                    @for (m of candidates(); track m.id) {
                      <hlm-select-item [value]="m.id">{{ m.code }} - {{ m.name }}</hlm-select-item>
                    }
                  </hlm-select-content>
                </hlm-select>
                @if (fieldError('componentId'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            }

            <div class="grid gap-1.5">
              <label hlmLabel for="b-qty">Quantità *</label>
              <input
                hlmInput
                id="b-qty"
                type="number"
                inputmode="decimal"
                step="any"
                class="tabular-nums"
                formControlName="quantity"
                [attr.aria-invalid]="fieldError('quantity') ? true : null"
                [attr.aria-describedby]="fieldError('quantity') ? 'b-qty-err' : null"
              />
              @if (fieldError('quantity'); as e) {
                <p id="b-qty-err" class="text-destructive text-xs">{{ e }}</p>
              }
            </div>

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="saving()">
                {{ saving() ? 'Salvataggio…' : 'Salva' }}
              </button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class BomItemDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(BomStore);
  protected readonly materials = inject(MaterialsStore);

  /** Riga da modificare; assente in aggiunta. */
  readonly item = input<BomNodeModel | null>(null);
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});

  protected readonly candidates = computed(() => {
    const selfId = this.store.material()?.id;
    return this.materials.materials().filter((m) => m.isActive && m.id !== selfId);
  });

  protected readonly form = this.fb.group({
    componentId: this.fb.control<number | null>(null),
    quantity: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(MIN_QUANTITY),
      Validators.max(MAX_QUANTITY),
    ]),
  });

  ngOnInit(): void {
    const i = this.item();
    if (i) {
      this.form.patchValue({ quantity: i.quantity });
    } else {
      this.form.controls.componentId.addValidators(Validators.required);
      this.form.controls.componentId.updateValueAndValidity();
      void this.materials.load();
    }
  }

  protected fieldError(name: 'componentId' | 'quantity'): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.controls[name];
    if (!(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    if (c.hasError('min')) return `Il valore minimo è ${MIN_QUANTITY}`;
    if (c.hasError('max')) return 'Valore troppo grande';
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const { componentId, quantity } = this.form.getRawValue();
    if (quantity === null) return;
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const i = this.item();
      if (i) {
        await this.store.updateQuantity(i.id, quantity);
      } else {
        if (componentId === null) return;
        await this.store.add(componentId, quantity);
      }
      this.saved.emit();
    } catch (e) {
      const f = toFailure(e);
      if (f.status === 422 && Object.keys(f.fieldErrors).length) {
        const { componentMaterialId, ...rest } = f.fieldErrors;
        this.serverErrors.set(componentMaterialId ? { ...rest, componentId: componentMaterialId } : rest);
        if (!('quantity' in rest) && !componentMaterialId) this.formError.set(f.message);
      } else {
        this.formError.set(f.message);
      }
    } finally {
      this.saving.set(false);
    }
  }
}
