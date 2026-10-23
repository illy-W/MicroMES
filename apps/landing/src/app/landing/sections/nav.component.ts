import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideGithub, lucideMenu } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSheetImports } from '@spartan-ng/helm/sheet';
import { LANDING_LINKS } from '../../landing-links';

@Component({
  selector: 'app-nav',
  imports: [HlmButton, NgIcon, HlmSheetImports],
  providers: [provideIcons({ lucideGithub, lucideMenu })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'bg-background/95 border-border sticky top-0 z-40 block border-b backdrop-blur' },
  template: `
    <div class="mx-auto flex h-14 w-full max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
      <a href="#top" class="flex items-center gap-2 font-bold tracking-tight max-sm:min-h-11" aria-label="MicroMES, inizio pagina">
        <span class="bg-primary text-primary-foreground grid size-7 place-items-center rounded-md font-mono text-sm" aria-hidden="true">M</span>
        MicroMES
      </a>
      <nav aria-label="Sezioni della pagina" class="hidden items-center gap-5 text-sm font-medium md:flex">
        @for (link of links; track link.href) {
          <a [href]="link.href" class="text-muted-foreground hover:text-foreground inline-flex min-h-8 items-center">{{ link.label }}</a>
        }
      </nav>
      <div class="flex items-center gap-2">
        <a hlmBtn variant="ghost" size="sm" class="focus-visible:outline-ring max-sm:min-h-11 max-sm:min-w-11" [href]="repo" target="_blank" rel="noopener" aria-label="Repository su GitHub (si apre in una nuova scheda)">
          <ng-icon name="lucideGithub" aria-hidden="true" /> <span class="hidden sm:inline">GitHub</span>
        </a>
        <a hlmBtn size="sm" class="max-sm:min-h-11" href="#installa">Provalo</a>
        <hlm-sheet #menu="hlmSheet" side="right">
          <button hlmSheetTrigger hlmBtn variant="outline" size="sm" class="min-h-11 min-w-11 md:hidden" aria-label="Apri il menu delle sezioni">
            <ng-icon name="lucideMenu" aria-hidden="true" />
          </button>
          <hlm-sheet-content *hlmSheetPortal="let ctx" class="w-72">
            <hlm-sheet-header>
              <h2 hlmSheetTitle>Sezioni</h2>
              <p hlmSheetDescription class="sr-only">Vai a una sezione della pagina</p>
            </hlm-sheet-header>
            <nav aria-label="Sezioni della pagina (menu)" class="grid px-4 pb-4">
              @for (link of links; track link.href) {
                <a [href]="link.href" class="border-border flex min-h-11 items-center border-t text-base font-medium" (click)="menu.close({})">{{ link.label }}</a>
              }
            </nav>
          </hlm-sheet-content>
        </hlm-sheet>
      </div>
    </div>
  `,
})
export class NavComponent {
  protected readonly repo = LANDING_LINKS.repo;
  protected readonly links = [
    { href: '#funzioni', label: 'Funzioni' },
    { href: '#come-funziona', label: 'Come funziona' },
    { href: '#gratis', label: 'Gratis' },
    { href: '#installa', label: 'Installazione' },
    { href: '#faq', label: 'Domande' },
  ];
}
