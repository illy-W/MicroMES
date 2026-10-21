import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideChartNoAxesColumn,
  lucideClipboardList,
  lucideHouse,
  lucideListChecks,
  lucidePackage,
  lucideTags,
  lucideWorkflow,
} from '@ng-icons/lucide';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { NavSection } from '../../models/nav-item.model';

@Component({
  selector: 'app-nav-main',
  imports: [HlmSidebarImports, NgIcon, RouterLink, RouterLinkActive],
  providers: [
    provideIcons({
      lucideChartNoAxesColumn,
      lucideClipboardList,
      lucideHouse,
      lucideListChecks,
      lucidePackage,
      lucideTags,
      lucideWorkflow,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (section of sections(); track section.id) {
      <hlm-sidebar-group>
        @if (section.label) {
          <div hlmSidebarGroupLabel>{{ section.label }}</div>
        }
        <ul hlmSidebarMenu [attr.aria-label]="section.label ?? null">
          @for (item of section.items; track item.id) {
            <li hlmSidebarMenuItem>
              <a
                hlmSidebarMenuButton
                [routerLink]="item.route"
                routerLinkActive
                #rla="routerLinkActive"
                [isActive]="rla.isActive"
                [attr.aria-current]="rla.isActive ? 'page' : null"
                [attr.aria-label]="item.displayName"
              >
                <ng-icon [name]="item.iconName" />
                {{ item.displayName }}
              </a>
            </li>
          }
        </ul>
      </hlm-sidebar-group>
    }
  `,
})
export class NavMainComponent {
  public readonly sections = input.required<NavSection[]>();
}
