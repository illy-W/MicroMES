import { ChangeDetectionStrategy, Component, DestroyRef, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { VersionService } from '@micromes/api-client/services/version';
import type { VersionModel } from '@micromes/api-client/models';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideCopy } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmPopoverImports } from '@spartan-ng/helm/popover';
import { HlmTooltip } from '@spartan-ng/helm/tooltip';

const RELEASES_URL = 'https://github.com/illy-W/MicroMES/releases';
const UPGRADE_COMMAND = 'micromes upgrade';
const DISMISS_KEY = 'micromes.update-dismissed';

@Component({
  selector: 'app-version-info',
  imports: [HlmButton, HlmPopoverImports, HlmTooltip, NgIcon],
  providers: [provideIcons({ lucideCheck, lucideCopy })],
  host: { class: 'flex min-h-8 flex-col justify-center gap-1 px-2 text-xs' },
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (info(); as v) {
      <span class="text-muted-foreground w-fit" tabindex="0" [hlmTooltip]="details()" [attr.aria-label]="'Versione installata ' + label() + '. ' + details()">{{ label() }}</span>
      @if (showUpdate()) {
        <div class="flex items-center gap-1">
          <hlm-popover align="start">
            <button hlmBtn hlmPopoverTrigger type="button" variant="link" size="sm" class="h-auto p-0 text-xs">Disponibile v{{ v.latestVersion }}</button>
            <hlm-popover-content *hlmPopoverPortal="let ctx" class="w-72 gap-3" role="dialog" aria-label="Aggiornamento disponibile">
              <p class="text-sm font-medium">Nuova versione v{{ v.latestVersion }}</p>
              <a class="text-primary w-fit text-sm underline underline-offset-4" [href]="releaseUrl()" target="_blank" rel="noopener noreferrer">Note di rilascio</a>
              <div class="flex flex-col gap-1">
                <span class="text-muted-foreground text-xs">Per aggiornare esegui sul server:</span>
                <div class="bg-muted flex items-center justify-between gap-2 rounded-md px-2 py-1">
                  <code class="text-xs">{{ command }}</code>
                  <button hlmBtn type="button" size="icon-sm" variant="ghost" aria-label="Copia comando" hlmTooltip="Copia comando" (click)="copy()">
                    <ng-icon [name]="copied() ? 'lucideCheck' : 'lucideCopy'" aria-hidden="true" />
                  </button>
                </div>
              </div>
            </hlm-popover-content>
          </hlm-popover>
          <button hlmBtn type="button" variant="ghost" size="sm" class="text-muted-foreground ml-auto h-auto px-1 py-0 text-xs" aria-label="Nascondi avviso di aggiornamento" (click)="dismiss()">Nascondi</button>
        </div>
      }
    }
  `,
})
export class VersionInfoComponent {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  protected readonly command = UPGRADE_COMMAND;
  protected readonly info = signal<VersionModel | null>(null);
  protected readonly dismissed = signal<string | null>(this.readDismissed());
  protected readonly copied = signal(false);

  protected readonly label = computed(() => {
    const v = this.info()?.version;
    return !v || v.startsWith('0.0.0-dev') ? 'dev' : `v${v}`;
  });
  protected readonly details = computed(() => {
    const v = this.info();
    const parts = [v?.commit ? `Commit ${v.commit.slice(0, 7)}` : null, v?.buildDate ? `Build ${v.buildDate}` : null].filter(Boolean);
    return parts.length ? parts.join(' · ') : 'Dettagli build non disponibili';
  });
  protected readonly showUpdate = computed(() => {
    const v = this.info();
    return !!v?.updateAvailable && !!v.latestVersion && this.dismissed() !== v.latestVersion;
  });
  protected readonly releaseUrl = computed(() => `${RELEASES_URL}/tag/app-v${this.info()?.latestVersion}`);

  constructor() {
    if (!this.browser) return;
    inject(VersionService)
      .getVersion()
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe({ next: (v) => this.info.set(v), error: () => undefined });
  }

  protected dismiss(): void {
    const latest = this.info()?.latestVersion;
    if (!latest) return;
    this.dismissed.set(latest);
    try {
      localStorage.setItem(DISMISS_KEY, latest);
    } catch {
      /* storage non disponibile */
    }
  }

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(UPGRADE_COMMAND);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      /* clipboard non disponibile */
    }
  }

  private readDismissed(): string | null {
    if (!this.browser) return null;
    try {
      return localStorage.getItem(DISMISS_KEY);
    } catch {
      return null;
    }
  }
}
