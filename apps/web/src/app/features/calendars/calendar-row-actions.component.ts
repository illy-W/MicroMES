import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import type { WorkCalendarModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';

@Component({
  selector: 'app-calendar-row-actions',
  imports: [HlmButton, HlmDropdownMenuImports, NgIcon],
  providers: [provideIcons({ lucideEllipsis })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button hlmBtn variant="ghost" size="icon-sm" align="end" [hlmDropdownMenuTrigger]="menu" [attr.aria-label]="'Azioni per ' + calendar().code">
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          <button hlmDropdownMenuItem (click)="open.emit(calendar())">Apri</button>
          <button hlmDropdownMenuItem (click)="edit.emit(calendar())">Modifica</button>
          <button hlmDropdownMenuItem (click)="duplicate.emit(calendar())">Duplica</button>
          @if (calendar().isActive && !calendar().isDefault) {
            <button hlmDropdownMenuItem (click)="setDefault.emit(calendar())">Imposta come predefinito</button>
          }
        </hlm-dropdown-menu-group>
        @if (calendar().isActive && !calendar().isDefault) {
          <hlm-dropdown-menu-separator />
          <hlm-dropdown-menu-group>
            <button hlmDropdownMenuItem variant="destructive" (click)="deactivate.emit(calendar())">Disattiva</button>
          </hlm-dropdown-menu-group>
        }
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class CalendarRowActionsComponent {
  readonly calendar = input.required<WorkCalendarModel>();
  readonly open = output<WorkCalendarModel>();
  readonly edit = output<WorkCalendarModel>();
  readonly duplicate = output<WorkCalendarModel>();
  readonly setDefault = output<WorkCalendarModel>();
  readonly deactivate = output<WorkCalendarModel>();
}
