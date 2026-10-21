import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { EquipmentModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { LinkedItemsPickerComponent } from '../../shared/components/linked-items-picker.component';
import { CalendarsStore } from '../calendars/calendars.store';
import { formatHours } from '../calendars/calendar-time';
import { toFailure } from '../materials/materials.service';
import { ProcessSegmentsStore } from '../process-segments/process-segments.store';
import { EquipmentStore } from './equipment.store';

@Component({
  selector: 'app-equipment-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel, HlmSelectImports, HlmSwitch, HlmTextarea, LinkedItemsPickerComponent],
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

            <div class="grid gap-1.5">
              <label hlmLabel for="eq-cal">Calendario di lavoro</label>
              <hlm-select [value]="calendarSel()" [itemToString]="calendarLabel" (valueChange)="calendarSel.set($event ?? 0)">
                <hlm-select-trigger id="eq-cal" class="w-full" aria-label="Calendario di lavoro">
                  <hlm-select-value />
                </hlm-select-trigger>
                <hlm-select-content *hlmSelectPortal>
                  <hlm-select-item [value]="0">Predefinito aziendale</hlm-select-item>
                  @for (c of calendarOptions(); track c.id) {
                    <hlm-select-item [value]="c.id">{{ c.name }} · {{ hours(c.weeklyHours) }}/sett.{{ c.isActive ? '' : ' (disattivato)' }}</hlm-select-item>
                  }
                </hlm-select-content>
              </hlm-select>
              @if (fieldError('workCalendarId'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>
            <div class="grid gap-4 sm:grid-cols-2">
              <div class="grid gap-1.5">
                <label hlmLabel for="eq-cap">Capacità</label>
                <input hlmInput id="eq-cap" type="number" min="1" max="1000" step="1" formControlName="capacity" [attr.aria-invalid]="fieldError('capacity') ? true : null" aria-describedby="eq-cap-hint" />
                <p id="eq-cap-hint" class="text-muted-foreground text-xs">Numero di lavorazioni simultanee.</p>
                @if (fieldError('capacity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="eq-eff">Efficienza (%)</label>
                <input hlmInput id="eq-eff" type="number" min="1" max="1000" step="1" formControlName="efficiencyPercent" [attr.aria-invalid]="fieldError('efficiencyPercent') ? true : null" aria-describedby="eq-eff-hint" />
                <p id="eq-eff-hint" class="text-muted-foreground text-xs">80% = più lenta dello standard, 120% = più veloce.</p>
                @if (fieldError('efficiencyPercent'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
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
  private readonly calendars = inject(CalendarsStore);

  readonly equipment = input<EquipmentModel | null>(null);
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});
  protected readonly selected = signal<ReadonlySet<number>>(new Set());

  /** 0 = predefinito aziendale (workCalendarId null). */
  protected readonly calendarSel = signal(0);
  protected readonly calendarOptions = computed(() => {
    const current = this.equipment()?.workCalendarId;
    return this.calendars.items().filter((c) => c.isActive || c.id === current);
  });
  protected readonly hours = (weekly: number): string => formatHours(weekly * 60);
  protected readonly calendarLabel = (v: number | null): string => (v ? (this.calendars.items().find((c) => c.id === v)?.name ?? '') : 'Predefinito aziendale');

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    isActive: [true],
    capacity: [1, [Validators.required, Validators.min(1), Validators.max(1000)]],
    efficiencyPercent: [100, [Validators.required, Validators.min(0.01), Validators.max(1000)]],
  });

  ngOnInit(): void {
    void this.segments.load();
    void this.calendars.load();
    const e = this.equipment();
    if (e) {
      this.form.patchValue({ code: e.code, name: e.name, description: e.description ?? '', isActive: e.isActive, capacity: e.capacity, efficiencyPercent: e.efficiencyPercent });
      this.calendarSel.set(e.workCalendarId ?? 0);
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
    if (c.hasError('min')) return name === 'capacity' ? 'Almeno 1' : 'Deve essere maggiore di 0';
    if (c.hasError('max')) return 'Valore troppo alto (massimo 1000)';
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
      // Sempre inviati: sull'aggiornamento un workCalendarId assente reimposterebbe il predefinito.
      workCalendarId: this.calendarSel() || null,
      capacity: Math.trunc(v.capacity),
      efficiencyPercent: v.efficiencyPercent,
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
