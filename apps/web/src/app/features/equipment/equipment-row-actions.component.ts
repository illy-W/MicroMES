import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import type { EquipmentModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';

@Component({
  selector: 'app-equipment-row-actions',
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
      [attr.aria-label]="'Azioni per ' + equipment().code"
    >
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          <button hlmDropdownMenuItem (click)="open.emit(equipment())">Apri pagina macchina</button>
          <button hlmDropdownMenuItem (click)="edit.emit(equipment())">Modifica</button>
        </hlm-dropdown-menu-group>
        @if (equipment().isActive) {
          <hlm-dropdown-menu-separator />
          <hlm-dropdown-menu-group>
            <button hlmDropdownMenuItem variant="destructive" (click)="deactivate.emit(equipment())">Disattiva</button>
          </hlm-dropdown-menu-group>
        }
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class EquipmentRowActionsComponent {
  readonly equipment = input.required<EquipmentModel>();
  readonly open = output<EquipmentModel>();
  readonly edit = output<EquipmentModel>();
  readonly deactivate = output<EquipmentModel>();
}
