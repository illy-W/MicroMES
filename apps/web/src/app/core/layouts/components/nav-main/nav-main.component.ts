import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideHouse } from '@ng-icons/lucide';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { NavItem } from '../../models/nav-item.model';

@Component({
  selector: 'app-nav-main',
  imports: [HlmSidebarImports, NgIcon, RouterLink, RouterLinkActive],
  providers: [provideIcons({ lucideHouse })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-sidebar-group>
      <ul hlmSidebarMenu>
        @for (item of items(); track item.id) {
          <li hlmSidebarMenuItem>
            <a
              hlmSidebarMenuButton
              [routerLink]="item.route"
              routerLinkActive
              #rla="routerLinkActive"
              [isActive]="rla.isActive"
            >
              <ng-icon [name]="item.iconName" />
              {{ item.displayName }}
            </a>
          </li>
        }
      </ul>
    </hlm-sidebar-group>
  `,
})
export class NavMainComponent {
  public readonly items = input.required<NavItem[]>();
}
