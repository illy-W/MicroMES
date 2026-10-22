import { Injectable, inject, signal } from '@angular/core';
import type { CreateToolingRequest, ToolingDetailModel, ToolingModel, UpdateToolingRequest } from '@micromes/api-client/models';
import { ToolingService } from '@micromes/api-client/services/tooling';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

@Injectable({ providedIn: 'root' })
export class ToolingStore {
  private readonly api = inject(ToolingService);

  readonly items = signal<ToolingModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.items.set(await firstValueFrom(this.api.getToolingList()));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  detail(id: number): Promise<ToolingDetailModel> {
    return firstValueFrom(this.api.getTooling(id));
  }

  create(req: CreateToolingRequest): Promise<ToolingDetailModel> {
    return firstValueFrom(this.api.createTooling(req));
  }

  update(id: number, req: UpdateToolingRequest): Promise<ToolingDetailModel> {
    return firstValueFrom(this.api.updateTooling(id, req));
  }

  deactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.deactivateTooling(id));
  }

  reactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.reactivateTooling(id));
  }
}
