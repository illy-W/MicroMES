import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { WorkCalendarModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { toFailure } from '../materials/materials.service';
import { CalendarsStore } from './calendars.store';

@Component({
  selector: 'app-calendar-duplicate-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-md">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>Duplica calendario</h2>
              <p hlmDialogDescription>Crea una copia di «{{ source().name }}» con le stesse fasce settimanali. Le eccezioni non vengono copiate.</p>
            </hlm-dialog-header>
            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
            }
            <div class="grid gap-1.5">
              <label hlmLabel for="dup-code">Codice *</label>
              <input hlmInput id="dup-code" formControlName="code" maxlength="50" />
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="dup-name">Nome *</label>
              <input hlmInput id="dup-name" formControlName="name" maxlength="200" />
            </div>
            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="saving()">{{ saving() ? 'Duplicazione…' : 'Duplica' }}</button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class CalendarDuplicateDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(CalendarsStore);

  readonly source = input.required<WorkCalendarModel>();
  readonly saved = output<WorkCalendarModel>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
  });

  ngOnInit(): void {
    const s = this.source();
    this.form.patchValue({ code: `${s.code}-COPIA`.slice(0, 50), name: `${s.name} (copia)`.slice(0, 200) });
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.formError.set(null);
    try {
      this.saved.emit(await this.store.duplicate(this.source().id, { code: v.code.trim().toUpperCase(), name: v.name.trim() }));
    } catch (err) {
      this.formError.set(toFailure(err).message);
    } finally {
      this.saving.set(false);
    }
  }
}
