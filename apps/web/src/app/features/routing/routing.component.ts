import { ChangeDetectionStrategy, Component, computed, effect, HostListener, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideTriangleAlert, lucideWorkflow } from '@ng-icons/lucide';
import type {
  MaterialModel,
  ProcessSegmentModel,
  RoutingModel,
  UpdateRoutingRequest,
} from '@micromes/api-client/models';
import { MaterialsService } from '@micromes/api-client/services/materials';
import { ProcessSegmentsService } from '@micromes/api-client/services/process-segments';
import { RoutingService } from '@micromes/api-client/services/routing';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import {
  initializeModel,
  NgDiagramBackgroundComponent,
  NgDiagramComponent,
  NgDiagramMinimapComponent,
  NgDiagramModelService,
  NgDiagramNodeTemplateMap,
  NgDiagramSelectionService,
  NgDiagramViewportService,
  provideNgDiagram,
  type Edge,
  type Node,
  type SelectionChangedEvent,
} from 'ng-diagram';
import { firstValueFrom, map } from 'rxjs';
import { toFailure } from '../materials/materials.service';
import { layeredLayout, ROW_SPACING } from './routing-layout';
import { RoutingNodeComponent, type StepNodeData } from './routing-node.component';
import { validateRouting } from './routing-validation';

type StepNode = Node<StepNodeData>;

function toNumberOrNull(v: string): number | null {
  if (v.trim() === '') return null;
  const n = Number(v.replace(',', '.'));
  return Number.isFinite(n) ? n : null;
}

@Component({
  selector: 'app-routing',
  imports: [
    RouterLink,
    NgDiagramComponent,
    NgDiagramBackgroundComponent,
    NgDiagramMinimapComponent,
    HlmAlertDialogImports,
    HlmBadge,
    HlmButton,
    HlmEmptyImports,
    HlmInput,
    HlmLabel,
    HlmSkeleton,
    HlmTableImports,
    HlmTextarea,
    NgIcon,
  ],
  providers: [provideNgDiagram(), provideIcons({ lucidePlus, lucideTriangleAlert, lucideWorkflow })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
  `,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
          <a hlmBtn variant="link" size="sm" class="h-auto w-fit p-0" routerLink="/materials">← Materiali</a>
          <h1 class="text-xl font-semibold">
            Ciclo di lavoro
            @if (material(); as m) {
              <span class="text-muted-foreground font-normal">· {{ m.code }} — {{ m.name }}</span>
            }
          </h1>
          <p class="text-muted-foreground text-sm">Passi di produzione e dipendenze: collega le porte da destra a sinistra.</p>
        </div>
        <div class="flex items-center gap-2">
          @if (dirty()) {
            <span hlmBadge variant="outline" role="status">Modifiche non salvate</span>
          }
          <button hlmBtn variant="outline" size="sm" type="button" [disabled]="!dirty() || saving()" (click)="discard()">
            Annulla modifiche
          </button>
          <button hlmBtn size="sm" type="button" [disabled]="!dirty() || saving() || loading()" (click)="save()">
            Salva
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento ciclo di lavoro">
          <hlm-skeleton class="h-10 w-64" />
          <hlm-skeleton class="h-96 w-full" />
        </div>
      } @else if (loadError()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare il ciclo di lavoro</div>
            <div hlmEmptyDescription>{{ loadError() }}</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="load()">Riprova</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else {
        @if (saveError()) {
          <div class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
            {{ saveError() }}
          </div>
        }
        @if (validationErrors().length) {
          <div class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
            <p class="font-medium">Correggi prima di salvare:</p>
            <ul class="list-disc pl-5">
              @for (e of validationErrors(); track e) {
                <li>{{ e }}</li>
              }
            </ul>
          </div>
        }

        <div class="flex items-center gap-2" role="group" aria-label="Vista del ciclo">
          <button hlmBtn size="sm" type="button" [variant]="view() === 'diagram' ? 'default' : 'outline'"
            [attr.aria-pressed]="view() === 'diagram'" (click)="view.set('diagram')">Diagramma</button>
          <button hlmBtn size="sm" type="button" [variant]="view() === 'list' ? 'default' : 'outline'"
            [attr.aria-pressed]="view() === 'list'" (click)="view.set('list')">Elenco</button>
        </div>

        <div class="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <div class="min-w-0">
            @if (view() === 'diagram') {
              <div class="relative h-[calc(100vh-18rem)] min-h-96 overflow-hidden rounded-md border">
                <ng-diagram
                  [model]="model"
                  [nodeTemplateMap]="nodeTemplateMap"
                  [config]="config"
                  (diagramInit)="onInit()"
                  (edgeDrawn)="touch()"
                  (selectionMoved)="touch()"
                  (selectionRemoved)="onRemoved()"
                  (selectionChanged)="onSelection($event)"
                >
                  <ng-diagram-background />
                  <ng-diagram-minimap position="bottom-right" />
                </ng-diagram>
                @if (!steps().length) {
                  <div class="pointer-events-none absolute inset-0 flex items-center justify-center">
                    <div class="bg-card flex max-w-sm flex-col items-center gap-2 rounded-lg border p-6 text-center shadow-sm">
                      <ng-icon name="lucideWorkflow" size="24" />
                      <p class="font-medium">Nessun passo nel ciclo</p>
                      <p class="text-muted-foreground text-sm">Aggiungi la prima fase dall'elenco a destra.</p>
                    </div>
                  </div>
                }
              </div>
            } @else {
              <div class="overflow-auto rounded-md border">
                <div hlmTableContainer>
                  <table hlmTable>
                    <caption class="sr-only">Passi del ciclo con predecessori e successori (sola lettura)</caption>
                    <thead hlmTHead class="bg-muted">
                      <tr hlmTr>
                        <th hlmTh>Op.</th>
                        <th hlmTh>Fase</th>
                        <th hlmTh class="text-right">Attrezzaggio (min)</th>
                        <th hlmTh class="text-right">Per unità (min)</th>
                        <th hlmTh>Predecessori</th>
                        <th hlmTh>Successori</th>
                      </tr>
                    </thead>
                    <tbody hlmTBody>
                      @for (r of rows(); track r.id) {
                        <tr hlmTr>
                          <td hlmTd class="tabular-nums">{{ r.op }}</td>
                          <td hlmTd><span class="font-medium">{{ r.code }}</span> <span class="text-muted-foreground">{{ r.name }}</span></td>
                          <td hlmTd class="text-right tabular-nums">{{ r.setup }}</td>
                          <td hlmTd class="text-right tabular-nums">{{ r.run }}</td>
                          <td hlmTd>{{ r.preds || '—' }}</td>
                          <td hlmTd>{{ r.succs || '—' }}</td>
                        </tr>
                      } @empty {
                        <tr hlmTr><td hlmTd colspan="6" class="text-muted-foreground text-center">Nessun passo nel ciclo.</td></tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          </div>

          <div class="flex flex-col gap-4">
            @if (selected(); as s) {
              <section class="bg-card flex flex-col gap-3 rounded-lg border p-4" aria-labelledby="step-panel-title">
                <h2 id="step-panel-title" class="text-sm font-semibold">Passo {{ s.data.code }}</h2>
                <div class="flex flex-col gap-1.5">
                  <label hlmLabel for="st-op">Numero operazione</label>
                  <input hlmInput id="st-op" type="number" min="1" step="1" [value]="s.data.operationNumber ?? ''"
                    (input)="patch(s.id, { operationNumber: num($event) })" />
                </div>
                <div class="flex flex-col gap-1.5">
                  <label hlmLabel for="st-setup">Attrezzaggio (min)</label>
                  <input hlmInput id="st-setup" type="number" min="0" step="any" [value]="s.data.setupMinutes ?? ''"
                    [placeholder]="'Standard: ' + s.data.standardSetup" (input)="patch(s.id, { setupMinutes: num($event) })" />
                  <p class="text-muted-foreground text-xs">Vuoto = valore standard della fase ({{ s.data.standardSetup }}).</p>
                </div>
                <div class="flex flex-col gap-1.5">
                  <label hlmLabel for="st-run">Tempo per unità (min)</label>
                  <input hlmInput id="st-run" type="number" min="0" step="any" [value]="s.data.runMinutesPerUnit ?? ''"
                    [placeholder]="'Standard: ' + s.data.standardRun" (input)="patch(s.id, { runMinutesPerUnit: num($event) })" />
                  <p class="text-muted-foreground text-xs">Vuoto = valore standard della fase ({{ s.data.standardRun }}).</p>
                </div>
                <div class="flex flex-col gap-1.5">
                  <label hlmLabel for="st-notes">Note</label>
                  <textarea hlmTextarea id="st-notes" maxlength="1000" [value]="s.data.notes ?? ''"
                    (input)="patch(s.id, { notes: text($event) })"></textarea>
                </div>
              </section>
            }

            <section class="bg-card flex flex-col gap-2 rounded-lg border p-4" aria-labelledby="palette-title">
              <h2 id="palette-title" class="text-sm font-semibold">Fasi disponibili</h2>
              @if (segments().length) {
                <ul class="flex max-h-80 flex-col gap-1 overflow-auto">
                  @for (seg of segments(); track seg.id) {
                    <li class="flex items-center justify-between gap-2">
                      <span class="min-w-0 text-sm">
                        <span class="font-medium">{{ seg.code }}</span>
                        <span class="text-muted-foreground block truncate text-xs">{{ seg.name }}</span>
                      </span>
                      <button hlmBtn variant="outline" size="sm" type="button" [attr.aria-label]="'Aggiungi ' + seg.code"
                        (click)="addStep(seg)">
                        <ng-icon name="lucidePlus" /> Aggiungi
                      </button>
                    </li>
                  }
                </ul>
              } @else {
                <p class="text-muted-foreground text-sm">Nessuna fase attiva. Creane una in "Fasi di processo".</p>
              }
            </section>
          </div>
        </div>
      }
    </div>

    @if (leaveResolver()) {
      <hlm-alert-dialog state="open" (closed)="resolveLeave(false)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Uscire senza salvare?</h2>
              <p hlmAlertDialogDescription>Le modifiche al ciclo di lavoro non salvate andranno perse.</p>
            </hlm-alert-dialog-header>
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Resta</button>
              <button hlmBtn type="button" variant="destructive" (click)="resolveLeave(true)">Esci senza salvare</button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }
  `,
})
export class RoutingComponent {
  private readonly routingApi = inject(RoutingService);
  private readonly materialsApi = inject(MaterialsService);
  private readonly segmentsApi = inject(ProcessSegmentsService);
  private readonly modelService = inject(NgDiagramModelService);
  private readonly viewport = inject(NgDiagramViewportService);
  private readonly selection = inject(NgDiagramSelectionService);

  private readonly materialId = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((p) => Number(p.get('id')))),
    { initialValue: 0 },
  );

  protected readonly material = signal<MaterialModel | null>(null);
  protected readonly segments = signal<ProcessSegmentModel[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly validationErrors = signal<string[]>([]);
  protected readonly dirty = signal(false);
  protected readonly view = signal<'diagram' | 'list'>('diagram');
  protected readonly leaveResolver = signal<((v: boolean) => void) | null>(null);

  private readonly version = signal(0);
  private readonly selectedId = signal<string | null>(null);
  private serverRouting: RoutingModel | null = null;
  private newCounter = 0;

  protected readonly nodeTemplateMap = new NgDiagramNodeTemplateMap([['step', RoutingNodeComponent]]);
  protected readonly model = initializeModel({ nodes: [], edges: [] });

  /** Impedisce archi che chiudono un ciclo, anelli e duplicati già durante il collegamento. */
  protected readonly config = {
    linking: {
      validateConnection: (source: Node | null, _sp: unknown, target: Node | null): boolean => {
        if (!source || !target || source.id === target.id) return false;
        const edges = this.model.getEdges();
        if (edges.some((e) => e.source === source.id && e.target === target.id)) return false;
        // il nuovo arco source→target chiude un ciclo se source è raggiungibile da target
        const stack = [target.id];
        const seen = new Set(stack);
        while (stack.length) {
          const id = stack.pop() as string;
          if (id === source.id) return false;
          for (const e of edges) {
            if (e.source === id && !seen.has(e.target)) {
              seen.add(e.target);
              stack.push(e.target);
            }
          }
        }
        return true;
      },
    },
  };

  protected readonly steps = computed<StepNode[]>(() => {
    this.version();
    return this.model.getNodes() as StepNode[];
  });

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    return id ? (this.steps().find((n) => n.id === id) ?? null) : null;
  });

  protected readonly rows = computed(() => {
    this.version();
    const nodes = this.model.getNodes() as StepNode[];
    const edges = this.model.getEdges();
    const label = (id: string) => {
      const n = nodes.find((x) => x.id === id);
      return n ? `${n.data.operationNumber ?? '—'} ${n.data.code}` : '';
    };
    return [...nodes]
      .sort((a, b) => (a.data.operationNumber ?? 0) - (b.data.operationNumber ?? 0))
      .map((n) => ({
        id: n.id,
        op: n.data.operationNumber ?? '—',
        code: n.data.code,
        name: n.data.name,
        setup: n.data.setupMinutes ?? n.data.standardSetup,
        run: n.data.runMinutesPerUnit ?? n.data.standardRun,
        preds: edges.filter((e) => e.target === n.id).map((e) => label(e.source)).join(', '),
        succs: edges.filter((e) => e.source === n.id).map((e) => label(e.target)).join(', '),
      }));
  });

  constructor() {
    effect(() => {
      const id = this.materialId();
      untracked(() => {
        if (id) void this.load();
      });
    });
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(e: BeforeUnloadEvent): void {
    if (this.dirty()) e.preventDefault();
  }

  confirmLeave(): Promise<boolean> {
    if (!this.dirty()) return Promise.resolve(true);
    return new Promise((resolve) => this.leaveResolver.set(resolve));
  }

  protected resolveLeave(value: boolean): void {
    const r = this.leaveResolver();
    this.leaveResolver.set(null);
    r?.(value);
  }

  async load(): Promise<void> {
    const id = this.materialId();
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const [material, routing, segments] = await Promise.all([
        firstValueFrom(this.materialsApi.getMaterial(id)),
        firstValueFrom(this.routingApi.getRouting(id)),
        firstValueFrom(this.segmentsApi.getProcessSegments()),
      ]);
      this.material.set(material);
      this.segments.set(segments.filter((s) => s.isActive));
      this.applyRouting(routing);
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  /** Ricostruisce il modello del diagramma dal ciclo ricevuto dal server (id definitivi). */
  private applyRouting(routing: RoutingModel): void {
    this.serverRouting = routing;
    const key = (id: number) => String(id);
    const edges = routing.dependencies.map((d) => ({
      source: key(d.predecessorStepId),
      target: key(d.successorStepId),
    }));
    const ops = new Map(routing.steps.map((s) => [key(s.id), s.operationNumber]));
    const layout = layeredLayout(
      routing.steps.map((s) => key(s.id)),
      edges,
      (id) => ops.get(id) ?? 0,
    );
    const nodes: StepNode[] = routing.steps.map((s) => ({
      id: key(s.id),
      type: 'step',
      position:
        s.positionX != null && s.positionY != null
          ? { x: s.positionX, y: s.positionY }
          : (layout.get(key(s.id)) ?? { x: 60, y: 60 }),
      data: {
        stepId: s.id,
        processSegmentId: s.processSegmentId,
        code: s.processSegmentCode,
        name: s.processSegmentName,
        standardSetup: s.standardSetupMinutes,
        standardRun: s.standardRunMinutesPerUnit,
        operationNumber: s.operationNumber,
        setupMinutes: s.setupMinutes ?? null,
        runMinutesPerUnit: s.runMinutesPerUnit ?? null,
        notes: s.notes ?? null,
      },
    }));
    const modelEdges: Edge[] = routing.dependencies.map((d) => ({
      id: `e-${d.id}`,
      source: key(d.predecessorStepId),
      sourcePort: 'out',
      target: key(d.successorStepId),
      targetPort: 'in',
      data: {},
    }));
    this.model.updateNodes(nodes);
    this.model.updateEdges(modelEdges);
    this.selectedId.set(null);
    this.dirty.set(false);
    this.saveError.set(null);
    this.validationErrors.set([]);
    this.version.update((v) => v + 1);
  }

  protected onInit(): void {
    void this.viewport.zoomToFit();
  }

  protected touch(): void {
    this.dirty.set(true);
    this.version.update((v) => v + 1);
  }

  protected onRemoved(): void {
    this.selectedId.set(null);
    this.touch();
  }

  protected onSelection(e: SelectionChangedEvent): void {
    this.selectedId.set(e.selectedNodes.length === 1 ? e.selectedNodes[0].id : null);
  }

  protected num(e: Event): number | null {
    return toNumberOrNull((e.target as HTMLInputElement).value);
  }

  protected text(e: Event): string | null {
    const v = (e.target as HTMLTextAreaElement).value;
    return v === '' ? null : v;
  }

  protected patch(id: string, change: Partial<StepNodeData>): void {
    const node = this.steps().find((n) => n.id === id);
    if (!node) return;
    this.modelService.updateNodeData(id, { ...node.data, ...change });
    this.touch();
  }

  protected addStep(seg: ProcessSegmentModel): void {
    const nodes = this.model.getNodes() as StepNode[];
    const maxOp = Math.max(0, ...nodes.map((n) => n.data.operationNumber ?? 0));
    const operationNumber = (Math.floor(maxOp / 10) + 1) * 10;
    const maxY = nodes.length ? Math.max(...nodes.map((n) => n.position.y)) : -ROW_SPACING + 60;
    const id = `new-${++this.newCounter}`;
    this.modelService.addNodes([
      {
        id,
        type: 'step',
        position: { x: 60, y: maxY + ROW_SPACING },
        data: {
          stepId: null,
          processSegmentId: seg.id,
          code: seg.code,
          name: seg.name,
          standardSetup: seg.setupMinutes,
          standardRun: seg.runMinutesPerUnit,
          operationNumber,
          setupMinutes: null,
          runMinutesPerUnit: null,
          notes: null,
        },
      } satisfies StepNode,
    ]);
    this.touch();
    void this.selection.select([id]);
  }

  protected discard(): void {
    if (this.serverRouting) this.applyRouting(this.serverRouting);
  }

  protected async save(): Promise<void> {
    const nodes = this.model.getNodes() as StepNode[];
    const edges = this.model.getEdges();
    const errors = validateRouting(
      nodes.map((n) => ({ id: n.id, operationNumber: n.data.operationNumber, label: `${n.data.operationNumber ?? '—'} ${n.data.code}` })),
      edges,
    );
    this.validationErrors.set(errors);
    this.saveError.set(null);
    if (errors.length) return;

    const request: UpdateRoutingRequest = {
      steps: nodes.map((n) => ({
        key: n.id,
        id: n.data.stepId,
        processSegmentId: n.data.processSegmentId,
        operationNumber: n.data.operationNumber,
        setupMinutes: n.data.setupMinutes,
        runMinutesPerUnit: n.data.runMinutesPerUnit,
        notes: n.data.notes,
        positionX: Math.round(n.position.x),
        positionY: Math.round(n.position.y),
      })),
      dependencies: edges.map((e) => ({ predecessorKey: e.source, successorKey: e.target })),
    };
    this.saving.set(true);
    try {
      const saved = await firstValueFrom(this.routingApi.updateRouting(this.materialId(), request));
      this.applyRouting(saved);
    } catch (e) {
      const f = toFailure(e);
      const fields = Object.values(f.fieldErrors);
      this.saveError.set(fields.length ? `${f.message} ${fields.join(' ')}` : f.message);
    } finally {
      this.saving.set(false);
    }
  }
}
