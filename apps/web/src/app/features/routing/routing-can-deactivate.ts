import type { CanDeactivateFn } from '@angular/router';
import type { RoutingComponent } from './routing.component';

/** Guard: chiede conferma se ci sono modifiche non salvate (import solo di tipo: la pagina resta lazy). */
export const routingCanDeactivate: CanDeactivateFn<RoutingComponent> = (c) => c.confirmLeave();
