import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { HeaderComponent } from '../components/header/header.component';
import { SidebarComponent } from '../components/sidebar/sidebar.component';
import { sidebarSections } from './sidebar-data';

@Component({
  selector: 'app-full-layout',
  imports: [RouterOutlet, HlmSidebarImports, SidebarComponent, HeaderComponent],
  templateUrl: './full.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FullLayoutComponent {
  protected readonly sections = sidebarSections;
}
