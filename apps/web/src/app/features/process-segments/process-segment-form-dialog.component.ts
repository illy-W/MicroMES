import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { ProcessSegmentModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { toFailure } from '../materials/materials.service';
import { ProcessSegmentsStore } from './process-segments.store';

@Component({
  selector: 'app-process-segment-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel, HlmSwitch, HlmTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ segment() ? 'Modifica fase' : 'Nuova fase' }}</h2>
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
                <label hlmLabel for="ps-code">Codice *</label>
                <input hlmInput id="ps-code" formControlName="code" maxlength="50" [attr.aria-invalid]="fieldError('code') ? true : null" [attr.aria-describedby]="fieldError('code') ? 'ps-code-err' : null" />
                @if (fieldError('code'); as e) {
                  <p id="ps-code-err" class="text-destructive text-xs">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="ps-name">Nome *</label>
                <input hlmInput id="ps-name" formControlName="name" maxlength="200" [attr.aria-invalid]="fieldError('name') ? true : null" [attr.aria-describedby]="fieldError('name') ? 'ps-name-err' : null" />
                @if (fieldError('name'); as e) {
                  <p id="ps-name-err" class="text-destructive text-xs">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="ps-desc">Descrizione</label>
                <textarea hlmTextarea id="ps-desc" formControlName="description" maxlength="1000"></textarea>
                @if (fieldError('description'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </fieldset>

            <fieldset class="grid grid-cols-2 gap-4">
              <legend class="col-span-2 text-sm font-medium">Tempi</legend>
              <div class="grid gap-1.5">
                <label hlmLabel for="ps-setup">Attrezzaggio (min)</label>
                <input hlmInput id="ps-setup" type="number" min="0" step="0.01" inputmode="decimal" formControlName="setupMinutes" [attr.aria-invalid]="fieldError('setupMinutes') ? true : null" [attr.aria-describedby]="fieldError('setupMinutes') ? 'ps-setup-err' : null" />
                @if (fieldError('setupMinutes'); as e) {
                  <p id="ps-setup-err" class="text-destructive text-xs">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="ps-run">Tempo per unità (min)</label>
                <input hlmInput id="ps-run" type="number" min="0" step="0.0001" inputmode="decimal" formControlName="runMinutesPerUnit" [attr.aria-invalid]="fieldError('runMinutesPerUnit') ? true : null" [attr.aria-describedby]="fieldError('runMinutesPerUnit') ? 'ps-run-err' : null" />
                @if (fieldError('runMinutesPerUnit'); as e) {
                  <p id="ps-run-err" class="text-destructive text-xs">{{ e }}</p>
                }
              </div>
            </fieldset>

            @if (segment()) {
              <div class="flex items-center gap-2">
                <hlm-switch inputId="ps-active" formControlName="isActive" />
                <label hlmLabel for="ps-active">Attiva (selezionabile nei cicli)</label>
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
export class ProcessSegmentFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(ProcessSegmentsStore);

  readonly segment = input<ProcessSegmentModel | null>(null);
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    setupMinutes: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    runMinutesPerUnit: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    isActive: [true],
  });

  ngOnInit(): void {
    const s = this.segment();
    if (s) {
      this.form.patchValue({
        code: s.code,
        name: s.name,
        description: s.description ?? '',
        setupMinutes: s.setupMinutes,
        runMinutesPerUnit: s.runMinutesPerUnit,
        isActive: s.isActive,
      });
    }
  }

  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    if (c.hasError('min')) return 'Il valore non può essere negativo';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const base = {
      code: v.code.trim(),
      name: v.name.trim(),
      description: v.description.trim() || null,
      setupMinutes: v.setupMinutes ?? 0,
      runMinutesPerUnit: v.runMinutesPerUnit ?? 0,
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const s = this.segment();
      if (s) {
        await this.store.update(s.id, { ...base, isActive: v.isActive });
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
