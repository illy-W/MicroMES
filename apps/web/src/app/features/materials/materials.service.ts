import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import type {
  CreateMaterialRequest,
  MaterialModel,
  UpdateMaterialRequest,
} from '@micromes/api-client/models';
import { MaterialsService } from '@micromes/api-client/services/materials';
import { firstValueFrom } from 'rxjs';

export interface ApiFailure {
  status: number;
  message: string;
  fieldErrors: Record<string, string>;
}

export function toFailure(err: unknown): ApiFailure {
  if (!(err instanceof HttpErrorResponse)) {
    return { status: 0, message: 'Errore imprevisto', fieldErrors: {} };
  }
  if (err.status === 0) {
    return { status: 0, message: 'Server non raggiungibile', fieldErrors: {} };
  }
  const body = err.error ?? {};
  const fieldErrors: Record<string, string> = {};
  const errors = body.errors as Record<string, string[]> | undefined;
  if (errors) {
    for (const [k, v] of Object.entries(errors)) {
      fieldErrors[k.charAt(0).toLowerCase() + k.slice(1)] = v.join(' ');
    }
  }
  return {
    status: err.status,
    message: body.detail || body.title || 'Operazione non riuscita',
    fieldErrors,
  };
}

@Injectable({ providedIn: 'root' })
export class MaterialsStore {
  private readonly api = inject(MaterialsService);

  readonly materials = signal<MaterialModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.materials.set(await firstValueFrom(this.api.getMaterials({ includeInactive: true })));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  create(req: CreateMaterialRequest): Promise<MaterialModel> {
    return firstValueFrom(this.api.createMaterial(req));
  }

  update(id: number, req: UpdateMaterialRequest): Promise<MaterialModel> {
    return firstValueFrom(this.api.updateMaterial(id, req));
  }

  deactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.deactivateMaterial(id));
  }
}
