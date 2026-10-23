import { IMAGE_LOADER, NgOptimizedImage, type ImageLoaderConfig } from '@angular/common';
import { ChangeDetectionStrategy, Component, Injector, inject, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMaximize2 } from '@ng-icons/lucide';
import type { ProductLightboxContext } from './product-lightbox.component';

/**
 * Screenshot del prodotto: file `<nome>-960.webp` e `<nome>-1600.webp` in `assets/images/product`.
 * Un click (o Invio/Spazio) apre la versione a 1600 px in un dialog spartan. Il codice del dialog e l'immagine grande
 * vengono caricati solo al primo uso (import dinamico), non nel percorso iniziale.
 */
@Component({
  selector: 'app-product-image',
  imports: [NgOptimizedImage, NgIcon],
  providers: [
    provideIcons({ lucideMaximize2 }),
    {
      provide: IMAGE_LOADER,
      useValue: (config: ImageLoaderConfig) => `/assets/images/product/${config.src}-${config.width ?? 1600}.webp`,
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
  `,
  template: `
    <button
      type="button"
      class="group relative block w-full cursor-zoom-in"
      aria-haspopup="dialog"
      (click)="open()"
      (pointerenter)="preload()"
      (focus)="preload()"
    >
      <picture>
        @if (narrowName(); as narrow) {
          <source media="(max-width: 639px)" [attr.srcset]="'/assets/images/product/' + narrow + '.webp'" />
        }
        <img
          [ngSrc]="name()"
          ngSrcset="960w, 1600w"
          [sizes]="sizes()"
          [width]="width()"
          [height]="height()"
          [alt]="alt()"
          [priority]="priority() && !narrowName()"
          [loading]="priority() ? 'eager' : 'lazy'"
          [attr.fetchpriority]="priority() ? 'high' : null"
          class="bg-card block h-auto w-full"
          [class]="imgClass()"
        />
      </picture>
      <span class="bg-primary text-primary-foreground absolute end-2 bottom-2 inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium shadow-sm group-hover:underline">
        <ng-icon name="lucideMaximize2" size="14" aria-hidden="true" /> Ingrandisci
      </span>
    </button>
  `,
})
export class ProductImageComponent {
  private readonly injector = inject(Injector);
  private lightbox?: Promise<readonly [typeof import('@spartan-ng/helm/dialog'), typeof import('./product-lightbox.component')]>;

  readonly name = input.required<string>();
  readonly alt = input.required<string>();
  readonly width = input.required<number>();
  readonly height = input.required<number>();
  readonly priority = input(false);
  readonly sizes = input('(min-width: 1152px) 1100px, 100vw');
  /** Didascalia mostrata nel dialog (se assente, solo il testo alternativo). */
  readonly caption = input('');
  /** Titolo del dialog. */
  readonly title = input('Schermata di MicroMES');
  /** Variante stretta (<640 px): nome del file senza estensione, es. `overview-narrow`. */
  readonly narrowName = input<string>();
  readonly imgClass = input('');

  protected preload() {
    return (this.lightbox ??= Promise.all([import('@spartan-ng/helm/dialog'), import('./product-lightbox.component')]));
  }

  protected async open(): Promise<void> {
    const [{ HlmDialogService }, { ProductLightboxComponent }] = await this.preload();
    const context: ProductLightboxContext = {
      src: `/assets/images/product/${this.name()}-1600.webp`,
      alt: this.alt(),
      title: this.title(),
      caption: this.caption(),
      width: 1600,
      height: this.height(),
    };
    this.injector.get(HlmDialogService).open(ProductLightboxComponent, {
      context,
      closeLabel: 'Chiudi',
      contentClass: 'sm:max-w-[min(1700px,calc(100vw-2rem))] max-h-[calc(100dvh-2rem)] overflow-y-auto',
      restoreFocus: true,
    });
  }
}
