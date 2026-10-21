import { Injectable, inject, signal } from '@angular/core';
import type { CreateEquipmentRequest, EquipmentModel, UpdateEquipmentRequest } from '@micromes/api-client/models';
import { EquipmentService } from '@micromes/api-client/services/equipment';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

@Injectable({ providedIn: 'root' })
export class EquipmentStore {
  private readonly api = inject(EquipmentService);

  readonly items = signal<EquipmentModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.items.set(await firstValueFrom(this.api.getEquipmentList({ includeInactive: true })));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  create(req: CreateEquipmentRequest): Promise<EquipmentModel> {
    return firstValueFrom(this.api.createEquipment(req));
  }

  update(id: number, req: UpdateEquipmentRequest): Promise<EquipmentModel> {
    return firstValueFrom(this.api.updateEquipment(id, req));
  }

  deactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.deactivateEquipment(id));
  }
}
