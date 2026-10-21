import { Injectable, inject, signal } from '@angular/core';
import type { CreateScrapReasonRequest, ScrapReasonModel, UpdateScrapReasonRequest } from '@micromes/api-client/models';
import { ScrapReasonsService } from '@micromes/api-client/services/scrap-reasons';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

@Injectable({ providedIn: 'root' })
export class ScrapReasonsStore {
  private readonly api = inject(ScrapReasonsService);

  readonly reasons = signal<ScrapReasonModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.reasons.set(await firstValueFrom(this.api.getScrapReasons({ includeInactive: true })));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  create(req: CreateScrapReasonRequest): Promise<ScrapReasonModel> {
    return firstValueFrom(this.api.createScrapReason(req));
  }

  update(id: number, req: UpdateScrapReasonRequest): Promise<ScrapReasonModel> {
    return firstValueFrom(this.api.updateScrapReason(id, req));
  }

  deactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.deactivateScrapReason(id));
  }
}
