import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideEllipsis } from '@ng-icons/lucide';
import { MaterialType, type MaterialModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';

/** Menu azioni di riga (Modifica / Disattiva), come ActionDropdown dell'esempio dashboard Spartan. */
@Component({
  selector: 'app-material-row-actions',
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
      [attr.aria-label]="'Azioni per ' + material().code"
    >
      <ng-icon name="lucideEllipsis" />
    </button>
    <ng-template #menu>
      <hlm-dropdown-menu>
        <hlm-dropdown-menu-group>
          @if (hasBom()) {
            <button hlmDropdownMenuItem (click)="openBom()">Distinta base</button>
            <button hlmDropdownMenuItem (click)="openRouting()">Ciclo di lavoro</button>
          }
          <button hlmDropdownMenuItem (click)="edit.emit(material())">Modifica</button>
        </hlm-dropdown-menu-group>
        @if (material().isActive) {
          <hlm-dropdown-menu-separator />
          <hlm-dropdown-menu-group>
            <button hlmDropdownMenuItem variant="destructive" (click)="deactivate.emit(material())">Disattiva</button>
          </hlm-dropdown-menu-group>
        }
      </hlm-dropdown-menu>
    </ng-template>
  `,
})
export class MaterialRowActionsComponent {
  private readonly router = inject(Router);
  readonly material = input.required<MaterialModel>();
  protected readonly hasBom = computed(
    () => this.material().type === MaterialType.Intermediate || this.material().type === MaterialType.FinishedGood,
  );

  protected openBom(): void {
    void this.router.navigate(['/materials', this.material().id, 'bom']);
  }

  protected openRouting(): void {
    void this.router.navigate(['/materials', this.material().id, 'routing']);
  }

  readonly edit = output<MaterialModel>();
  readonly deactivate = output<MaterialModel>();
}
