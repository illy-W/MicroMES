import { Injectable, signal } from '@angular/core';

/**
 * Stato di vista condiviso tra la pagina e i template di nodo/arco (istanziati dal diagramma:
 * non ricevono input dalla pagina). Un'istanza per panoramica.
 */
@Injectable()
export class OverviewViewState {
  /** Evidenzia il percorso più lungo (attivo di default). */
  readonly showCritical = signal(true);
}
