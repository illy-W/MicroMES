import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import type { ScrapReasonModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';

@Component({
  selector: 'app-scrap-reason-row-actions',
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
      [attr.aria-label]="'Azioni per ' + reason().code"
    >
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          <button hlmDropdownMenuItem (click)="edit.emit(reason())">Modifica</button>
        </hlm-dropdown-menu-group>
        @if (reason().isActive) {
          <hlm-dropdown-menu-separator />
          <hlm-dropdown-menu-group>
            <button hlmDropdownMenuItem variant="destructive" (click)="deactivate.emit(reason())">Disattiva</button>
          </hlm-dropdown-menu-group>
        }
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class ScrapReasonRowActionsComponent {
  readonly reason = input.required<ScrapReasonModel>();
  readonly edit = output<ScrapReasonModel>();
  readonly deactivate = output<ScrapReasonModel>();
}
