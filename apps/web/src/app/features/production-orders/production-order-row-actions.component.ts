import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import type { ProductionOrderModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { ACTIONS, canEditOrder } from './production-order-labels';

/** Menu azioni di riga: le voci derivano da allowedActions (+ "Modifica" per gli ordini Planned non figli). */
@Component({
  selector: 'app-production-order-row-actions',
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
      [attr.aria-label]="'Azioni per ' + order().code"
    >
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          <button hlmDropdownMenuItem (click)="open()">Apri</button>
          @if (canEdit()) {
            <button hlmDropdownMenuItem (click)="edit.emit(order())">Modifica</button>
          }
          @if (can(actions.release)) {
            <button hlmDropdownMenuItem (click)="release.emit(order())">Rilascia</button>
          }
        </hlm-dropdown-menu-group>
        @if (can(actions.cancel) || can(actions.abort)) {
          <hlm-dropdown-menu-separator />
          <hlm-dropdown-menu-group>
            @if (can(actions.cancel)) {
              <button hlmDropdownMenuItem variant="destructive" (click)="cancelOrder.emit(order())">Annulla</button>
            }
            @if (can(actions.abort)) {
              <button hlmDropdownMenuItem variant="destructive" (click)="abortOrder.emit(order())">Interrompi</button>
            }
          </hlm-dropdown-menu-group>
        }
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class ProductionOrderRowActionsComponent {
  private readonly router = inject(Router);
  readonly order = input.required<ProductionOrderModel>();
  readonly edit = output<ProductionOrderModel>();
  readonly release = output<ProductionOrderModel>();
  readonly cancelOrder = output<ProductionOrderModel>();
  readonly abortOrder = output<ProductionOrderModel>();

  protected readonly actions = ACTIONS;
  protected readonly canEdit = computed(() => canEditOrder(this.order()));

  protected can(action: string): boolean {
    return this.order().allowedActions.includes(action);
  }

  protected open(): void {
    void this.router.navigate(['/production-orders', this.order().id]);
  }
}
