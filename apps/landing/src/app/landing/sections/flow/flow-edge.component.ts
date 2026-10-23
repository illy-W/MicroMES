import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgDiagramBaseEdgeComponent, type Edge, type NgDiagramEdgeTemplate } from 'ng-diagram';

/** Arco: più spesso e blu segnale sul percorso più lungo, tratteggiato se attraversa il bordo di un ordine figlio. */
@Component({
  selector: 'app-flow-edge',
  imports: [NgDiagramBaseEdgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ng-diagram-base-edge
    [edge]="edge()"
    [stroke]="data().critical ? 'var(--signal)' : undefined"
    [strokeWidth]="data().critical ? 3 : undefined"
    [strokeDasharray]="data().crossOrder ? '6 4' : undefined"
  />`,
})
export class FlowEdgeComponent implements NgDiagramEdgeTemplate {
  readonly edge = input.required<Edge>();
  protected readonly data = computed(() => this.edge().data as { critical: boolean; crossOrder: boolean });
}
