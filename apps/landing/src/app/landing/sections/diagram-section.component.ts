import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleCheck, lucideCircleDot, lucideClock, lucidePlay } from '@ng-icons/lucide';
import { FLOW_STATUS, FLOW_TEXT } from './flow/flow-data';
import { FlowDiagramComponent } from './flow/flow-diagram.component';

@Component({
  selector: 'app-diagram-section',
  imports: [FlowDiagramComponent, NgIcon],
  providers: [provideIcons({ lucideCircleCheck, lucideCircleDot, lucideClock, lucidePlay })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <section id="flusso" aria-labelledby="flusso-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-24">
      <p class="tag">Panoramica grafica</p>
      <h2 id="flusso-titolo" class="mt-3 max-w-3xl">Vedi a colpo d’occhio cosa blocca cosa.</h2>
      <p class="text-muted-foreground mt-4 max-w-2xl">
        Una sedia ha un telaio da saldare e un sedile da sagomare. L’assemblaggio parte solo quando entrambi sono pronti.
        Il percorso più lungo, quello che decide quando finisci, è evidenziato. Esempio statico: le schede non si muovono.
      </p>

      <ul class="mt-6 flex flex-wrap items-center gap-x-5 gap-y-2 text-sm" aria-label="Legenda degli stati">
        @for (s of legend; track s.label) {
          <li class="flex items-center gap-1.5" [class]="s.tone">
            <ng-icon [name]="s.icon" aria-hidden="true" /> {{ s.label }}
          </li>
        }
        <li class="text-muted-foreground flex items-center gap-1.5">
          <span class="border-signal bg-accent inline-block size-3 rounded-sm border-2" aria-hidden="true"></span>
          Percorso più lungo
        </li>
      </ul>

      <div
        class="border-border bg-card mt-4 overflow-x-auto rounded-lg border-2"
        role="region"
        aria-label="Diagramma di esempio dell’ordine OP-2026-0012. L’elenco testuale equivalente è sotto."
        tabindex="0"
      >
        @defer (on viewport) {
          <app-flow-diagram />
        } @placeholder {
          <div class="text-muted-foreground grid h-[440px] w-[1100px] place-items-center text-sm">Il diagramma si carica allo scorrimento…</div>
        } @loading {
          <div class="text-muted-foreground grid h-[440px] w-[1100px] place-items-center text-sm" role="status">Caricamento del diagramma…</div>
        }
      </div>

      <details class="mt-6">
        <summary class="flex cursor-pointer items-center text-sm font-semibold min-h-8 max-sm:min-h-11">Versione testuale del diagramma</summary>
        <div class="mt-3 grid gap-6 sm:grid-cols-2">
          @for (o of text; track o.order) {
            <div>
              <h3 class="text-base">{{ o.order }}</h3>
              <ol class="text-muted-foreground mt-2 list-decimal space-y-1 ps-5 text-sm">
                @for (i of o.items; track i.phase) {
                  <li><span class="text-foreground font-medium">{{ i.phase }}</span>: {{ i.status }}, {{ i.note }}.</li>
                }
              </ol>
            </div>
          }
        </div>
      </details>
    </section>
  `,
})
export class DiagramSectionComponent {
  protected readonly legend = Object.values(FLOW_STATUS);
  protected readonly text = FLOW_TEXT;
}
