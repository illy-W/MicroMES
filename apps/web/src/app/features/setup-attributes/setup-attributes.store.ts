import { Injectable, inject, signal } from '@angular/core';
import type { CreateSetupAttributeRequest, SetupAttributeModel, UpdateSetupAttributeRequest } from '@micromes/api-client/models';
import { SetupAttributesService } from '@micromes/api-client/services/setup-attributes';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

@Injectable({ providedIn: 'root' })
export class SetupAttributesStore {
  private readonly api = inject(SetupAttributesService);

  readonly items = signal<SetupAttributeModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.items.set(await firstValueFrom(this.api.getSetupAttributesList()));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  create(req: CreateSetupAttributeRequest): Promise<SetupAttributeModel> {
    return firstValueFrom(this.api.createSetupAttribute(req));
  }

  update(id: number, req: UpdateSetupAttributeRequest): Promise<SetupAttributeModel> {
    return firstValueFrom(this.api.updateSetupAttribute(id, req));
  }

  deactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.deactivateSetupAttribute(id));
  }

  reactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.reactivateSetupAttribute(id));
  }
}
