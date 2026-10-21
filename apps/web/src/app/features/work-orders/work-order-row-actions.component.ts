import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import type { WorkOrderModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';

/** Menu azioni di riga: le voci derivano solo da allowedActions del lavoro. */
@Component({
  selector: 'app-work-order-row-actions',
  imports: [HlmButton, HlmDropdownMenuImports, NgIcon],
  providers: [provideIcons({ lucideEllipsis })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      hlmBtn
      variant="ghost"
      size="icon-sm"
      align="end"
      [hlmDropdownMenuTrigger]="menu"
      [attr.aria-label]="'Azioni per ' + work().code"
    >
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          @if (can('Start')) {
            <button hlmDropdownMenuItem (click)="startWork.emit(work())">Avvia</button>
          }
          @if (can('Resume')) {
            <button hlmDropdownMenuItem (click)="resumeWork.emit(work())">Riprendi</button>
          }
          @if (can('Pause')) {
            <button hlmDropdownMenuItem (click)="pauseWork.emit(work())">Metti in pausa</button>
          }
          @if (can('Stop')) {
            <button hlmDropdownMenuItem (click)="stopWork.emit(work())">Chiudi lavoro</button>
          }
          <button hlmDropdownMenuItem (click)="sessions.emit(work())">Registrazioni</button>
          <button hlmDropdownMenuItem (click)="openOrder()">Apri ordine</button>
        </hlm-dropdown-menu-group>
        @if (can('Abort')) {
          <hlm-dropdown-menu-separator />
          <hlm-dropdown-menu-group>
            <button hlmDropdownMenuItem variant="destructive" (click)="abortWork.emit(work())">Interrompi</button>
          </hlm-dropdown-menu-group>
        }
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class WorkOrderRowActionsComponent {
  private readonly router = inject(Router);
  readonly work = input.required<WorkOrderModel>();
  readonly startWork = output<WorkOrderModel>();
  readonly resumeWork = output<WorkOrderModel>();
  readonly pauseWork = output<WorkOrderModel>();
  readonly stopWork = output<WorkOrderModel>();
  readonly abortWork = output<WorkOrderModel>();
  readonly sessions = output<WorkOrderModel>();

  protected can(action: string): boolean {
    return this.work().allowedActions.includes(action);
  }

  protected openOrder(): void {
    void this.router.navigate(['/production-orders', this.work().productionOrderId]);
  }
}
