import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ProductImageComponent } from '../product-image.component';

@Component({
  selector: 'app-how-it-works',
  imports: [ProductImageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <section id="come-funziona" aria-labelledby="come-funziona-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-24">
      <p class="tag">Come funziona</p>
      <h2 id="come-funziona-titolo" class="mt-3 max-w-3xl">Guarda come funziona.</h2>
      <p class="text-muted-foreground mt-4 max-w-2xl">Dall’ordine al lavoro finito in quattro passi, con schermate reali dell’applicazione e i dati di esempio di un’officina di mobili in acciaio e rovere. Clicca un’immagine per ingrandirla.</p>
      <ol class="mt-10 grid gap-10 md:grid-cols-2">
        @for (s of steps; track s.n) {
          <li>
            <figure>
              <div class="border-border bg-card overflow-hidden rounded-lg border-2 shadow-sm">
                <app-product-image [name]="s.name" [alt]="s.alt" [title]="s.title" [caption]="s.caption" [width]="1600" [height]="s.height" sizes="(min-width: 1152px) 540px, (min-width: 768px) 45vw, 100vw" />
              </div>
              <figcaption class="mt-3 flex gap-3">
                <span class="bg-signal text-signal-foreground grid size-8 shrink-0 place-items-center rounded-md font-mono text-sm font-bold" aria-hidden="true">{{ s.n }}</span>
                <span>
                  <span class="block font-semibold"><span class="sr-only">Passo {{ s.n }}: </span>{{ s.title }}</span>
                  <span class="text-muted-foreground block text-sm">{{ s.caption }}</span>
                </span>
              </figcaption>
            </figure>
          </li>
        }
      </ol>
    </section>
  `,
})
export class HowItWorksComponent {
  protected readonly steps = [
    {
      n: 1,
      name: 'bom',
      height: 680,
      title: 'Crea l’ordine',
      caption: 'Scegli cosa produrre: la distinta base elenca i componenti della sedia e il passo del ciclo in cui servono.',
      alt: 'Distinta base della sedia in acciaio e rovere: componenti con tipo, quantità, unità e operazione in cui sono usati.',
    },
    {
      n: 2,
      name: 'overview',
      height: 527,
      title: 'Rilascia',
      caption: 'L’ordine genera gli ordini di lavoro, anche per i semilavorati, con le dipendenze tra uno e l’altro.',
      alt: 'Panoramica grafica di un ordine: due ordini figli raggruppati con le schede degli ordini di lavoro e le frecce di dipendenza.',
    },
    {
      n: 3,
      name: 'planning',
      height: 1000,
      title: 'Pianifica',
      caption: 'Assegna i lavori alle macchine su una timeline per giorno, settimana o mese, con ritardi e lavori in esecuzione in vista.',
      alt: 'Pianificazione: timeline con una riga per macchina e i lavori colorati per ordine.',
    },
    {
      n: 4,
      name: 'work-orders',
      height: 780,
      title: 'Esegui',
      caption: 'Avvia, metti in pausa e chiudi ogni lavoro: vedi cosa è pronto e su quale macchina, con inizio e fine previsti.',
      alt: 'Elenco degli ordini di lavoro con fase, macchina, quantità, stato (pronto, in esecuzione, in pausa) e date previste.',
    },
  ];
}
