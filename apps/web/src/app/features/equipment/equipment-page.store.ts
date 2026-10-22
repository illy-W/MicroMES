import { Injectable, inject, signal } from '@angular/core';
import type { EquipmentModel, EquipmentOverviewModel } from '@micromes/api-client/models';
import { EquipmentService } from '@micromes/api-client/services/equipment';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

/** Stato della pagina di dettaglio macchina (una istanza per pagina). */
@Injectable()
export class EquipmentPageStore {
  private readonly api = inject(EquipmentService);

  readonly equipment = signal<EquipmentModel | null>(null);
  readonly overview = signal<EquipmentOverviewModel | null>(null);
  readonly loading = signal(false);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly overviewError = signal<string | null>(null);

  async load(id: number): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.error.set(null);
    try {
      this.equipment.set(await firstValueFrom(this.api.getEquipment(id)));
    } catch (e) {
      this.equipment.set(null);
      this.overview.set(null);
      if (e instanceof HttpErrorResponse && e.status === 404) this.notFound.set(true);
      else this.error.set(toFailure(e).message);
      this.loading.set(false);
      return;
    }
    this.loading.set(false);
    await this.loadOverview(id);
  }

  async loadOverview(id: number): Promise<void> {
    this.overviewError.set(null);
    try {
      this.overview.set(await firstValueFrom(this.api.getEquipmentOverview(id)));
    } catch (e) {
      this.overviewError.set(toFailure(e).message);
    }
  }
}
