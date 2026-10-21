import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { MaterialModel, MaterialType, UnitOfMeasure } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { MATERIAL_TYPE_OPTIONS, UNIT_OPTIONS } from './material-labels';
import { MaterialsStore, toFailure } from './materials.service';

@Component({
  selector: 'app-material-form-dialog',
  imports: [
    ReactiveFormsModule,
    HlmDialogImports,
    HlmSelectImports,
    HlmButton,
    HlmInput,
    HlmLabel,
    HlmSwitch,
    HlmTextarea,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ material() ? 'Modifica materiale' : 'Nuovo materiale' }}</h2>
              <p hlmDialogDescription>I campi contrassegnati con * sono obbligatori.</p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
                {{ formError() }}
              </p>
            }

            <fieldset class="grid gap-4">
            <legend class="mb-3 text-sm font-medium">Identificazione</legend>
            <div class="grid gap-1.5">
              <label hlmLabel for="m-code">Codice *</label>
              <input hlmInput id="m-code" [attr.aria-invalid]="fieldError('code') ? true : null" [attr.aria-describedby]="fieldError('code') ? 'm-code-err' : null" formControlName="code" maxlength="50" />
              @if (fieldError('code'); as e) {
                <p id="m-code-err" class="text-destructive text-xs">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="m-name">Nome *</label>
              <input hlmInput id="m-name" [attr.aria-invalid]="fieldError('name') ? true : null" [attr.aria-describedby]="fieldError('name') ? 'm-name-err' : null" formControlName="name" maxlength="200" />
              @if (fieldError('name'); as e) {
                <p id="m-name-err" class="text-destructive text-xs">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="m-desc">Descrizione</label>
              <textarea hlmTextarea id="m-desc" formControlName="description" maxlength="1000"></textarea>
              @if (fieldError('description'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>
            </fieldset>
            <fieldset class="grid grid-cols-2 gap-4">
              <legend class="col-span-2 text-sm font-medium">Classificazione</legend>
              <div class="grid gap-1.5">
                <label hlmLabel for="m-type">Tipo *</label>
                <hlm-select formControlName="type" [itemToString]="typeLabel">
                  <hlm-select-trigger class="w-full" buttonId="m-type">
                    <hlm-select-value placeholder="Seleziona" />
                  </hlm-select-trigger>
                  <hlm-select-content *hlmSelectPortal>
                    @for (o of types; track o.value) {
                      <hlm-select-item [value]="o.value">{{ o.label }}</hlm-select-item>
                    }
                  </hlm-select-content>
                </hlm-select>
                @if (fieldError('type'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="m-unit">Unità *</label>
                <hlm-select formControlName="unitOfMeasure" [itemToString]="unitOptionLabel">
                  <hlm-select-trigger class="w-full" buttonId="m-unit">
                    <hlm-select-value placeholder="Seleziona" />
                  </hlm-select-trigger>
                  <hlm-select-content *hlmSelectPortal>
                    @for (o of units; track o.value) {
                      <hlm-select-item [value]="o.value">{{ o.label }}</hlm-select-item>
                    }
                  </hlm-select-content>
                </hlm-select>
                @if (fieldError('unitOfMeasure'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </fieldset>
            <fieldset class="grid grid-cols-2 gap-4">
              <legend class="col-span-2 text-sm font-medium">Fornitore</legend>
              <div class="grid gap-1.5">
                <label hlmLabel for="m-vendor">Fornitore</label>
                <input hlmInput id="m-vendor" [attr.aria-invalid]="fieldError('vendor') ? true : null" [attr.aria-describedby]="fieldError('vendor') ? 'm-vendor-err' : null" formControlName="vendor" maxlength="200" />
                @if (fieldError('vendor'); as e) {
                  <p id="m-vendor-err" class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="m-vmid">Cod. fornitore</label>
                <input hlmInput id="m-vmid" [attr.aria-invalid]="fieldError('vendorMaterialId') ? true : null" [attr.aria-describedby]="fieldError('vendorMaterialId') ? 'm-vmid-err' : null" formControlName="vendorMaterialId" maxlength="100" />
                @if (fieldError('vendorMaterialId'); as e) {
                  <p id="m-vmid-err" class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </fieldset>
            @if (material()) {
              <div class="flex items-center gap-2">
                <hlm-switch inputId="m-active" formControlName="isActive" />
                <label hlmLabel for="m-active">Attivo (selezionabile nelle lavorazioni)</label>
              </div>
            }

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
export class MaterialFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(MaterialsStore);

  readonly material = input<MaterialModel | null>(null);
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly types = MATERIAL_TYPE_OPTIONS;
  protected readonly units = UNIT_OPTIONS;
  protected readonly typeLabel = (value: string): string => this.types.find((o) => o.value === value)?.label ?? String(value);
  protected readonly unitOptionLabel = (value: string): string => this.units.find((o) => o.value === value)?.label ?? String(value);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    type: this.fb.control<MaterialType | null>(null, Validators.required),
    unitOfMeasure: this.fb.control<UnitOfMeasure | null>(null, Validators.required),
    vendor: ['', Validators.maxLength(200)],
    vendorMaterialId: ['', Validators.maxLength(100)],
    isActive: [true],
  });

  ngOnInit(): void {
    const m = this.material();
    if (m) {
      this.form.patchValue({
        code: m.code,
        name: m.name,
        description: m.description ?? '',
        type: m.type,
        unitOfMeasure: m.unitOfMeasure,
        vendor: m.vendor ?? '',
        vendorMaterialId: m.vendorMaterialId ?? '',
        isActive: m.isActive,
      });
    }
  }

  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const { type, unitOfMeasure } = v;
    if (!type || !unitOfMeasure) return;
    const base = {
      code: v.code.trim(),
      name: v.name.trim(),
      description: v.description.trim() || null,
      type,
      unitOfMeasure,
      vendor: v.vendor.trim() || null,
      vendorMaterialId: v.vendorMaterialId.trim() || null,
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const m = this.material();
      if (m) {
        await this.store.update(m.id, { ...base, isActive: v.isActive });
      } else {
        await this.store.create(base);
      }
      this.saved.emit();
    } catch (e) {
      const f = toFailure(e);
      if (f.status === 409) {
        this.serverErrors.set({ code: f.message });
      } else if (f.status === 422) {
        this.serverErrors.set(f.fieldErrors);
        if (!Object.keys(f.fieldErrors).length) this.formError.set(f.message);
      } else {
        this.formError.set(f.message);
      }
    } finally {
      this.saving.set(false);
    }
  }
}
