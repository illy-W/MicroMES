import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { NavItem } from '../../models/nav-item.model';
import { NavMainComponent } from '../nav-main/nav-main.component';

@Component({
  selector: 'app-sidebar',
  imports: [HlmSidebarImports, NavMainComponent],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div hlmSidebarWrapper>
      <hlm-sidebar variant="inset">
        <hlm-sidebar-header>
          <span class="px-2 py-1 text-sm font-medium">MicroMES</span>
        </hlm-sidebar-header>
        <hlm-sidebar-content>
          <app-nav-main [items]="items()" />
        </hlm-sidebar-content>
      </hlm-sidebar>
      <ng-content />
    </div>
  `,
})
export class SidebarComponent {
  public readonly items = input.required<NavItem[]>();
}
