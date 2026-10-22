import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { SetupAttributeModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { toFailure } from '../materials/materials.service';
import { SetupAttributesStore } from './setup-attributes.store';

@Component({
  selector: 'app-setup-attribute-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel, HlmSwitch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ attribute() ? 'Modifica attributo di cambio' : 'Nuovo attributo di cambio' }}</h2>
              <p hlmDialogDescription>I campi contrassegnati con * sono obbligatori.</p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
            }

            <div class="grid gap-1.5">
              <label hlmLabel for="sa-code">Codice *</label>
              <input hlmInput id="sa-code" formControlName="code" maxlength="50" class="uppercase" [attr.aria-invalid]="fieldError('code') ? true : null" [attr.aria-describedby]="fieldError('code') ? 'sa-code-err' : null" />
              @if (fieldError('code'); as e) {
                <p id="sa-code-err" class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="sa-name">Nome *</label>
              <input hlmInput id="sa-name" formControlName="name" maxlength="200" [attr.aria-invalid]="fieldError('name') ? true : null" [attr.aria-describedby]="fieldError('name') ? 'sa-name-err' : null" />
              @if (fieldError('name'); as e) {
                <p id="sa-name-err" class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="sa-min">Minuti di cambio *</label>
              <input hlmInput id="sa-min" type="number" min="0" step="any" formControlName="changeMinutes" [attr.aria-invalid]="fieldError('changeMinutes') ? true : null" aria-describedby="sa-min-hint" />
              <p id="sa-min-hint" class="text-muted-foreground text-xs">Minuti aggiunti quando il valore cambia tra due lavori consecutivi sulla stessa macchina.</p>
              @if (fieldError('changeMinutes'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>

            @if (attribute()) {
              <div class="flex items-center gap-2">
                <hlm-switch inputId="sa-active" formControlName="isActive" />
                <label hlmLabel for="sa-active">Attivo (selezionabile nei passi del ciclo)</label>
              </div>
            }

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
export class SetupAttributeFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(SetupAttributesStore);

  readonly attribute = input<SetupAttributeModel | null>(null);
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    changeMinutes: [0, [Validators.required, Validators.min(0)]],
    isActive: [true],
  });

  ngOnInit(): void {
    const a = this.attribute();
    if (a) this.form.patchValue({ code: a.code, name: a.name, changeMinutes: a.changeMinutes, isActive: a.isActive });
  }

  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    if (c.hasError('min')) return 'Non può essere negativo';
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const base = { code: v.code.trim().toUpperCase(), name: v.name.trim(), changeMinutes: v.changeMinutes };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const a = this.attribute();
      if (a) await this.store.update(a.id, { ...base, isActive: v.isActive });
      else await this.store.create(base);
      this.saved.emit();
    } catch (err) {
      const f = toFailure(err);
      if (f.status === 409) this.serverErrors.set({ code: f.message });
      else if (f.status === 422) {
        this.serverErrors.set(f.fieldErrors);
        if (!Object.keys(f.fieldErrors).length) this.formError.set(f.message);
      } else this.formError.set(f.message);
    } finally {
      this.saving.set(false);
    }
  }
}
