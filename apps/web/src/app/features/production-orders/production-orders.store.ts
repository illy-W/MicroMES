import { Injectable, inject, signal } from '@angular/core';
import type {
  AbortRequest,
  CreateProductionOrderRequest,
  MaterialModel,
  ProductionOrderDetailModel,
  ProductionOrderModel,
  UpdateProductionOrderRequest,
} from '@micromes/api-client/models';
import { MaterialType } from '@micromes/api-client/models';
import { MaterialsService } from '@micromes/api-client/services/materials';
import { ProductionOrdersService } from '@micromes/api-client/services/production-orders';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

/** Store a signals degli ordini di produzione (elenco) e comandi condivisi con la pagina di dettaglio. */
@Injectable({ providedIn: 'root' })
export class ProductionOrdersStore {
  private readonly api = inject(ProductionOrdersService);
  private readonly materialsApi = inject(MaterialsService);

  readonly orders = signal<ProductionOrderModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);

  readonly materials = signal<MaterialModel[]>([]);
  readonly materialsLoading = signal(false);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.orders.set(await firstValueFrom(this.api.getProductionOrders()));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  /** Materiali attivi producibili (semilavorati e prodotti finiti) per il dialog di creazione. */
  async loadMaterials(): Promise<void> {
    this.materialsLoading.set(true);
    try {
      const all = await firstValueFrom(this.materialsApi.getMaterials());
      this.materials.set(
        all.filter((m) => m.isActive && (m.type === MaterialType.Intermediate || m.type === MaterialType.FinishedGood)),
      );
    } finally {
      this.materialsLoading.set(false);
    }
  }

  get(id: number): Promise<ProductionOrderDetailModel> {
    return firstValueFrom(this.api.getProductionOrder(id));
  }

  create(req: CreateProductionOrderRequest): Promise<ProductionOrderDetailModel> {
    return firstValueFrom(this.api.createProductionOrder(req));
  }

  update(id: number, req: UpdateProductionOrderRequest): Promise<ProductionOrderDetailModel> {
    return firstValueFrom(this.api.updateProductionOrder(id, req));
  }

  release(id: number): Promise<ProductionOrderDetailModel> {
    return firstValueFrom(this.api.releaseProductionOrder(id));
  }

  cancel(id: number): Promise<ProductionOrderDetailModel> {
    return firstValueFrom(this.api.cancelProductionOrder(id));
  }

  abort(id: number, req: AbortRequest): Promise<ProductionOrderDetailModel> {
    return firstValueFrom(this.api.abortProductionOrder(id, req));
  }
}
