import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, Injector, input, signal, untracked } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideMaximize,
  lucideRefreshCw,
  lucideLayoutDashboard,
  lucideTriangleAlert,
} from '@ng-icons/lucide';
import type { ProductionOrderOverviewModel } from '@micromes/api-client/models';
import { ProductionOrdersService } from '@micromes/api-client/services/production-orders';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmProgressImports } from '@spartan-ng/helm/progress';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import {
  initializeModel,
  NgDiagramBackgroundComponent,
  NgDiagramComponent,
  NgDiagramEdgeTemplateMap,
  NgDiagramMinimapComponent,
  NgDiagramModelService,
  NgDiagramNodeTemplateMap,
  NgDiagramService,
  NgDiagramViewportService,
  provideNgDiagram,
  type Middleware,
  type SelectionChangedEvent,
} from 'ng-diagram';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../../materials/materials.service';
import { WORK_STATUS } from '../production-order-labels';
import { OverviewEdgeComponent } from './overview-edge.component';
import { layoutOverview } from './overview-layout';
import {
  buildOverviewGraph,
  formatTime,
  ORDER_GROUP_TYPE,
  OVERVIEW_EDGE_TYPE,
  OVERVIEW_MINIMAP_THRESHOLD,
  summarize,
  WORK_NODE_TYPE,
  type OverviewGraph,
} from './overview-model';
import { OverviewOrderGroupComponent } from './overview-order-group.component';
import { OverviewPanelComponent, type OverviewSelection } from './overview-panel.component';
import { OverviewViewState } from './overview-view-state';
import { OverviewWorkNodeComponent } from './overview-work-node.component';

/** Azioni bloccate in sola lettura (stesso insieme di features/routing + ridimensionamento/rotazione). */
const READ_ONLY_BLOCKED = new Set<string>([
  'deleteSelection',
  'deleteNodes',
  'deleteEdges',
  'deleteElements',
  'startLinking',
  'startLinkingFromPosition',
  'finishLinking',
  'startRelinking',
  'finishRelinking',
  'moveNodes',
  'moveNodesBy',
  'moveNodesStart',
  'resizeNode',
  'rotateNodeTo',
  'paste',
  'paletteDropNode',
]);

type DiagramModel = ReturnType<typeof initializeModel>;

/**
 * Panoramica grafica dell'ordine (sola lettura). Il modello ngDiagram si crea UNA volta dopo il primo
 * caricamento e non viene più ricreato: "Aggiorna" aggiorna i dati dei nodi in place (vista e selezione
 * restano) e ricalcola il layout solo se cambia l'insieme di nodi/archi.
 */
@Component({
  selector: 'app-production-order-overview',
  imports: [
    NgDiagramComponent,
    NgDiagramBackgroundComponent,
    NgDiagramMinimapComponent,
    OverviewPanelComponent,
    HlmButton,
    HlmEmptyImports,
    HlmProgressImports,
    HlmSkeleton,
    HlmSwitch,
    NgIcon,
  ],
  providers: [
    provideNgDiagram(),
    OverviewViewState,
    provideIcons({ lucideMaximize, lucideRefreshCw, lucideLayoutDashboard, lucideTriangleAlert }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
  `,
  template: `
    @if (!overview() && loading()) {
      <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento panoramica">
        <hlm-skeleton class="h-14 w-full" />
        <hlm-skeleton class="h-96 w-full" />
      </div>
    } @else if (!overview() && loadError()) {
      <hlm-empty>
        <hlm-empty-header>
          <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
          <div hlmEmptyTitle>Impossibile caricare la panoramica</div>
          <div hlmEmptyDescription>{{ loadError() }}</div>
        </hlm-empty-header>
        <hlm-empty-content>
          <button hlmBtn type="button" (click)="reload()">Riprova</button>
        </hlm-empty-content>
      </hlm-empty>
    } @else if (overview(); as ov) {
      @if (!ov.workOrders.length) {
        <p class="text-muted-foreground text-sm">Nessun lavoro da mostrare per questo ordine.</p>
      } @else {
        @let s = summary();
        <div class="flex flex-col gap-3">
          <div class="bg-card flex flex-wrap items-center justify-between gap-x-6 gap-y-2 rounded-lg border px-4 py-3" role="group" aria-label="Riepilogo dei lavori">
            <dl class="flex flex-wrap items-baseline gap-x-6 gap-y-1 text-sm">
              <div class="flex items-baseline gap-1.5">
                <dt class="text-muted-foreground">Conclusi</dt>
                <dd class="font-semibold tabular-nums">{{ s.ended }} / {{ s.total }}</dd>
              </div>
              <div class="flex items-baseline gap-1.5">
                <dt class="text-muted-foreground">In esecuzione</dt>
                <dd class="font-semibold tabular-nums">{{ s.running }}</dd>
              </div>
              <div class="flex items-baseline gap-1.5">
                <dt class="text-muted-foreground">Pronti</dt>
                <dd class="font-semibold tabular-nums">{{ s.ready }}</dd>
              </div>
              <div class="flex items-baseline gap-1.5">
                <dt class="text-muted-foreground">In attesa</dt>
                <dd class="font-semibold tabular-nums">{{ s.waiting }}</dd>
              </div>
              <div class="flex items-baseline gap-1.5">
                <dt class="text-muted-foreground">Interrotti o abortiti</dt>
                <dd class="font-semibold tabular-nums">{{ s.problems }}</dd>
              </div>
              <div class="flex items-center gap-1.5">
                <dt class="text-muted-foreground">Avanzamento</dt>
                <dd class="flex items-center gap-2 font-semibold tabular-nums">
                  <hlm-progress class="w-20" [value]="s.progressPercent" aria-label="Avanzamento dei lavori">
                    <hlm-progress-indicator />
                  </hlm-progress>
                  {{ s.progressPercent }}%
                </dd>
              </div>
              <div class="flex items-baseline gap-1.5">
                <dt class="text-muted-foreground">Durata minima</dt>
                <dd class="font-semibold tabular-nums">{{ s.minimumDuration }}</dd>
              </div>
            </dl>
            <div class="flex items-center gap-3">
              <span class="text-muted-foreground text-xs" role="status">Aggiornato alle {{ updatedAt() }}</span>
              <button hlmBtn variant="outline" size="sm" type="button" [disabled]="busy()" (click)="refresh()">
                <ng-icon name="lucideRefreshCw" [class.animate-spin]="refreshing()" aria-hidden="true" /> Aggiorna
              </button>
            </div>
          </div>
          @if (loadError()) {
            <p class="text-destructive flex flex-wrap items-center gap-2 text-sm" role="alert">
              <ng-icon name="lucideTriangleAlert" aria-hidden="true" /> Aggiornamento non riuscito: {{ loadError() }}
              <button hlmBtn variant="link" size="sm" type="button" class="h-auto p-0" (click)="refresh()">Riprova</button>
            </p>
          }

          <div class="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
            <ul class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs" aria-label="Legenda degli stati">
              @for (st of legend; track st.label) {
                <li class="flex items-center gap-1" [class]="st.tone">
                  <ng-icon [name]="st.icon" aria-hidden="true" /> {{ st.label }}
                </li>
              }
              <li class="text-muted-foreground flex items-center gap-1.5">
                <svg width="28" height="8" aria-hidden="true"><line x1="0" y1="4" x2="28" y2="4" class="stroke-current" stroke-width="2" stroke-dasharray="6 4" /></svg>
                Dipendenza tra ordini
              </li>
              <li class="text-muted-foreground flex items-center gap-1.5">
                <span class="border-primary bg-accent inline-block size-3 rounded-sm border-2" aria-hidden="true"></span>
                Percorso più lungo
              </li>
            </ul>
            <div class="flex flex-wrap items-center gap-3">
              <label class="flex items-center gap-2 text-sm" for="ov-critical">
                <hlm-switch inputId="ov-critical" [checked]="view.showCritical()" (checkedChange)="view.showCritical.set($event)" />
                Mostra percorso più lungo
              </label>
              <button hlmBtn variant="outline" size="sm" type="button" [disabled]="busy() || !ready()" (click)="rearrange()">
                <ng-icon name="lucideLayoutDashboard" aria-hidden="true" /> Riordina
              </button>
              <button hlmBtn variant="outline" size="sm" type="button" [disabled]="busy() || !ready()" (click)="fitView()">
                <ng-icon name="lucideMaximize" aria-hidden="true" /> Adatta
              </button>
            </div>
          </div>

          <div class="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
            <!-- Il diagramma non si smonta mai: invisible (non display:none) finché il primo layout non è finito. -->
            <div class="relative h-[calc(100vh-24rem)] min-h-96 min-w-0 overflow-hidden rounded-md border" [class.invisible]="!ready()">
              @if (model(); as m) {
                <ng-diagram
                  [model]="m"
                  [nodeTemplateMap]="nodeTemplateMap"
                  [edgeTemplateMap]="edgeTemplateMap"
                  [config]="config"
                  (diagramInit)="onInit()"
                  (selectionChanged)="onSelection($event)"
                >
                  <ng-diagram-background />
                </ng-diagram>
                @if (showMinimap()) {
                  <ng-diagram-minimap position="bottom-right" />
                }
              }
            </div>
            <app-overview-panel [overview]="ov" [rootOrderId]="orderId()" [selection]="selected()" />
          </div>
        </div>
      }
    }
  `,
})
export class ProductionOrderOverviewComponent {
  private readonly injector = inject(Injector);
  private readonly api = inject(ProductionOrdersService);
  private readonly modelService = inject(NgDiagramModelService);
  private readonly viewport = inject(NgDiagramViewportService);
  private readonly diagramService = inject(NgDiagramService);
  protected readonly view = inject(OverviewViewState);

  readonly orderId = input.required<number>();
  /** Cambia quando la pagina ricarica l'ordine con un nuovo stato: ricarica la panoramica in place. */
  readonly refreshKey = input<string>('');

  protected readonly overview = signal<ProductionOrderOverviewModel | null>(null);
  protected readonly model = signal<DiagramModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly refreshing = signal(false);
  protected readonly working = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly ready = signal(false);
  protected readonly selected = signal<OverviewSelection | null>(null);

  protected readonly busy = computed(() => this.loading() || this.refreshing() || this.working());
  protected readonly summary = computed(() => summarize(this.overview() as ProductionOrderOverviewModel));
  protected readonly updatedAt = computed(() => formatTime(this.overview()?.generatedAt ?? new Date().toISOString()));
  protected readonly showMinimap = computed(
    () => (this.overview()?.workOrders.length ?? 0) + (this.overview()?.orders.length ?? 0) > OVERVIEW_MINIMAP_THRESHOLD,
  );
  protected readonly legend = Object.values(WORK_STATUS);

  protected readonly nodeTemplateMap = new NgDiagramNodeTemplateMap([
    [WORK_NODE_TYPE, OverviewWorkNodeComponent],
    [ORDER_GROUP_TYPE, OverviewOrderGroupComponent],
  ]);
  protected readonly edgeTemplateMap = new NgDiagramEdgeTemplateMap([[OVERVIEW_EDGE_TYPE, OverviewEdgeComponent]]);
  /** Nessun trascinamento né ridimensionamento/rotazione; pan, zoom e selezione restano. */
  protected readonly config = {
    nodeDraggingEnabled: false,
    resize: { defaultResizable: false },
    nodeRotation: { defaultRotatable: false },
  };

  /** True mentre "Aggiorna" aggiunge/rimuove nodi: il middleware lascia passare solo quelle modifiche. */
  private mutating = false;
  private unregisterGate: (() => void) | null = null;
  private lastKey: string | null = null;

  private readonly readOnlyGate: Middleware = {
    name: 'overview-read-only',
    execute: (context, next, cancel) => {
      if (!this.mutating && context.modelActionTypes.some((a) => READ_ONLY_BLOCKED.has(a))) {
        cancel();
        return;
      }
      void next();
    },
  };

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.unregisterGate?.();
      this.unregisterGate = null;
    });
    effect(() => {
      const id = this.orderId();
      untracked(() => void this.load(id));
    });
    effect(() => {
      const key = this.refreshKey();
      untracked(() => {
        if (this.lastKey !== null && this.lastKey !== key && this.model()) void this.refresh();
        this.lastKey = key;
      });
    });
  }

  protected reload(): void {
    if (this.model()) void this.refresh();
    else void this.load(this.orderId());
  }

  private fetch(): Promise<ProductionOrderOverviewModel> {
    return firstValueFrom(this.api.getProductionOrderOverview(this.orderId()));
  }

  private failure(e: unknown): string {
    const f = toFailure(e);
    return f.status === 404 ? 'Ordine non trovato.' : f.message;
  }

  /** Primo caricamento: crea il modello una sola volta (dopo un await serve l'injector). */
  private async load(id: number): Promise<void> {
    if (this.model()) return;
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const ov = await this.fetch();
      const graph = buildOverviewGraph(ov, id);
      this.overview.set(ov);
      if (ov.workOrders.length) {
        this.model.set(initializeModel({ nodes: graph.nodes, edges: graph.edges }, this.injector));
      }
    } catch (e) {
      this.loadError.set(this.failure(e));
    } finally {
      this.loading.set(false);
    }
  }

  /** Il flow core è nuovo a ogni inizializzazione: gate e config si registrano qui, poi primo layout. */
  protected async onInit(): Promise<void> {
    this.diagramService.unregisterMiddleware(this.readOnlyGate.name);
    this.unregisterGate = this.diagramService.registerMiddleware(this.readOnlyGate) as (() => void) | null;
    this.diagramService.updateConfig({ nodeDraggingEnabled: false });
    try {
      await this.layout();
      await this.fitView();
    } finally {
      this.ready.set(true);
    }
  }

  protected onSelection(e: SelectionChangedEvent): void {
    const n = e.selectedNodes.length === 1 ? e.selectedNodes[0] : null;
    if (!n) return this.selected.set(null);
    const d = n.data as { workOrderId?: number; orderId?: number };
    if (n.type === WORK_NODE_TYPE && d.workOrderId != null) this.selected.set({ kind: 'work', id: d.workOrderId });
    else if (n.type === ORDER_GROUP_TYPE && d.orderId != null) this.selected.set({ kind: 'order', id: d.orderId });
    else this.selected.set(null);
  }

  /** Layout ELK annidato: posizioni globali dei nodi e size dei gruppi in un solo commit. */
  private async layout(): Promise<void> {
    const model = this.modelService.getModel();
    const nodes = model.getNodes().filter((n) => !n.computedHidden);
    const placed = await layoutOverview(nodes, model.getEdges());
    await this.diagramService.transaction(() => {
      void this.modelService.updateNodes(
        placed.map((p) =>
          p.size ? { id: p.id, position: p.position, size: p.size, autoSize: false } : { id: p.id, position: p.position },
        ),
      );
    });
  }

  protected async rearrange(): Promise<void> {
    this.working.set(true);
    try {
      await this.layout();
      await this.fitView();
    } finally {
      this.working.set(false);
    }
  }

  /** Adatta la vista con margine, senza ingrandire oltre 1:1. */
  protected async fitView(): Promise<void> {
    await this.viewport.zoomToFit({ padding: 60 });
    const { x, y, scale, width, height } = this.modelService.getModel().getMetadata().viewport;
    if (scale > 1 && width && height) {
      const cx = width / 2;
      const cy = height / 2;
      await this.viewport.setViewport(cx - (cx - x) / scale, cy - (cy - y) / scale, 1);
    }
  }

  /** Ricarica i dati e li applica al modello esistente (nessuna ricreazione del diagramma). */
  protected async refresh(): Promise<void> {
    if (!this.model()) return void this.load(this.orderId());
    if (this.refreshing()) return;
    this.refreshing.set(true);
    this.loadError.set(null);
    try {
      const ov = await this.fetch();
      await this.apply(buildOverviewGraph(ov, this.orderId()));
      this.overview.set(ov);
    } catch (e) {
      this.loadError.set(this.failure(e));
    } finally {
      this.refreshing.set(false);
    }
  }

  private async apply(graph: OverviewGraph): Promise<void> {
    const model = this.modelService.getModel();
    const cur = model.getNodes();
    const curEdges = model.getEdges();
    const newIds = new Set(graph.nodes.map((n) => n.id));
    const curIds = new Set(cur.map((n) => n.id));
    const newEdgeIds = new Set(graph.edges.map((e) => e.id));
    const curEdgeIds = new Set(curEdges.map((e) => e.id));

    const removedNodes = cur.filter((n) => !newIds.has(n.id)).map((n) => n.id);
    const addedNodes = graph.nodes.filter((n) => !curIds.has(n.id));
    const removedEdges = curEdges.filter((e) => !newEdgeIds.has(e.id)).map((e) => e.id);
    const addedEdges = graph.edges.filter((e) => !curEdgeIds.has(e.id));
    const structural = removedNodes.length + addedNodes.length + removedEdges.length + addedEdges.length > 0;

    // Dati cambiati dei nodi/archi rimasti: solo updateNodes/updateEdges, vista e selezione intatte.
    const curNode = new Map(cur.map((n) => [n.id, n]));
    const changedNodes = graph.nodes
      .filter((n) => curIds.has(n.id) && JSON.stringify(curNode.get(n.id)?.data) !== JSON.stringify(n.data))
      .map((n) => ({ id: n.id, data: n.data }));
    const curEdge = new Map(curEdges.map((e) => [e.id, e]));
    const changedEdges = graph.edges
      .filter((e) => curEdgeIds.has(e.id) && JSON.stringify(curEdge.get(e.id)?.data) !== JSON.stringify(e.data))
      .map((e) => ({ id: e.id, data: e.data }));

    if (changedNodes.length) await this.modelService.updateNodes(changedNodes);
    if (changedEdges.length) await this.modelService.updateEdges(changedEdges);
    if (!structural) return;

    this.mutating = true;
    try {
      if (removedEdges.length) await this.modelService.deleteEdges(removedEdges);
      if (removedNodes.length) await this.modelService.deleteNodes(removedNodes);
      if (addedNodes.length) await this.modelService.addNodes(addedNodes, { waitForMeasurements: true });
      if (addedEdges.length) await this.modelService.addEdges(addedEdges, { waitForMeasurements: true });
    } finally {
      this.mutating = false;
    }
    await this.layout();
    await this.fitView();
  }
}
