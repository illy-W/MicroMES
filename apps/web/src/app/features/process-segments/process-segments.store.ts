import { Injectable, inject, signal } from '@angular/core';
import type {
  CreateProcessSegmentRequest,
  ProcessSegmentModel,
  UpdateProcessSegmentRequest,
} from '@micromes/api-client/models';
import { ProcessSegmentsService } from '@micromes/api-client/services/process-segments';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

@Injectable({ providedIn: 'root' })
export class ProcessSegmentsStore {
  private readonly api = inject(ProcessSegmentsService);

  readonly segments = signal<ProcessSegmentModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.segments.set(await firstValueFrom(this.api.getProcessSegments({ includeInactive: true })));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  create(req: CreateProcessSegmentRequest): Promise<ProcessSegmentModel> {
    return firstValueFrom(this.api.createProcessSegment(req));
  }

  update(id: number, req: UpdateProcessSegmentRequest): Promise<ProcessSegmentModel> {
    return firstValueFrom(this.api.updateProcessSegment(id, req));
  }

  deactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.deactivateProcessSegment(id));
  }
}
