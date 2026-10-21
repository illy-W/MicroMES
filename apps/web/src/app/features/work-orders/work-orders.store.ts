import { Injectable, inject, signal } from '@angular/core';
import type { AbortRequest, CloseSessionRequest, CorrectSessionRequest, WorkOrderDetailModel, WorkOrderModel } from '@micromes/api-client/models';
import { WorkOrdersService } from '@micromes/api-client/services/work-orders';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

/** Store a signals degli ordini di lavoro: elenco completo (i filtri sono client-side) e comandi. */
@Injectable({ providedIn: 'root' })
export class WorkOrdersStore {
  private readonly api = inject(WorkOrdersService);

  readonly workOrders = signal<WorkOrderModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.workOrders.set(await firstValueFrom(this.api.getWorkOrders()));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  /** Aggiorna subito la riga con la risposta del comando; gli altri lavori (successori) si riallineano con load(). */
  private apply(updated: WorkOrderDetailModel): WorkOrderDetailModel {
    this.workOrders.update((list) => list.map((w) => (w.id === updated.id ? { ...w, ...updated } : w)));
    return updated;
  }

  async start(id: number): Promise<WorkOrderDetailModel> {
    return this.apply(await firstValueFrom(this.api.startWorkOrder(id)));
  }

  async resume(id: number): Promise<WorkOrderDetailModel> {
    return this.apply(await firstValueFrom(this.api.resumeWorkOrder(id)));
  }

  async pause(id: number, req: CloseSessionRequest): Promise<WorkOrderDetailModel> {
    return this.apply(await firstValueFrom(this.api.pauseWorkOrder(id, req)));
  }

  async stop(id: number, req: CloseSessionRequest): Promise<WorkOrderDetailModel> {
    return this.apply(await firstValueFrom(this.api.stopWorkOrder(id, req)));
  }

  async abort(id: number, req: AbortRequest): Promise<WorkOrderDetailModel> {
    return this.apply(await firstValueFrom(this.api.abortWorkOrder(id, req)));
  }

  getDetail(id: number): Promise<WorkOrderDetailModel> {
    return firstValueFrom(this.api.getWorkOrder(id));
  }

  async correct(id: number, responseId: number, req: CorrectSessionRequest): Promise<WorkOrderDetailModel> {
    return this.apply(await firstValueFrom(this.api.correctWorkOrderSession(id, responseId, req)));
  }
}
