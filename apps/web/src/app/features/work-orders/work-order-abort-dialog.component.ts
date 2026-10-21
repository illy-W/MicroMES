import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { toast } from '@spartan-ng/brain/sonner';
import type { WorkOrderModel } from '@micromes/api-client/models';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { toFailure } from '../materials/materials.service';
import { WorkOrdersStore } from './work-orders.store';

/** Conferma di interruzione del lavoro (destructive) con note; l'errore 409 resta nella conferma. */
@Component({
  selector: 'app-work-order-abort-dialog',
  imports: [HlmAlertDialogImports, HlmButton, HlmLabel, HlmTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-alert-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmAlertDialogPortal>
        <hlm-alert-dialog-content>
          <hlm-alert-dialog-header>
            <h2 hlmAlertDialogTitle>Interrompere il lavoro?</h2>
            <p hlmAlertDialogDescription>{{ work().code }} sarà interrotto. L’operazione non si può disfare.</p>
          </hlm-alert-dialog-header>
          <div class="grid gap-1.5">
            <label hlmLabel for="wo-abort-notes">Note</label>
            <textarea hlmTextarea id="wo-abort-notes" maxlength="1000" [value]="notes()" (input)="notes.set($any($event.target).value)"></textarea>
          </div>
          @if (error()) {
            <p class="text-destructive text-sm" role="alert">{{ error() }}</p>
          }
          <hlm-alert-dialog-footer>
            <button hlmAlertDialogCancel>Indietro</button>
            <button hlmBtn type="button" variant="destructive" [disabled]="busy()" (click)="confirm()">
              {{ busy() ? 'Interruzione…' : 'Interrompi' }}
            </button>
          </hlm-alert-dialog-footer>
        </hlm-alert-dialog-content>
      </ng-template>
    </hlm-alert-dialog>
  `,
})
export class WorkOrderAbortDialogComponent {
  private readonly store = inject(WorkOrdersStore);

  readonly work = input.required<WorkOrderModel>();
  readonly done = output<void>();
  readonly closed = output<void>();

  protected readonly notes = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async confirm(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.store.abort(this.work().id, { notes: this.notes().trim() || null });
      toast.success('Lavoro interrotto');
      this.done.emit();
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.busy.set(false);
    }
  }
}
