import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, HostListener, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideListOrdered, lucidePencil, lucidePlus, lucideTriangleAlert, lucideWorkflow } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import type {
  MaterialModel,
  ProcessSegmentModel,
  RoutingModel,
  UpdateRoutingRequest,
} from '@micromes/api-client/models';
import { MaterialsService } from '@micromes/api-client/services/materials';
import { ProcessSegmentsService } from '@micromes/api-client/services/process-segments';
import { RoutingService } from '@micromes/api-client/services/routing';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import {
  initializeModel,
  NgDiagramBackgroundComponent,
  NgDiagramComponent,
  NgDiagramEdgeTemplateMap,
  NgDiagramMinimapComponent,
  NgDiagramModelService,
  NgDiagramNodeTemplateMap,
  NgDiagramSelectionService,
  NgDiagramService,
  NgDiagramViewportService,
  provideNgDiagram,
  type Edge,
  type Middleware,
  type Node,
  type SelectionChangedEvent,
} from 'ng-diagram';
import { firstValueFrom, map } from 'rxjs';
import { unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { RoutingEdgeComponent } from './routing-edge.component';
import { layeredLayout, ROW_SPACING } from './routing-layout';
import { computeMetrics, formatMinutes } from './routing-metrics';
import { RoutingNodeComponent, type StepNodeData } from './routing-node.component';
import { numbersOutOfFlow, proposeNumber, renumber } from './routing-numbering';
import { RoutingViewState } from './routing-view-state';
import { validateRouting } from './routing-validation';

type StepNode = Node<StepNodeData>;

/** Azioni del diagramma che in lettura vengono bloccate dal middleware (modifiche a nodi, archi e posizioni). */
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
  'paste',
  'paletteDropNode',
]);

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
    HlmAlertImports,
    HlmAlertDialogImports,
    HlmBadge,
    HlmButton,
    HlmCardImports,
    HlmEmptyImports,
    HlmInput,
    HlmLabel,
    HlmSkeleton,
    HlmTableImports,
    HlmTabsImports,
    HlmTextarea,
    NgIcon,
  ],
  providers: [
    provideNgDiagram(),
    RoutingViewState,
    provideIcons({ lucideListOrdered, lucidePencil, lucidePlus, lucideTriangleAlert, lucideWorkflow }),
  ],
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
          <p class="text-muted-foreground text-sm">
            @if (editing()) {
              Passi di produzione e dipendenze: collega le porte da destra a sinistra.
            } @else {
              Passi di produzione e dipendenze, in sola lettura.
            }
          </p>
        </div>
        <div class="flex items-center gap-2">
          @if (editing()) {
            @if (dirty()) {
              <span hlmBadge variant="outline" role="status">Modifiche non salvate</span>
            }
            <button hlmBtn variant="outline" size="sm" type="button" [disabled]="saving()" (click)="cancelEdit()">Annulla</button>
            <button hlmBtn size="sm" type="button" [disabled]="!dirty() || saving() || loading()" (click)="save()">Salva</button>
          } @else {
            <button hlmBtn size="sm" type="button" [disabled]="loading() || !!loadError()" (click)="startEdit()">
              <ng-icon name="lucidePencil" /> Modifica ciclo
            </button>
          }
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
          <div hlmAlert variant="destructive">
            <ng-icon name="lucideTriangleAlert" aria-hidden="true" />
            <div hlmAlertTitle>Salvataggio non riuscito</div>
            <div hlmAlertDescription>{{ saveError() }}</div>
          </div>
        }
        @if (editing() && validationErrors().length) {
          <div hlmAlert variant="destructive">
            <ng-icon name="lucideTriangleAlert" aria-hidden="true" />
            <div hlmAlertTitle>Correggi prima di salvare</div>
            <div hlmAlertDescription>
              <ul class="list-disc pl-5">
                @for (e of validationErrors(); track e) {
                  <li>{{ e }}</li>
                }
              </ul>
            </div>
          </div>
        }
        @if (editing() && outOfFlow()) {
          <div hlmAlert>
            <ng-icon name="lucideListOrdered" aria-hidden="true" />
            <div hlmAlertTitle>I numeri non seguono il flusso</div>
            <div hlmAlertDescription>Un passo ha un numero uguale o maggiore di quello di un suo successore. Il salvataggio resta possibile.</div>
            <div hlmAlertAction>
              <button hlmBtn variant="outline" size="sm" type="button" (click)="renumberSteps()">Rinumera</button>
            </div>
          </div>
        }

        <div class="bg-card flex flex-wrap items-center justify-between gap-x-6 gap-y-2 rounded-lg border px-4 py-3" role="group" aria-label="Riepilogo del ciclo">
          <dl class="flex flex-wrap items-baseline gap-x-6 gap-y-1 text-sm">
            <div class="flex items-baseline gap-1.5">
              <dt class="text-muted-foreground">Passi</dt>
              <dd class="font-semibold tabular-nums">{{ metrics().stepCount }}</dd>
            </div>
            <div class="flex items-baseline gap-1.5">
              <dt class="text-muted-foreground">Contenuto di lavoro</dt>
              <dd class="font-semibold tabular-nums">{{ fmtMinutes(metrics().workContent) }}</dd>
            </div>
            <div class="flex items-baseline gap-1.5">
              <dt class="text-muted-foreground">Percorso più lungo</dt>
              <dd class="font-semibold tabular-nums">{{ fmtMinutes(metrics().longestPath) }}</dd>
            </div>
          </dl>
          <div class="flex items-center gap-4">
            @if (!editing() && metrics().criticalNodeIds.size) {
              <span class="text-muted-foreground flex items-center gap-1.5 text-xs">
                <span class="border-primary bg-accent inline-block size-3 rounded-sm border-2" aria-hidden="true"></span>
                Percorso più lungo
              </span>
            }
            @if (editing()) {
              <button hlmBtn variant="outline" size="sm" type="button" [disabled]="!steps().length" (click)="renumberSteps()">
                <ng-icon name="lucideListOrdered" /> Rinumera
              </button>
            }
          </div>
        </div>
        <p class="text-muted-foreground -mt-2 text-xs">Tempi per 1 unità: attrezzaggio più lavorazione, valori effettivi.</p>

        <hlm-tabs [tab]="view()" (tabActivated)="view.set($any($event))">
          <hlm-tabs-list aria-label="Vista del ciclo">
            <button hlmTabsTrigger="diagram" type="button">Diagramma</button>
            <button hlmTabsTrigger="list" type="button">Elenco</button>
          </hlm-tabs-list>
        </hlm-tabs>

        <div class="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
            <div class="relative min-w-0">
              <!--
                Il diagramma resta sempre montato: ng-diagram misura nodi e porte solo alla creazione, quindi
                ricrearlo a ogni cambio di tab lascia archi e selezione non disegnati. Da inattivo lo si
                nasconde senza display:none (che romperebbe le misure) e lo si rende inerte.
              -->
              <div
                role="tabpanel"
                aria-label="Diagramma"
                [class]="view() === 'diagram' ? '' : 'pointer-events-none invisible absolute inset-x-0 top-0'"
                [attr.aria-hidden]="view() === 'diagram' ? null : 'true'"
                [attr.inert]="view() === 'diagram' ? null : ''"
              >
                  <div class="relative h-[calc(100vh-22rem)] min-h-96 overflow-hidden rounded-md border">
                    <ng-diagram
                      [model]="model"
                      [nodeTemplateMap]="nodeTemplateMap"
                      [edgeTemplateMap]="edgeTemplateMap"
                      [config]="config"
                      (diagramInit)="onInit()"
                      (edgeDrawn)="touch()"
                      (selectionMoved)="touch()"
                      (selectionRemoved)="onRemoved()"
                      (selectionChanged)="onSelection($event)"
                    >
                      <ng-diagram-background />
                    </ng-diagram>
                    @if (showMinimap()) {
                      <ng-diagram-minimap position="bottom-right" />
                    }
                    @if (!steps().length) {
                      <div class="absolute inset-0 flex items-center justify-center" [class.pointer-events-none]="editing()">
                        <div class="bg-card flex max-w-sm flex-col items-center gap-2 rounded-lg border p-6 text-center shadow-sm">
                          <ng-icon name="lucideWorkflow" size="24" aria-hidden="true" />
                          <p class="font-medium">Nessun passo nel ciclo</p>
                          @if (editing()) {
                            <p class="text-muted-foreground text-sm">Aggiungi la prima fase dall'elenco a destra.</p>
                          } @else {
                            <p class="text-muted-foreground text-sm">Questo materiale non ha ancora un ciclo di lavoro.</p>
                            <button hlmBtn size="sm" type="button" (click)="startEdit()">Crea il ciclo</button>
                          }
                        </div>
                      </div>
                    }
                  </div>
              </div>
              <div role="tabpanel" aria-label="Elenco" [hidden]="view() !== 'list'">
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
              </div>
            </div>

            <div class="flex flex-col gap-4">
              @if (selected(); as s) {
                @if (editing()) {
                  <section hlmCard aria-labelledby="step-panel-title">
                    <div hlmCardHeader>
                      <h2 hlmCardTitle id="step-panel-title">Passo {{ s.data.code }}</h2>
                    </div>
                    <div hlmCardContent class="flex flex-col gap-3">
                      <div class="flex flex-col gap-1.5">
                        <span hlmLabel id="st-op-label">Numero operazione</span>
                        <p class="text-sm tabular-nums" aria-labelledby="st-op-label">{{ s.data.operationNumber ?? '—' }}</p>
                        <p class="text-muted-foreground text-xs">Assegnato dal sistema. Usa "Rinumera" per riallinearlo al flusso.</p>
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
                      <div class="flex flex-col gap-1.5">
                        <h3 hlmLabel>Componenti consumati</h3>
                        @for (c of s.data.consumedComponents; track c.bomItemId) {
                          <p class="flex flex-wrap items-baseline gap-x-2 text-sm">
                            <span class="font-medium">{{ c.materialCode }}</span>
                            <span class="text-muted-foreground">{{ c.materialName }}</span>
                            <span class="tabular-nums">{{ c.quantity }} {{ unit(c.unitOfMeasure) }}</span>
                            @if (c.isAutomatic) {
                              <span class="text-muted-foreground text-xs">automatico</span>
                            }
                          </p>
                        } @empty {
                          <p class="text-muted-foreground text-sm">Nessun componente.</p>
                        }
                        <a hlmBtn variant="link" size="sm" class="h-auto w-fit p-0" [routerLink]="['/materials', materialId(), 'bom']">
                          Apri la distinta base
                        </a>
                        <p class="text-muted-foreground text-xs">Il collegamento si modifica dalla distinta base.</p>
                      </div>
                    </div>
                  </section>
                } @else {
                  <section hlmCard aria-labelledby="step-detail-title">
                    <div hlmCardHeader>
                      <h2 hlmCardTitle id="step-detail-title">Passo {{ s.data.code }}</h2>
                      <p hlmCardDescription>{{ s.data.name }}</p>
                    </div>
                    <div hlmCardContent>
                      <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
                        <dt class="text-muted-foreground">Numero operazione</dt>
                        <dd class="tabular-nums">{{ s.data.operationNumber ?? '—' }}</dd>
                        <dt class="text-muted-foreground">Attrezzaggio</dt>
                        <dd class="tabular-nums">
                          {{ fmtMinutes(s.data.setupMinutes ?? s.data.standardSetup) }}
                          <span class="text-muted-foreground block text-xs">Standard: {{ fmtMinutes(s.data.standardSetup) }}</span>
                        </dd>
                        <dt class="text-muted-foreground">Tempo per unità</dt>
                        <dd class="tabular-nums">
                          {{ fmtMinutes(s.data.runMinutesPerUnit ?? s.data.standardRun) }}
                          <span class="text-muted-foreground block text-xs">Standard: {{ fmtMinutes(s.data.standardRun) }}</span>
                        </dd>
                        <dt class="text-muted-foreground">Note</dt>
                        <dd class="whitespace-pre-wrap">{{ s.data.notes || '—' }}</dd>
                      </dl>
                      <div class="mt-4">
                      <div class="flex flex-col gap-1.5">
                        <h3 class="text-muted-foreground text-sm">Componenti consumati</h3>
                        @for (c of s.data.consumedComponents; track c.bomItemId) {
                          <p class="flex flex-wrap items-baseline gap-x-2 text-sm">
                            <span class="font-medium">{{ c.materialCode }}</span>
                            <span class="text-muted-foreground">{{ c.materialName }}</span>
                            <span class="tabular-nums">{{ c.quantity }} {{ unit(c.unitOfMeasure) }}</span>
                            @if (c.isAutomatic) {
                              <span class="text-muted-foreground text-xs">automatico</span>
                            }
                          </p>
                        } @empty {
                          <p class="text-muted-foreground text-sm">Nessun componente.</p>
                        }
                        <a hlmBtn variant="link" size="sm" class="h-auto w-fit p-0" [routerLink]="['/materials', materialId(), 'bom']">
                          Apri la distinta base
                        </a>
                        <p class="text-muted-foreground text-xs">Il collegamento si modifica dalla distinta base.</p>
                      </div>
                      </div>
                    </div>
                  </section>
                }
              } @else if (!editing()) {
                <p class="text-muted-foreground text-sm">Seleziona un passo per vederne i dettagli.</p>
              }

              @if (editing()) {
                <section hlmCard aria-labelledby="palette-title">
                  <div hlmCardHeader>
                    <h2 hlmCardTitle id="palette-title">Fasi disponibili</h2>
                  </div>
                  <div hlmCardContent>
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
                  </div>
                </section>
              }
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

    @if (deleteConfirm(); as dc) {
      <hlm-alert-dialog state="open" (closed)="deleteConfirm.set(null)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Eliminare passi con componenti collegati?</h2>
              <p hlmAlertDialogDescription>
                I componenti {{ dc.names }} tornano al primo passo.
              </p>
            </hlm-alert-dialog-header>
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Annulla</button>
              <button hlmBtn type="button" (click)="confirmDelete()">Salva comunque</button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }

    @if (discardOpen()) {
      <hlm-alert-dialog state="open" (closed)="discardOpen.set(false)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Scartare le modifiche?</h2>
              <p hlmAlertDialogDescription>Le modifiche al ciclo di lavoro non salvate andranno perse.</p>
            </hlm-alert-dialog-header>
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Continua a modificare</button>
              <button hlmBtn type="button" variant="destructive" (click)="confirmDiscard()">Scarta modifiche</button>
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
  private readonly diagramService = inject(NgDiagramService);
  private readonly viewState = inject(RoutingViewState);

  private readonly materialIdParam = toSignal(
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
  /** Lettura di default; la modifica si attiva esplicitamente. */
  protected readonly editing = signal(false);
  /** Oltre questo numero di passi compare la minimappa. */
  protected readonly showMinimap = computed(() => this.steps().length > 8);
  protected readonly discardOpen = signal(false);
  protected readonly deleteConfirm = signal<{ names: string; request: UpdateRoutingRequest } | null>(null);
  protected readonly materialId = computed(() => this.materialIdParam());
  protected readonly unit = unitLabel;
  protected readonly leaveResolver = signal<((v: boolean) => void) | null>(null);

  private readonly version = signal(0);
  private readonly selectedId = signal<string | null>(null);
  private serverRouting: RoutingModel | null = null;
  private unregisterGate: (() => void) | null = null;
  private newCounter = 0;

  protected readonly nodeTemplateMap = new NgDiagramNodeTemplateMap([['step', RoutingNodeComponent]]);
  protected readonly edgeTemplateMap = new NgDiagramEdgeTemplateMap([['dep', RoutingEdgeComponent]]);
  protected readonly model = initializeModel({ nodes: [], edges: [] });

  /** Middleware che in lettura blocca cancellazioni, collegamenti e spostamenti, senza toccare il modello. */
  private readonly readOnlyGate: Middleware = {
    name: 'routing-read-only',
    execute: (context, next, cancel) => {
      if (!this.editing() && context.modelActionTypes.some((a) => READ_ONLY_BLOCKED.has(a))) {
        cancel();
        return;
      }
      void next();
    },
  };

  /** Impedisce archi che chiudono un ciclo, anelli e duplicati già durante il collegamento. */
  protected readonly config = {
    linking: {
      validateConnection: (source: Node | null, _sp: unknown, target: Node | null): boolean => {
        if (!this.editing() || !source || !target || source.id === target.id) return false;
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

  protected readonly metrics = computed(() => {
    this.version();
    const nodes = this.model.getNodes() as StepNode[];
    return computeMetrics(
      nodes.map((n) => ({
        id: n.id,
        setup: n.data.setupMinutes ?? n.data.standardSetup,
        run: n.data.runMinutesPerUnit ?? n.data.standardRun,
      })),
      this.model.getEdges(),
    );
  });

  protected readonly outOfFlow = computed(() => {
    this.version();
    const nodes = this.model.getNodes() as StepNode[];
    return numbersOutOfFlow(
      nodes.map((n) => ({ id: n.id, operationNumber: n.data.operationNumber })),
      this.model.getEdges(),
    );
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
    inject(DestroyRef).onDestroy(() => {
      this.unregisterGate?.();
      this.unregisterGate = null;
    });
    effect(() => {
      const id = this.materialIdParam();
      untracked(() => {
        if (id) void this.load();
      });
    });
    // I template di nodo e arco leggono lo stato di vista da qui.
    effect(() => {
      this.viewState.editing.set(this.editing());
      const m = this.metrics();
      this.viewState.criticalNodeIds.set(m.criticalNodeIds);
      this.viewState.criticalEdgeKeys.set(m.criticalEdgeKeys);
    });
    // Trascinamento dei nodi attivo solo in modifica.
    effect(() => {
      const edit = this.editing();
      if (this.diagramService.isInitialized()) {
        untracked(() => this.diagramService.updateConfig({ nodeDraggingEnabled: edit }));
      }
    });
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(e: BeforeUnloadEvent): void {
    if (this.editing() && this.dirty()) e.preventDefault();
  }

  confirmLeave(): Promise<boolean> {
    if (!this.editing() || !this.dirty()) return Promise.resolve(true);
    return new Promise((resolve) => this.leaveResolver.set(resolve));
  }

  protected resolveLeave(value: boolean): void {
    const r = this.leaveResolver();
    this.leaveResolver.set(null);
    r?.(value);
  }

  async load(): Promise<void> {
    const id = this.materialIdParam();
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
        consumedComponents: s.consumedComponents ?? [],
      },
    }));
    const modelEdges: Edge[] = routing.dependencies.map((d) => ({
      id: `e-${d.id}`,
      type: 'dep',
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
    // Il flow core è nuovo a ogni ricreazione del diagramma: si registra una sola volta per istanza.
    this.diagramService.unregisterMiddleware(this.readOnlyGate.name);
    this.unregisterGate = this.diagramService.registerMiddleware(this.readOnlyGate) as (() => void) | null;
    this.diagramService.updateConfig({ nodeDraggingEnabled: this.editing() });
    void this.fitView();
  }

  /** Adatta la vista con margine, senza ingrandire oltre 1:1 (pochi passi non devono diventare enormi). */
  private async fitView(): Promise<void> {
    await this.viewport.zoomToFit({ padding: 60 });
    const scale = this.viewport.scale();
    if (scale > 1) await this.viewport.zoom(1 / scale);
  }

  protected fmtMinutes(v: number | null): string {
    return formatMinutes(v);
  }

  protected startEdit(): void {
    this.editing.set(true);
    this.view.set('diagram');
  }

  protected cancelEdit(): void {
    if (this.dirty()) {
      this.discardOpen.set(true);
      return;
    }
    this.exitEdit();
  }

  protected confirmDiscard(): void {
    this.discardOpen.set(false);
    this.exitEdit();
  }

  /** Torna in lettura scartando la bozza (ricostruisce il modello dal ciclo salvato). */
  private exitEdit(): void {
    if (this.serverRouting) this.applyRouting(this.serverRouting);
    this.editing.set(false);
  }

  protected async renumberSteps(): Promise<void> {
    const nodes = this.model.getNodes() as StepNode[];
    const numbers = renumber(
      nodes.map((n) => ({ id: n.id, operationNumber: n.data.operationNumber, positionY: n.position.y })),
      this.model.getEdges(),
    );
    await Promise.all(
      nodes.map((n) => this.modelService.updateNodeData(n.id, { ...n.data, operationNumber: numbers.get(n.id) ?? n.data.operationNumber })),
    );
    this.touch();
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
    const operationNumber = proposeNumber(nodes.map((n) => n.data.operationNumber));
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
          consumedComponents: [],
        },
      } satisfies StepNode,
    ]);
    this.touch();
    void this.selection.select([id]);
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
    const keptIds = new Set(request.steps.map((st) => st.id).filter((i): i is number => i != null));
    const explicit = (this.serverRouting?.steps ?? [])
      .filter((st) => !keptIds.has(st.id))
      .flatMap((st) => (st.consumedComponents ?? []).filter((c) => !c.isAutomatic));
    if (explicit.length) {
      this.deleteConfirm.set({ names: explicit.map((c) => c.materialName).join(', '), request });
      return;
    }
    await this.send(request);
  }

  protected async confirmDelete(): Promise<void> {
    const dc = this.deleteConfirm();
    this.deleteConfirm.set(null);
    if (dc) await this.send(dc.request);
  }

  private async send(request: UpdateRoutingRequest): Promise<void> {
    this.saving.set(true);
    try {
      const saved = await firstValueFrom(this.routingApi.updateRouting(this.materialIdParam(), request));
      this.applyRouting(saved);
      this.editing.set(false);
      const un = saved.unassignedComponents ?? [];
      if (un.length) {
        toast.info(`Ciclo salvato. I componenti ${un.map((c) => c.materialName).join(', ')} tornano al primo passo.`);
      } else {
        toast.success('Ciclo salvato');
      }
      // ricarica silenziosa (senza skeleton: il diagramma resta montato) per aggiornare i conteggi dei componenti
      try {
        this.applyRouting(await firstValueFrom(this.routingApi.getRouting(this.materialIdParam())));
      } catch {
        // la risposta del PUT è già applicata
      }
    } catch (e) {
      const f = toFailure(e);
      const fields = Object.values(f.fieldErrors);
      this.saveError.set(fields.length ? `${f.message} ${fields.join(' ')}` : f.message);
    } finally {
      this.saving.set(false);
    }
  }
}
