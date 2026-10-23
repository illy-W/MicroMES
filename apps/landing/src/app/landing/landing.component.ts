import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { LANDING_LINKS } from '../landing-links';
import { CtaComponent } from './sections/cta.component';
import { DiagramSectionComponent } from './sections/diagram-section.component';
import { FaqComponent } from './sections/faq.component';
import { FeaturesComponent } from './sections/features.component';
import { FooterComponent } from './sections/footer.component';
import { FreeComponent } from './sections/free.component';
import { HowItWorksComponent } from './sections/how-it-works.component';
import { HeroComponent } from './sections/hero.component';
import { InstallComponent } from './sections/install.component';
import { NavComponent } from './sections/nav.component';
import { ProblemsComponent } from './sections/problems.component';
import { StandardComponent } from './sections/standard.component';
import { SupportComponent } from './sections/support.component';

const TITLE = 'MicroMES · il MES gratuito e open source per le piccole officine';
const DESCRIPTION =
  'MicroMES è un MES per PMI e micro imprese manifatturiere (1-20 persone): ordini, cicli, pianificazione e scarti. Gratuito, open source AGPL-3.0, self-hosted. Provalo in locale con i dati di esempio.';

@Component({
  selector: 'app-landing',
  imports: [
    NavComponent,
    HeroComponent,
    ProblemsComponent,
    FeaturesComponent,
    HowItWorksComponent,
    DiagramSectionComponent,
    FreeComponent,
    InstallComponent,
    StandardComponent,
    SupportComponent,
    FaqComponent,
    CtaComponent,
    FooterComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a href="#contenuto" class="bg-primary text-primary-foreground sr-only z-50 rounded-md px-3 py-2 focus:not-sr-only focus:fixed focus:start-2 focus:top-2">Vai al contenuto</a>
    <app-nav />
    <main id="contenuto" tabindex="-1" class="outline-none">
      <div id="top"></div>
      <app-hero />
      <app-problems />
      <app-features />
      <app-how-it-works />
      <app-diagram-section />
      <app-free />
      <app-install />
      <app-standard />
      <app-support />
      <app-faq />
      <app-cta />
    </main>
    <app-footer />
  `,
})
export class LandingComponent {
  constructor() {
    inject(Title).setTitle(TITLE);
    const meta = inject(Meta);
    const og = `${LANDING_LINKS.site}/og-image.jpg`;
    meta.updateTag({ name: 'description', content: DESCRIPTION });
    meta.updateTag({ property: 'og:type', content: 'website' });
    meta.updateTag({ property: 'og:locale', content: 'it_IT' });
    meta.updateTag({ property: 'og:title', content: TITLE });
    meta.updateTag({ property: 'og:description', content: DESCRIPTION });
    meta.updateTag({ property: 'og:url', content: LANDING_LINKS.site });
    meta.updateTag({ property: 'og:image', content: og });
    meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    meta.updateTag({ name: 'twitter:image', content: og });
  }
}
