import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import type { SetupAttributeModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';

@Component({
  selector: 'app-setup-attribute-row-actions',
  imports: [HlmButton, HlmDropdownMenuImports, NgIcon],
  providers: [provideIcons({ lucideEllipsis })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button hlmBtn variant="ghost" size="icon-sm" align="end" [hlmDropdownMenuTrigger]="menu" [attr.aria-label]="'Azioni per ' + attribute().code">
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          <button hlmDropdownMenuItem (click)="edit.emit(attribute())">Modifica</button>
        </hlm-dropdown-menu-group>
        <hlm-dropdown-menu-separator />
        <hlm-dropdown-menu-group>
          @if (attribute().isActive) {
            <button hlmDropdownMenuItem variant="destructive" (click)="deactivate.emit(attribute())">Disattiva</button>
          } @else {
            <button hlmDropdownMenuItem (click)="reactivate.emit(attribute())">Riattiva</button>
          }
        </hlm-dropdown-menu-group>
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class SetupAttributeRowActionsComponent {
  readonly attribute = input.required<SetupAttributeModel>();
  readonly edit = output<SetupAttributeModel>();
  readonly deactivate = output<SetupAttributeModel>();
  readonly reactivate = output<SetupAttributeModel>();
}
