import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { toFailure } from '../materials/materials.service';
import { ACTIONS } from './production-order-labels';
import { ProductionOrdersStore } from './production-orders.store';

export interface ConfirmTarget {
  id: number;
  code: string;
}

interface ActionCopy {
  title: string;
  description: (code: string) => string;
  confirm: string;
  pending: string;
  done: string;
  destructive: boolean;
}

const COPY: Record<string, ActionCopy> = {
  [ACTIONS.release]: {
    title: 'Rilasciare l’ordine?',
    description: (c) => `${c} sarà rilasciato e i suoi lavori saranno generati. Non potrai più modificarlo.`,
    confirm: 'Rilascia',
    pending: 'Rilascio…',
    done: 'Ordine rilasciato',
    destructive: false,
  },
  [ACTIONS.cancel]: {
    title: 'Annullare l’ordine?',
    description: (c) => `${c} sarà annullato. L’operazione non si può disfare.`,
    confirm: 'Annulla ordine',
    pending: 'Annullamento…',
    done: 'Ordine annullato',
    destructive: true,
  },
  [ACTIONS.abort]: {
    title: 'Interrompere l’ordine?',
    description: (c) => `${c} sarà interrotto insieme ai lavori ancora aperti. L’operazione non si può disfare.`,
    confirm: 'Interrompi',
    pending: 'Interruzione…',
    done: 'Ordine interrotto',
    destructive: true,
  },
};

/** Conferma di Rilascia / Annulla / Interrompi (con note); mostra l'errore 409/422 del backend nella conferma. */
@Component({
  selector: 'app-production-order-confirm-dialog',
  imports: [HlmAlertDialogImports, HlmButton, HlmLabel, HlmTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-alert-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmAlertDialogPortal>
        <hlm-alert-dialog-content>
          <hlm-alert-dialog-header>
            <h2 hlmAlertDialogTitle>{{ copy().title }}</h2>
            <p hlmAlertDialogDescription>{{ copy().description(order().code) }}</p>
          </hlm-alert-dialog-header>
          @if (action() === 'Abort') {
            <div class="grid gap-1.5">
              <label hlmLabel for="abort-notes">Note</label>
              <textarea hlmTextarea id="abort-notes" maxlength="1000" [value]="notes()" (input)="notes.set($any($event.target).value)"></textarea>
            </div>
          }
          @if (error()) {
            <p class="text-destructive text-sm" role="alert">{{ error() }}</p>
          }
          <hlm-alert-dialog-footer>
            <button hlmAlertDialogCancel>Indietro</button>
            <button hlmBtn type="button" [variant]="copy().destructive ? 'destructive' : 'default'" [disabled]="busy()" (click)="confirm()">
              {{ busy() ? copy().pending : copy().confirm }}
            </button>
          </hlm-alert-dialog-footer>
        </hlm-alert-dialog-content>
      </ng-template>
    </hlm-alert-dialog>
  `,
})
export class ProductionOrderConfirmDialogComponent {
  private readonly store = inject(ProductionOrdersStore);

  readonly order = input.required<ConfirmTarget>();
  readonly action = input.required<string>();
  readonly done = output<void>();
  readonly closed = output<void>();

  protected readonly copy = computed(() => COPY[this.action()]);
  protected readonly notes = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async confirm(): Promise<void> {
    const { id } = this.order();
    this.busy.set(true);
    this.error.set(null);
    try {
      switch (this.action()) {
        case ACTIONS.release:
          await this.store.release(id);
          break;
        case ACTIONS.cancel:
          await this.store.cancel(id);
          break;
        default:
          await this.store.abort(id, { notes: this.notes().trim() || null });
      }
      toast.success(this.copy().done);
      this.done.emit();
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.busy.set(false);
    }
  }
}
