import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { WorkCalendarModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { toFailure } from '../materials/materials.service';
import { daysToSlots, hasErrors, slotsToDays, TimeRange, validateRanges, WEEK_DAYS } from './calendar-time';
import { CalendarsStore } from './calendars.store';
import { WeeklyEditorComponent } from './weekly-editor.component';

@Component({
  selector: 'app-calendar-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel, HlmSwitch, HlmTextarea, WeeklyEditorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ calendar() ? 'Modifica calendario' : 'Nuovo calendario' }}</h2>
              <p hlmDialogDescription>
                Orari settimanali di lavoro. I giorni senza fasce sono chiusi. I campi contrassegnati con * sono obbligatori.
              </p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
            }

            <div class="grid gap-4 sm:grid-cols-2">
              <div class="grid gap-1.5">
                <label hlmLabel for="cal-code">Codice *</label>
                <input hlmInput id="cal-code" formControlName="code" maxlength="50" [attr.aria-invalid]="fieldError('code') ? true : null" [attr.aria-describedby]="fieldError('code') ? 'cal-code-err' : null" />
                @if (fieldError('code'); as e) {
                  <p id="cal-code-err" class="text-destructive text-xs">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="cal-name">Nome *</label>
                <input hlmInput id="cal-name" formControlName="name" maxlength="200" [attr.aria-invalid]="fieldError('name') ? true : null" [attr.aria-describedby]="fieldError('name') ? 'cal-name-err' : null" />
                @if (fieldError('name'); as e) {
                  <p id="cal-name-err" class="text-destructive text-xs">{{ e }}</p>
                }
              </div>
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="cal-desc">Descrizione</label>
              <textarea hlmTextarea id="cal-desc" formControlName="description" maxlength="1000"></textarea>
            </div>

            <fieldset class="grid gap-2">
              <legend class="mb-1 text-sm font-medium">Orari settimanali *</legend>
              <app-weekly-editor [(value)]="days" />
              @if (submitted() && weekError(); as e) {
                <p class="text-destructive text-sm" role="alert">{{ e }}</p>
              }
              @if (fieldError('slots'); as e) {
                <p class="text-destructive text-sm" role="alert">{{ e }}</p>
              }
            </fieldset>

            @if (calendar()) {
              <div class="flex items-center gap-2">
                <hlm-switch inputId="cal-active" formControlName="isActive" />
                <label hlmLabel for="cal-active">Attivo (assegnabile alle macchine)</label>
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
export class CalendarFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(CalendarsStore);

  readonly calendar = input<WorkCalendarModel | null>(null);
  readonly saved = output<WorkCalendarModel>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});
  protected readonly days = signal<TimeRange[][]>(WEEK_DAYS.map(() => []));

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    isActive: [true],
  });

  protected readonly weekError = computed(() => {
    const d = this.days();
    if (d.some((r) => hasErrors(validateRanges(r)))) return 'Correggi le fasce orarie evidenziate.';
    if (!d.some((r) => r.length)) return 'Inserisci almeno una fascia oraria in un giorno della settimana.';
    return null;
  });

  ngOnInit(): void {
    const c = this.calendar();
    if (c) {
      this.form.patchValue({ code: c.code, name: c.name, description: c.description ?? '', isActive: c.isActive });
      this.days.set(slotsToDays(c.slots));
    } else {
      this.days.set(WEEK_DAYS.map((_, i) => (i < 5 ? [{ start: '08:00', end: '12:00' }, { start: '13:00', end: '17:00' }] : [])));
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
    this.submitted.set(true);
    if (this.form.invalid || this.weekError()) return;
    const v = this.form.getRawValue();
    const base = {
      code: v.code.trim().toUpperCase(),
      name: v.name.trim(),
      description: v.description.trim() || null,
      slots: daysToSlots(this.days()),
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const c = this.calendar();
      const result = c ? await this.store.update(c.id, { ...base, isActive: v.isActive }) : await this.store.create(base);
      this.saved.emit(result);
    } catch (err) {
      const f = toFailure(err);
      if (f.status === 409 && /codice|code/i.test(f.message)) this.serverErrors.set({ code: f.message });
      else if (f.status === 422 && Object.keys(f.fieldErrors).length) {
        this.serverErrors.set(f.fieldErrors);
        this.formError.set(f.message);
      } else this.formError.set(f.message);
    } finally {
      this.saving.set(false);
    }
  }
}
