import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideLayoutDashboard } from '@ng-icons/lucide';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';

@Component({
  selector: 'app-dashboard',
  imports: [HlmEmptyImports, NgIcon],
  providers: [provideIcons({ lucideLayoutDashboard })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-empty>
      <hlm-empty-header>
        <hlm-empty-media variant="icon">
          <ng-icon name="lucideLayoutDashboard" />
        </hlm-empty-media>
        <div hlmEmptyTitle>Dashboard</div>
        <div hlmEmptyDescription>Nessun contenuto al momento</div>
      </hlm-empty-header>
    </hlm-empty>
  `,
})
export class DashboardComponent {}
