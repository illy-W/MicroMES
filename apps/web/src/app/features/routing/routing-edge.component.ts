import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NgDiagramBaseEdgeComponent, type Edge, type NgDiagramEdgeTemplate } from 'ng-diagram';
import { edgeKey } from './routing-metrics';
import { RoutingViewState } from './routing-view-state';

/** Arco del diagramma: come quello di default, ma con il token `primary` se sta sul percorso più lungo (solo in lettura). */
@Component({
  selector: 'app-routing-edge',
  imports: [NgDiagramBaseEdgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ng-diagram-base-edge [edge]="edge()" [stroke]="stroke()" [strokeWidth]="strokeWidth()" />`,
})
export class RoutingEdgeComponent implements NgDiagramEdgeTemplate {
  private readonly view = inject(RoutingViewState);
  readonly edge = input.required<Edge>();

  private readonly critical = computed(
    () => !this.view.editing() && this.view.criticalEdgeKeys().has(edgeKey(this.edge().source, this.edge().target)),
  );
  protected readonly stroke = computed(() => (this.critical() ? 'var(--primary)' : undefined));
  protected readonly strokeWidth = computed(() => (this.critical() ? 3 : undefined));
}
