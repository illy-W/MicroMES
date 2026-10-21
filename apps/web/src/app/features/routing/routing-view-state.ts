import { Injectable, signal } from '@angular/core';

/**
 * Stato di vista condiviso tra la pagina e i template di nodo/arco del diagramma
 * (i template sono istanziati dal diagramma e non ricevono input dalla pagina).
 */
@Injectable()
export class RoutingViewState {
  /** True in modalità modifica. */
  readonly editing = signal(false);
  /** Passi sul percorso più lungo (evidenziati solo in lettura). */
  readonly criticalNodeIds = signal<ReadonlySet<string>>(new Set());
  /** Archi sul percorso più lungo, chiave `source->target`. */
  readonly criticalEdgeKeys = signal<ReadonlySet<string>>(new Set());
}
