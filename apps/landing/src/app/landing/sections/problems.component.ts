import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-problems',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'bg-primary text-primary-foreground block' },
  template: `
    <div class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
      <p class="tag tag-inv">Il problema</p>
      <h2 class="mt-3 max-w-3xl">Tre domande, ogni giorno, senza un consulente.</h2>
      <ol class="mt-10 grid gap-px sm:grid-cols-3">
        @for (q of questions; track q.n) {
          <li class="border-primary-foreground/25 border-t-2 pt-4 sm:pr-6">
            <span class="text-signal-light font-mono text-sm font-bold">{{ q.n }}</span>
            <p class="mt-2 text-xl leading-snug font-semibold">{{ q.q }}</p>
            <p class="text-primary-foreground/75 mt-3 text-sm">{{ q.a }}</p>
          </li>
        }
      </ol>
    </div>
  `,
})
export class ProblemsComponent {
  protected readonly questions = [
    {
      n: '1',
      q: 'Cosa devo produrre, per quando, e a che punto siamo?',
      a: 'Ordini di produzione con date richieste, avanzamento e fine pianificata sempre in vista.',
    },
    {
      n: '2',
      q: 'Cosa posso avviare adesso e cosa sta aspettando cosa?',
      a: 'Ogni lavoro è In attesa, Pronto o In esecuzione. Le dipendenze tra fasi sono esplicite e il sistema non permette cicli.',
    },
    {
      n: '3',
      q: 'Cosa è stato prodotto davvero, in quanto tempo e con quanti scarti?',
      a: 'Buoni, scarti con il loro motivo e tempi registrati su ogni lavoro, mai sovrascritti.',
    },
  ];
}
