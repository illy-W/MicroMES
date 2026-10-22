import type { WorkOrderStatus } from '@micromes/api-client/models';

export interface VisibilityOptions {
  /** `true` = le fasi terminate sono mostrate (pulsante attivo o filtro di stato "Terminato"). */
  showEnded: boolean;
  /** Lavori sempre visibili anche se terminati (selezionato, col focus). */
  keepIds?: readonly (number | null | undefined)[];
}

/** Una barra è visibile se non è terminata, oppure se le terminate sono mostrate, oppure se è selezionata/con focus. Interrotti e abortiti restano sempre visibili. */
export function isWorkVisible(w: { id: number; status: WorkOrderStatus }, o: VisibilityOptions): boolean {
  if (w.status !== 'Ended') return true;
  return o.showEnded || !!o.keepIds?.includes(w.id);
}
