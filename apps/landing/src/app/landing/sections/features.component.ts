import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideBoxes,
  lucideCalendarRange,
  lucideCamera,
  lucideCog,
  lucideListChecks,
  lucideNetwork,
  lucideRoute,
  lucideTrash2,
} from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';

@Component({
  selector: 'app-features',
  imports: [NgIcon, HlmBadge],
  providers: [
    provideIcons({ lucideBoxes, lucideCalendarRange, lucideCamera, lucideCog, lucideListChecks, lucideNetwork, lucideRoute, lucideTrash2 }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <section id="funzioni" aria-labelledby="funzioni-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-24">
      <p class="tag">Cosa fa</p>
      <h2 id="funzioni-titolo" class="mt-3 max-w-3xl">Quello che serve in officina, niente di più.</h2>
      <p class="text-muted-foreground mt-4 max-w-2xl">
        Ogni funzione ha un valore di default sensato: il caso comune non richiede configurazione.
      </p>
      <ul class="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        @for (f of features; track f.title) {
          <li class="bg-card border-border flex flex-col gap-3 rounded-lg border p-5">
            <span class="bg-accent text-accent-foreground grid size-9 place-items-center rounded-md">
              <ng-icon [name]="f.icon" size="20" aria-hidden="true" />
            </span>
            <h3>{{ f.title }}</h3>
            <p class="text-muted-foreground text-sm leading-6">{{ f.text }}</p>
          </li>
        }
      </ul>

      <div class="border-border mt-10 grid gap-6 rounded-lg border-2 border-dashed p-5 sm:grid-cols-2">
        <div>
          <h3 class="flex items-center gap-2">In arrivo <span hlmBadge variant="secondary">non ancora disponibile</span></h3>
          <ul class="text-muted-foreground mt-3 list-disc space-y-1 ps-5 text-sm">
            <li>Vista operatore (avvio, pausa e chiusura dal banco)</li>
            <li>Autenticazione e permessi per ruolo</li>
          </ul>
        </div>
        <div>
          <h3>Non incluso, per scelta</h3>
          <p class="text-muted-foreground mt-3 text-sm">
            Contabilità e costi, magazzino e giacenze, acquisti, collegamento diretto alle macchine. MicroMES resta piccolo
            e fa bene il suo mestiere.
          </p>
        </div>
      </div>
    </section>
  `,
})
export class FeaturesComponent {
  protected readonly features = [
    { icon: 'lucideBoxes', title: 'Anagrafiche e distinta base', text: 'Materiali, semilavorati e prodotti finiti con distinta base su più livelli.' },
    { icon: 'lucideRoute', title: 'Cicli a grafo', text: 'Le fasi di lavorazione e le loro dipendenze come grafo. Il percorso più lungo è calcolato per te.' },
    { icon: 'lucideListChecks', title: 'Ordini e rilascio', text: 'Il rilascio fa un’istantanea di ciclo e distinta: cambiare l’anagrafica dopo non altera gli ordini già lanciati.' },
    { icon: 'lucideNetwork', title: 'Ordini di lavoro con stato', text: 'In attesa, Pronto, In esecuzione, In pausa, Terminato. Transizioni non valide impedite, con indicazione di cosa fare.' },
    { icon: 'lucideCamera', title: 'Panoramica grafica', text: 'L’ordine come diagramma: stato di ogni lavoro, ordini figli raggruppati, percorso più lungo evidenziato.' },
    { icon: 'lucideCalendarRange', title: 'Pianificazione a capacità finita', text: 'Timeline per macchina che rispetta calendari, ferie ed eccezioni, con segnalazione dei ritardi.' },
    { icon: 'lucideCog', title: 'Macchine, attrezzature, attrezzaggi', text: 'Postazioni, attrezzature e attributi di cambio: i tempi di attrezzaggio entrano nel piano.' },
    { icon: 'lucideTrash2', title: 'Scarti con motivo e report', text: 'Ogni scarto ha il suo motivo. Vedi dove e perché perdi materiale.' },
  ];
}
