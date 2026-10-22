import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import type { ToolingModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';

@Component({
  selector: 'app-tooling-row-actions',
  imports: [HlmButton, HlmDropdownMenuImports, NgIcon],
  providers: [provideIcons({ lucideEllipsis })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button hlmBtn variant="ghost" size="icon-sm" align="end" [hlmDropdownMenuTrigger]="menu" [attr.aria-label]="'Azioni per ' + tooling().code">
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          <button hlmDropdownMenuItem (click)="open.emit(tooling())">Dettaglio</button>
          <button hlmDropdownMenuItem (click)="edit.emit(tooling())">Modifica</button>
        </hlm-dropdown-menu-group>
        <hlm-dropdown-menu-separator />
        <hlm-dropdown-menu-group>
          @if (tooling().isActive) {
            <button hlmDropdownMenuItem variant="destructive" (click)="deactivate.emit(tooling())">Disattiva</button>
          } @else {
            <button hlmDropdownMenuItem (click)="reactivate.emit(tooling())">Riattiva</button>
          }
        </hlm-dropdown-menu-group>
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class ToolingRowActionsComponent {
  readonly tooling = input.required<ToolingModel>();
  readonly open = output<ToolingModel>();
  readonly edit = output<ToolingModel>();
  readonly deactivate = output<ToolingModel>();
  readonly reactivate = output<ToolingModel>();
}
