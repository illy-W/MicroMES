import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { ToolingModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { LinkedItemsPickerComponent } from '../../shared/components/linked-items-picker.component';
import { EquipmentStore } from '../equipment/equipment.store';
import { toFailure } from '../materials/materials.service';
import { ToolingStore } from './tooling.store';

@Component({
  selector: 'app-tooling-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel, HlmSwitch, HlmTextarea, LinkedItemsPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ tooling() ? 'Modifica attrezzatura' : 'Nuova attrezzatura' }}</h2>
              <p hlmDialogDescription>I campi contrassegnati con * sono obbligatori.</p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
            }

            <div class="grid gap-1.5">
              <label hlmLabel for="tl-code">Codice *</label>
              <input hlmInput id="tl-code" formControlName="code" maxlength="50" class="uppercase" [attr.aria-invalid]="fieldError('code') ? true : null" [attr.aria-describedby]="fieldError('code') ? 'tl-code-err' : null" />
              @if (fieldError('code'); as e) {
                <p id="tl-code-err" class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="tl-name">Nome *</label>
              <input hlmInput id="tl-name" formControlName="name" maxlength="200" [attr.aria-invalid]="fieldError('name') ? true : null" [attr.aria-describedby]="fieldError('name') ? 'tl-name-err' : null" />
              @if (fieldError('name'); as e) {
                <p id="tl-name-err" class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="tl-desc">Descrizione</label>
              <textarea hlmTextarea id="tl-desc" formControlName="description" maxlength="1000"></textarea>
              @if (fieldError('description'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>
            <div class="grid gap-4 sm:grid-cols-2">
              <div class="grid gap-1.5">
                <label hlmLabel for="tl-qty">Quantità *</label>
                <input hlmInput id="tl-qty" type="number" min="1" step="1" formControlName="quantity" [attr.aria-invalid]="fieldError('quantity') ? true : null" aria-describedby="tl-qty-hint" />
                <p id="tl-qty-hint" class="text-muted-foreground text-xs">Unità identiche disponibili.</p>
                @if (fieldError('quantity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="tl-setup">Minuti di montaggio *</label>
                <input hlmInput id="tl-setup" type="number" min="0" step="any" formControlName="setupMinutes" [attr.aria-invalid]="fieldError('setupMinutes') ? true : null" aria-describedby="tl-setup-hint" />
                <p id="tl-setup-hint" class="text-muted-foreground text-xs">Tempo per montarla su una macchina.</p>
                @if (fieldError('setupMinutes'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </div>

            <app-linked-items-picker
              legend="Macchine compatibili"
              hint="Nessuna selezione = compatibile con tutte le macchine."
              searchLabel="Cerca macchina"
              emptyText="Nessuna macchina trovata."
              idPrefix="tl-eq"
              [items]="equipment.items()"
              [loadError]="equipment.loadError()"
              [(selected)]="selected"
            />
            @if (fieldError('equipmentIds'); as e) {
              <p class="text-destructive text-xs" role="alert">{{ e }}</p>
            }

            @if (tooling()) {
              <div class="flex items-center gap-2">
                <hlm-switch inputId="tl-active" formControlName="isActive" />
                <label hlmLabel for="tl-active">Attiva (selezionabile nei passi del ciclo)</label>
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
export class ToolingFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(ToolingStore);
  protected readonly equipment = inject(EquipmentStore);

  readonly tooling = input<ToolingModel | null>(null);
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
    quantity: [1, [Validators.required, Validators.min(1)]],
    setupMinutes: [0, [Validators.required, Validators.min(0)]],
  });

  ngOnInit(): void {
    void this.equipment.load();
    const t = this.tooling();
    if (t) {
      this.form.patchValue({ code: t.code, name: t.name, description: t.description ?? '', isActive: t.isActive, quantity: t.quantity, setupMinutes: t.setupMinutes });
      void this.store.detail(t.id).then((d) => this.selected.set(new Set(d.equipment.map((e) => e.id))));
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
    if (c.hasError('min')) return name === 'quantity' ? 'Almeno 1' : 'Non può essere negativo';
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
      quantity: Math.trunc(v.quantity),
      setupMinutes: v.setupMinutes,
      equipmentIds: [...this.selected()],
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const t = this.tooling();
      if (t) await this.store.update(t.id, { ...base, isActive: v.isActive });
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
