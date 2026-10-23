import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Injector, afterNextRender, inject, ViewEncapsulation } from '@angular/core';
import {
  initializeModel,
  NgDiagramBackgroundComponent,
  NgDiagramComponent,
  NgDiagramEdgeTemplateMap,
  NgDiagramNodeTemplateMap,
  NgDiagramViewportService,
  provideNgDiagram,
} from 'ng-diagram';
import { FLOW_EDGE_TYPE, FLOW_EDGES, FLOW_GROUP_TYPE, FLOW_NODES, FLOW_WORK_TYPE } from './flow-data';
import { FlowEdgeComponent } from './flow-edge.component';
import { FlowGroupComponent } from './flow-group.component';
import { FlowNodeComponent } from './flow-node.component';

/**
 * Diagramma statico e in sola lettura. Caricato solo nel browser (dentro `@defer`).
 * - Nessun trascinamento né pan: la rotellina e il tocco scorrono la pagina (l'evento wheel non arriva a ng-diagram).
 * - Il contenitore ha larghezza minima fissa: su schermi stretti scorre in orizzontale invece di rimpicciolirsi.
 */
@Component({
  selector: 'app-flow-diagram',
  imports: [NgDiagramComponent, NgDiagramBackgroundComponent],
  providers: [provideNgDiagram()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './flow-diagram.component.scss',
  encapsulation: ViewEncapsulation.None,
  template: `
    <ng-diagram
      [model]="model"
      [nodeTemplateMap]="nodeTemplateMap"
      [edgeTemplateMap]="edgeTemplateMap"
      [config]="config"
      (diagramInit)="fit()"
    >
      <ng-diagram-background />
    </ng-diagram>
  `,
})
export class FlowDiagramComponent {
  private readonly viewport = inject(NgDiagramViewportService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly model = initializeModel({ nodes: FLOW_NODES, edges: FLOW_EDGES }, inject(Injector));
  protected readonly nodeTemplateMap = new NgDiagramNodeTemplateMap([
    [FLOW_WORK_TYPE, FlowNodeComponent],
    [FLOW_GROUP_TYPE, FlowGroupComponent],
  ]);
  protected readonly edgeTemplateMap = new NgDiagramEdgeTemplateMap([[FLOW_EDGE_TYPE, FlowEdgeComponent]]);
  protected readonly config = {
    nodeDraggingEnabled: false,
    viewportPanningEnabled: false,
    hideWatermark: true,
    resize: { defaultResizable: false },
    nodeRotation: { defaultRotatable: false },
    zoom: { min: 0.5, max: 1 },
  };

  constructor() {
    afterNextRender(() => {
      const el = this.host.nativeElement;
      const stop = (e: Event) => e.stopPropagation();
      el.addEventListener('wheel', stop, { capture: true, passive: true });
      this.destroyRef.onDestroy(() => el.removeEventListener('wheel', stop, { capture: true }));
    });
  }

  protected async fit(): Promise<void> {
    await this.viewport.zoomToFit({ padding: 24 });
  }
}
