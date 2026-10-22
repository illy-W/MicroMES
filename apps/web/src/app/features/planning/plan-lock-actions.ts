import { toast } from '@spartan-ng/brain/sonner';
import { toFailure } from '../materials/materials.service';
import type { WorkOrdersStore } from '../work-orders/work-orders.store';

/** Fissa o sblocca il piano di un lavoro mostrando un toast; i 409/422 mostrano il messaggio specifico del server. Ritorna `true` se riuscito. */
export async function runPlanLockAction(store: WorkOrdersStore, kind: 'lock' | 'unlock', id: number): Promise<boolean> {
  try {
    if (kind === 'lock') await store.lockPlan(id);
    else await store.unlockPlan(id);
    toast.success(kind === 'lock' ? 'Lavoro fissato' : 'Lavoro sbloccato');
    return true;
  } catch (e) {
    const f = toFailure(e);
    const detail = Object.values(f.fieldErrors).join(' ');
    toast.error((f.status === 422 && detail ? detail : f.message) || 'Operazione non riuscita');
    return false;
  }
}
