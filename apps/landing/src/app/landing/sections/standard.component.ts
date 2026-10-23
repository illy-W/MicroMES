import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-standard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <section id="standard" aria-labelledby="standard-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-24">
      <p class="tag">Standard e dati tuoi</p>
      <h2 id="standard-titolo" class="mt-3 max-w-3xl">Un vocabolario standard, perché i dati restino tuoi.</h2>
      <div class="mt-8 grid gap-8 md:grid-cols-2">
        <div>
          <h3>ISA-95 in forma ridotta</h3>
          <p class="text-muted-foreground mt-2 text-sm">
            Materiali, fasi di processo, ordini di lavoro: i nomi e le relazioni seguono lo standard ISA-95 (IEC 62264).
            Se un giorno colleghi un ERP, parlate la stessa lingua. Usiamo solo la parte che serve a una piccola officina.
          </p>
        </div>
        <div>
          <h3>Storico che non cambia</h3>
          <p class="text-muted-foreground mt-2 text-sm">
            Al rilascio di un ordine MicroMES salva un’istantanea di ciclo e distinta. Modificare l’anagrafica dopo non riscrive
            quello che hai già prodotto.
          </p>
        </div>
      </div>
    </section>
  `,
})
export class StandardComponent {}
