import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toast } from '@spartan-ng/brain/sonner';
import type { WorkOrderModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { formatQty } from '../production-orders/production-order-labels';
import { WorkOrdersStore } from './work-orders.store';

export type CloseMode = 'Pause' | 'Stop';

const COPY: Record<CloseMode, { title: string; submit: string; pending: string; done: string }> = {
  Pause: { title: 'Metti in pausa', submit: 'Metti in pausa', pending: 'Salvataggio…', done: 'Lavoro messo in pausa' },
  Stop: { title: 'Chiudi lavoro', submit: 'Chiudi lavoro', pending: 'Chiusura…', done: 'Lavoro chiuso' },
};

/** Dialog di pausa / chiusura: registra buono, scarto e note della sessione; mostra gli errori del backend. */
@Component({
  selector: 'app-work-order-close-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmInput, HlmLabel, HlmTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-md">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ copy().title }} · {{ work().code }}</h2>
              <p hlmDialogDescription>Registra le quantità prodotte in questa sessione di lavoro.</p>
            </hlm-dialog-header>

            <dl class="bg-muted grid grid-cols-3 gap-2 rounded-md p-3 text-sm">
              <div>
                <dt class="text-muted-foreground text-xs">Pianificata</dt>
                <dd class="tabular-nums">{{ qty(work().plannedQuantity) }} {{ unit() }}</dd>
              </div>
              <div>
                <dt class="text-muted-foreground text-xs">Già buona</dt>
                <dd class="tabular-nums">{{ qty(work().goodQuantity) }} {{ unit() }}</dd>
              </div>
              <div>
                <dt class="text-muted-foreground text-xs">Già scarto</dt>
                <dd class="tabular-nums">{{ qty(work().scrapQuantity) }} {{ unit() }}</dd>
              </div>
            </dl>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
                {{ formError() }}
              </p>
            }

            <div class="grid grid-cols-2 gap-4">
              <div class="grid gap-1.5">
                <label hlmLabel for="wo-good">Quantità buona</label>
                <input hlmInput id="wo-good" type="number" step="any" min="0" formControlName="goodQuantity" [attr.aria-invalid]="fieldError('goodQuantity') ? true : null" />
                @if (fieldError('goodQuantity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
              <div class="grid gap-1.5">
                <label hlmLabel for="wo-scrap">Scarto</label>
                <input hlmInput id="wo-scrap" type="number" step="any" min="0" formControlName="scrapQuantity" [attr.aria-invalid]="fieldError('scrapQuantity') ? true : null" />
                @if (fieldError('scrapQuantity'); as e) {
                  <p class="text-destructive text-xs" role="alert">{{ e }}</p>
                }
              </div>
            </div>

            <div class="grid gap-1.5">
              <label hlmLabel for="wo-notes">Note</label>
              <textarea hlmTextarea id="wo-notes" formControlName="notes" maxlength="1000"></textarea>
              @if (fieldError('notes'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="saving()">{{ saving() ? copy().pending : copy().submit }}</button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class WorkOrderCloseDialogComponent {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(WorkOrdersStore);

  readonly work = input.required<WorkOrderModel>();
  readonly mode = input.required<CloseMode>();
  readonly done = output<void>();
  readonly closed = output<void>();

  protected readonly qty = formatQty;
  protected readonly copy = computed(() => COPY[this.mode()]);
  protected readonly unit = computed(() => unitLabel(this.work().unitOfMeasure));
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});

  protected readonly form = this.fb.group({
    goodQuantity: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    scrapQuantity: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    notes: ['', Validators.maxLength(1000)],
  });

  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    if (c.hasError('min')) return 'La quantità non può essere negativa';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const req = { goodQuantity: v.goodQuantity ?? 0, scrapQuantity: v.scrapQuantity ?? 0, notes: v.notes.trim() || null };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const id = this.work().id;
      if (this.mode() === 'Pause') await this.store.pause(id, req);
      else await this.store.stop(id, req);
      toast.success(this.copy().done);
      this.done.emit();
    } catch (e) {
      const f = toFailure(e);
      if (f.status === 422 && Object.keys(f.fieldErrors).length) this.serverErrors.set(f.fieldErrors);
      else this.formError.set(f.message);
    } finally {
      this.saving.set(false);
    }
  }
}
