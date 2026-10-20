import { ChangeDetectionStrategy, Component } from '@angular/core';
import { HlmSeparatorImports } from '@spartan-ng/helm/separator';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';

@Component({
  selector: 'app-header',
  imports: [HlmSidebarImports, HlmSeparatorImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="flex h-16 shrink-0 items-center gap-2">
      <div class="flex items-center gap-2 px-4">
        <button hlmSidebarTrigger aria-label="Mostra/nascondi menu"></button>
        <hlm-separator class="mr-2 data-[orientation=vertical]:h-4" orientation="vertical" />
      </div>
    </header>
  `,
})
export class HeaderComponent {}
