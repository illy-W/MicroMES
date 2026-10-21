import { Injectable, inject } from '@angular/core';
import type { EquipmentModel, WorkOrderEquipmentModel } from '@micromes/api-client/models';
import { EquipmentService } from '@micromes/api-client/services/equipment';
import { WorkOrdersService } from '@micromes/api-client/services/work-orders';
import { firstValueFrom } from 'rxjs';

/** Lavoro in esecuzione che usa una macchina. */
export interface MachineUse {
  workOrderId: number;
  code: string;
}

/** Macchina -> lavori `Running` che la stanno usando (macchina della sessione aperta). */
export type MachineUsage = ReadonlyMap<number, readonly MachineUse[]>;

interface RunningCandidate {
  id: number;
  code: string;
  status: string;
  currentEquipment?: WorkOrderEquipmentModel | null;
}

/** Costruisce la mappa macchina -> lavori `Running`, escludendo (opzionale) un lavoro. */
export function machineUsage(works: readonly RunningCandidate[], excludeWorkOrderId?: number): MachineUsage {
  const map = new Map<number, MachineUse[]>();
  for (const w of works) {
    if (w.status !== 'Running' || !w.currentEquipment || w.id === excludeWorkOrderId) continue;
    const list = map.get(w.currentEquipment.id) ?? [];
    list.push({ workOrderId: w.id, code: w.code });
    map.set(w.currentEquipment.id, list);
  }
  return map;
}

export const usageText = (uses: readonly MachineUse[]): string => `In uso da ${uses.map((u) => u.code).join(', ')}`;

export const machineLabel = (m: { code: string; name: string }): string => `${m.code} · ${m.name}`;

/** Macchine candidate di una fase e lavori in esecuzione (solo lettura, nessun endpoint dedicato). */
@Injectable({ providedIn: 'root' })
export class WorkOrderMachinesService {
  private readonly equipmentApi = inject(EquipmentService);
  private readonly workApi = inject(WorkOrdersService);

  /** Macchine attive collegate alla fase. */
  async candidates(processSegmentId: number): Promise<EquipmentModel[]> {
    const list = await firstValueFrom(this.equipmentApi.getEquipmentList({ processSegmentId }));
    return list.filter((e) => e.isActive);
  }

  /** Dall'elenco lavori filtrato per stato `Running`. */
  async usage(excludeWorkOrderId?: number): Promise<MachineUsage> {
    const running = await firstValueFrom(this.workApi.getWorkOrders({ status: 'Running' }));
    return machineUsage(running, excludeWorkOrderId);
  }
}
