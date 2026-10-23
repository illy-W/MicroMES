import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { NavSection } from '../../models/nav-item.model';
import { VersionInfoComponent } from '../version-info/version-info.component';
import { NavMainComponent } from '../nav-main/nav-main.component';

@Component({
  selector: 'app-sidebar',
  imports: [HlmSidebarImports, NavMainComponent, VersionInfoComponent],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div hlmSidebarWrapper>
      <hlm-sidebar variant="inset">
        <hlm-sidebar-header>
          <span class="px-2 py-1 text-sm font-medium">MicroMES</span>
        </hlm-sidebar-header>
        <hlm-sidebar-content>
          <app-nav-main [sections]="sections()" />
        </hlm-sidebar-content>
        <hlm-sidebar-footer>
          <app-version-info />
        </hlm-sidebar-footer>
      </hlm-sidebar>
      <ng-content />
    </div>
  `,
})
export class SidebarComponent {
  public readonly sections = input.required<NavSection[]>();
}
