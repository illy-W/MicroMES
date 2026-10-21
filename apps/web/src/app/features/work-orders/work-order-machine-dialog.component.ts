import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, input, OnInit, output, signal, viewChild } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideTriangleAlert } from '@ng-icons/lucide';
import type { EquipmentModel, WorkOrderModel } from '@micromes/api-client/models';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { toFailure } from '../materials/materials.service';
import { machineLabel, type MachineUsage, usageText, WorkOrderMachinesService } from './work-order-machines';
import { WorkOrdersStore } from './work-orders.store';

export type MachineMode = 'Start' | 'Resume' | 'Assign';

const NONE = 'none';

const COPY: Record<MachineMode, { title: string; description: string; submit: string; pending: string; done: (code: string) => string }> = {
  Start: {
    title: 'Avvia lavoro',
    description: 'Scegli la macchina su cui eseguire la fase.',
    submit: 'Avvia',
    pending: 'Avvio…',
    done: (c) => `Lavoro ${c} avviato`,
  },
  Resume: {
    title: 'Riprendi lavoro',
    description: 'Scegli la macchina su cui riprendere la fase.',
    submit: 'Riprendi',
    pending: 'Ripresa…',
    done: (c) => `Lavoro ${c} ripreso`,
  },
  Assign: {
    title: 'Assegna macchina',
    description: 'La macchina assegnata viene proposta all’avvio del lavoro. Puoi anche rimuovere l’assegnazione.',
    submit: 'Assegna',
    pending: 'Salvataggio…',
    done: (c) => `Macchina aggiornata per ${c}`,
  },
};

/**
 * Scelta della macchina per avvio, ripresa e assegnazione. Le macchine candidate sono quelle attive collegate alla
 * fase; l'avviso «in uso» (lavori `Running`) è solo informativo. Se la macchina è già precompilata e libera,
 * il focus va sul pulsante di conferma (un clic / Invio).
 */
@Component({
  selector: 'app-work-order-machine-dialog',
  imports: [HlmDialogImports, HlmSelectImports, HlmButton, HlmSkeleton, NgIcon],
  providers: [provideIcons({ lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-md">
          <form class="grid gap-4" (submit)="$event.preventDefault(); submit()">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ copy().title }} · {{ work().code }}</h2>
              <p hlmDialogDescription>{{ copy().description }}</p>
            </hlm-dialog-header>

            @if (loading()) {
              <hlm-skeleton class="h-8 w-full" aria-busy="true" aria-label="Caricamento macchine" />
            } @else {
              @if (formError()) {
                <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
              }
              @if (staleAssigned(); as eq) {
                <p class="text-muted-foreground flex items-start gap-2 text-sm" role="status">
                  <ng-icon name="lucideTriangleAlert" class="mt-0.5" aria-hidden="true" />
                  <span>La macchina assegnata ({{ eq.code }}) non è più abilitata per questa fase: scegline un’altra.</span>
                </p>
              }
              <div class="grid gap-1.5">
                <span id="wo-machine-label" class="text-sm font-medium">Macchina{{ mode() === 'Assign' ? '' : ' *' }}</span>
                <hlm-select [value]="value()" [itemToString]="itemLabel" (valueChange)="value.set($event ?? null)">
                  <hlm-select-trigger class="w-full" aria-labelledby="wo-machine-label">
                    <hlm-select-value placeholder="Scegli la macchina" />
                  </hlm-select-trigger>
                  <hlm-select-content *hlmSelectPortal>
                    @if (mode() === 'Assign') {
                      <hlm-select-item [value]="NONE">Nessuna macchina</hlm-select-item>
                    }
                    @for (o of candidates(); track o.id) {
                      <hlm-select-item [value]="'' + o.id">{{ label(o) }}{{ usageOf(o.id) ? ' · in uso' : '' }}</hlm-select-item>
                    }
                  </hlm-select-content>
                </hlm-select>
                @if (selectedUsage(); as u) {
                  <p class="text-muted-foreground flex items-start gap-2 text-sm" role="status">
                    <ng-icon name="lucideTriangleAlert" class="mt-0.5" aria-hidden="true" />
                    <span>{{ usageText(u) }}. Puoi procedere comunque.</span>
                  </p>
                }
              </div>
            }

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button #confirmBtn hlmBtn type="submit" [disabled]="loading() || busy() || !canSubmit()">
                {{ busy() ? copy().pending : copy().submit }}
              </button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class WorkOrderMachineDialogComponent implements OnInit {
  private readonly store = inject(WorkOrdersStore);
  private readonly machines = inject(WorkOrderMachinesService);

  readonly work = input.required<WorkOrderModel>();
  readonly mode = input.required<MachineMode>();
  readonly done = output<void>();
  readonly closed = output<void>();

  private readonly confirmBtn = viewChild<ElementRef<HTMLButtonElement>>('confirmBtn');

  protected readonly NONE = NONE;
  protected readonly usageText = usageText;
  protected readonly label = machineLabel;
  protected readonly copy = computed(() => COPY[this.mode()]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly candidates = signal<EquipmentModel[]>([]);
  private readonly usage = signal<MachineUsage>(new Map());
  /** null = nessuna scelta; NONE = (solo assegnazione) rimuovi l'assegnazione; altrimenti l'id della macchina. */
  protected readonly value = signal<string | null>(null);
  private readonly oneClick = signal(false);
  private focused = false;

  protected readonly itemLabel = (v: string): string => {
    if (v === NONE) return 'Nessuna macchina';
    const m = this.candidates().find((c) => String(c.id) === v);
    return m ? machineLabel(m) : '';
  };

  /** Macchina assegnata ma non più utilizzabile (disattivata o scollegata dalla fase). */
  protected readonly staleAssigned = computed(() => {
    const eq = this.work().assignedEquipment;
    return !this.loading() && eq && !this.candidates().some((c) => c.id === eq.id) ? eq : null;
  });

  protected readonly selectedUsage = computed(() => {
    const v = this.value();
    return v && v !== NONE ? (this.usage().get(Number(v)) ?? null) : null;
  });

  protected readonly canSubmit = computed(() => this.value() !== null);

  constructor() {
    // Con macchina già valida e libera il focus va sul pulsante di conferma (Invio conferma).
    effect(() => {
      const btn = this.confirmBtn();
      if (btn && this.oneClick() && !this.focused) {
        this.focused = true;
        btn.nativeElement.focus();
      }
    });
  }

  ngOnInit(): void {
    void this.init();
  }

  protected usageOf(id: number): boolean {
    return !!this.usage().get(id)?.length;
  }

  private async init(): Promise<void> {
    const w = this.work();
    try {
      const [candidates, usage, lastMachineId] = await Promise.all([
        this.machines.candidates(w.processSegmentId),
        this.machines.usage(w.id),
        this.mode() === 'Resume' ? this.lastSessionMachine(w.id) : Promise.resolve(null),
      ]);
      this.candidates.set(candidates);
      this.usage.set(usage);
      const assigned = w.assignedEquipment && w.assignedEquipment.isEnabled ? w.assignedEquipment.id : null;
      const usable = (id: number | null | undefined): id is number => id != null && candidates.some((c) => c.id === id);
      const preferred = [lastMachineId, assigned].find(usable) ?? null;
      if (preferred != null) {
        this.value.set(String(preferred));
        this.oneClick.set(this.mode() !== 'Assign' && !usage.get(preferred)?.length);
      } else if (this.mode() === 'Assign' && !w.assignedEquipment) {
        this.value.set(NONE);
      } else if (candidates.length === 1 && this.mode() !== 'Assign') {
        this.value.set(String(candidates[0].id));
      }
    } catch (e) {
      this.formError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  /** Macchina dell'ultima sessione del lavoro (la ripresa la propone); null se non nota. */
  private async lastSessionMachine(workOrderId: number): Promise<number | null> {
    try {
      const detail = await this.store.getDetail(workOrderId);
      const sessions = [...detail.sessions].sort((a, b) => a.startedAt.localeCompare(b.startedAt));
      return sessions.at(-1)?.equipmentId ?? null;
    } catch {
      return null;
    }
  }

  protected async submit(): Promise<void> {
    const v = this.value();
    if (v === null || this.busy()) return;
    const w = this.work();
    this.busy.set(true);
    this.formError.set(null);
    try {
      if (this.mode() === 'Assign') {
        await this.store.assignEquipment(w.id, v === NONE ? null : Number(v));
      } else if (this.mode() === 'Start') {
        await this.store.start(w.id, { equipmentId: Number(v) });
      } else {
        await this.store.resume(w.id, { equipmentId: Number(v) });
      }
      toast.success(this.copy().done(w.code));
      this.done.emit();
    } catch (e) {
      const f = toFailure(e);
      const detail = Object.values(f.fieldErrors).join(' ');
      this.formError.set(f.status === 422 && detail ? detail : f.message);
    } finally {
      this.busy.set(false);
    }
  }
}
