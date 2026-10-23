import { ChangeDetectionStrategy, Component } from '@angular/core';
import { injectBrnDialogContext } from '@spartan-ng/brain/dialog';
// Questo file è esso stesso caricato in modo differito insieme al dialog: l'import statico qui è voluto.
// eslint-disable-next-line @nx/enforce-module-boundaries
import { HlmDialogDescription, HlmDialogHeader, HlmDialogTitle } from '@spartan-ng/helm/dialog';

export interface ProductLightboxContext {
  src: string;
  alt: string;
  title: string;
  caption: string;
  width: number;
  height: number;
}

/** Contenuto del dialog "Ingrandisci": immagine a 1600 px, scorrevole se più grande della finestra. Caricato in modo differito. */
@Component({
  selector: 'app-product-lightbox',
  imports: [HlmDialogHeader, HlmDialogTitle, HlmDialogDescription],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid min-w-0 gap-4' },
  template: `
    <hlm-dialog-header class="pe-8">
      <h2 hlmDialogTitle>{{ ctx.title }}</h2>
      @if (ctx.caption) {
        <p hlmDialogDescription>{{ ctx.caption }}</p>
      }
    </hlm-dialog-header>
    <div class="border-border overflow-auto rounded-md border" tabindex="0" role="group" aria-label="Immagine ingrandita, scorrevole">
      <img [src]="ctx.src" [width]="ctx.width" [height]="ctx.height" [alt]="ctx.alt" class="bg-card block h-auto w-full min-w-[720px] sm:min-w-0" />
    </div>
  `,
})
export class ProductLightboxComponent {
  protected readonly ctx = injectBrnDialogContext<ProductLightboxContext>();
}
