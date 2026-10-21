import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { EquipmentModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { LinkedItemsPickerComponent } from '../../shared/components/linked-items-picker.component';
import { toFailure } from '../materials/materials.service';
import { ProcessSegmentsStore } from '../process-segments/process-segments.store';
import { EquipmentStore } from './equipment.store';

@Component({
  selector: 'app-equipment-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel, HlmSwitch, HlmTextarea, LinkedItemsPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ equipment() ? 'Modifica macchina o postazione' : 'Nuova macchina o postazione' }}</h2>
              <p hlmDialogDescription>I campi contrassegnati con * sono obbligatori.</p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
                {{ formError() }}
              </p>
            }

            <div class="grid gap-1.5">
              <label hlmLabel for="eq-code">Codice *</label>
              <input hlmInput id="eq-code" formControlName="code" maxlength="50" [attr.aria-invalid]="fieldError('code') ? true : null" [attr.aria-describedby]="fieldError('code') ? 'eq-code-err' : null" />
              @if (fieldError('code'); as e) {
                <p id="eq-code-err" class="text-destructive text-xs">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="eq-name">Nome *</label>
              <input hlmInput id="eq-name" formControlName="name" maxlength="200" [attr.aria-invalid]="fieldError('name') ? true : null" [attr.aria-describedby]="fieldError('name') ? 'eq-name-err' : null" />
              @if (fieldError('name'); as e) {
                <p id="eq-name-err" class="text-destructive text-xs">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="eq-desc">Descrizione</label>
              <textarea hlmTextarea id="eq-desc" formControlName="description" maxlength="1000"></textarea>
              @if (fieldError('description'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>

            <app-linked-items-picker
              legend="Fasi di processo"
              hint="Nessuna fase selezionata = la macchina non viene offerta per nessuna fase."
              searchLabel="Cerca fase di processo"
              emptyText="Nessuna fase trovata."
              idPrefix="eq-seg"
              [items]="segments.segments()"
              [loadError]="segments.loadError()"
              [(selected)]="selected"
            />
            @if (fieldError('processSegmentIds'); as e) {
              <p class="text-destructive text-xs" role="alert">{{ e }}</p>
            }

            @if (equipment()) {
              <div class="flex items-center gap-2">
                <hlm-switch inputId="eq-active" formControlName="isActive" />
                <label hlmLabel for="eq-active">Attiva (offerta per le fasi collegate)</label>
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
export class EquipmentFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(EquipmentStore);
  protected readonly segments = inject(ProcessSegmentsStore);

  readonly equipment = input<EquipmentModel | null>(null);
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});
  protected readonly selected = signal<ReadonlySet<number>>(new Set());

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    isActive: [true],
  });

  ngOnInit(): void {
    void this.segments.load();
    const e = this.equipment();
    if (e) {
      this.form.patchValue({ code: e.code, name: e.name, description: e.description ?? '', isActive: e.isActive });
      this.selected.set(new Set(e.processSegments.map((s) => s.id)));
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
    const base = {
      code: v.code.trim().toUpperCase(),
      name: v.name.trim(),
      description: v.description.trim() || null,
      processSegmentIds: [...this.selected()],
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const e = this.equipment();
      if (e) await this.store.update(e.id, { ...base, isActive: v.isActive });
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
