import { Injectable, inject, signal } from '@angular/core';
import type { PlanAlertModel, PlanAlertsModel } from '@micromes/api-client/models';
import { PlanningService } from '@micromes/api-client/services/planning';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

/** Store delle segnalazioni di pianificazione. */
@Injectable({ providedIn: 'root' })
export class PlanningStore {
  private readonly api = inject(PlanningService);

  readonly alerts = signal<PlanAlertModel[]>([]);
  readonly criticalCount = signal(0);
  readonly warningCount = signal(0);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  /** Richiesta di mostrare un lavoro nella timeline (`n` cambia a ogni richiesta). */
  readonly focusRequest = signal<{ workOrderId: number | null; productionOrderId: number | null; n: number } | null>(null);

  showInPlan(workOrderId: number | null | undefined, productionOrderId?: number | null): void {
    this.focusRequest.set({ workOrderId: workOrderId ?? null, productionOrderId: productionOrderId ?? null, n: (this.focusRequest()?.n ?? 0) + 1 });
  }

  async loadAlerts(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const r: PlanAlertsModel = await firstValueFrom(this.api.getPlanAlerts());
      this.alerts.set(r.alerts);
      this.criticalCount.set(r.criticalCount);
      this.warningCount.set(r.warningCount);
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }
}
