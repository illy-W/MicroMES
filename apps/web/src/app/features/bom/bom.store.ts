import { Injectable, inject, signal } from '@angular/core';
import type { BomNodeModel, MaterialModel, RoutingModel } from '@micromes/api-client/models';
import { BomService } from '@micromes/api-client/services/bom';
import { MaterialsService } from '@micromes/api-client/services/materials';
import { RoutingService } from '@micromes/api-client/services/routing';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

/** Store a signals della distinta base di un materiale; un'istanza per pagina. */
@Injectable()
export class BomStore {
  private readonly api = inject(BomService);
  private readonly materialsApi = inject(MaterialsService);
  private readonly routingApi = inject(RoutingService);

  readonly material = signal<MaterialModel | null>(null);
  readonly nodes = signal<BomNodeModel[]>([]);
  /** Ciclo del padre (passi selezionabili); null se non caricato. */
  readonly routing = signal<RoutingModel | null>(null);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  private materialId = 0;

  async load(materialId: number): Promise<void> {
    if (materialId !== this.materialId) {
      this.material.set(null);
      this.nodes.set([]);
      this.routing.set(null);
    }
    this.materialId = materialId;
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const [material, nodes, routing] = await Promise.all([
        firstValueFrom(this.materialsApi.getMaterial(materialId)),
        firstValueFrom(this.api.getBom(materialId)),
        firstValueFrom(this.routingApi.getRouting(materialId)),
      ]);
      if (materialId !== this.materialId) return;
      this.material.set(material);
      this.nodes.set(nodes);
      this.routing.set(routing);
    } catch (e) {
      if (materialId === this.materialId) this.loadError.set(toFailure(e).message);
    } finally {
      if (materialId === this.materialId) this.loading.set(false);
    }
  }

  reload(): Promise<void> {
    return this.load(this.materialId);
  }

  add(componentMaterialId: number, quantity: number, consumedInStepId: number | null): Promise<unknown> {
    return firstValueFrom(this.api.addBomItem(this.materialId, { componentMaterialId, quantity, consumedInStepId }));
  }

  /** PUT: omettere il passo lo azzera, quindi va sempre inviato il valore corrente. */
  update(bomItemId: number, quantity: number, consumedInStepId: number | null): Promise<unknown> {
    return firstValueFrom(this.api.updateBomItem(bomItemId, { quantity, consumedInStepId }));
  }

  remove(bomItemId: number): Promise<unknown> {
    return firstValueFrom(this.api.deleteBomItem(bomItemId));
  }
}
