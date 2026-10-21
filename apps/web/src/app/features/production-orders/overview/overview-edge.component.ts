import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NgDiagramBaseEdgeComponent, type Edge, type NgDiagramEdgeTemplate } from 'ng-diagram';
import type { OverviewEdgeData } from './overview-model';
import { OverviewViewState } from './overview-view-state';

/**
 * Arco della panoramica: tratteggiato se attraversa il bordo di un ordine figlio,
 * con il token `primary` e più spesso se sta sul percorso più lungo (quando l'evidenziazione è attiva).
 */
@Component({
  selector: 'app-overview-edge',
  imports: [NgDiagramBaseEdgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ng-diagram-base-edge
    [edge]="edge()"
    [stroke]="critical() ? 'var(--primary)' : undefined"
    [strokeWidth]="critical() ? 3 : undefined"
    [strokeDasharray]="data().crossOrder ? '6 4' : undefined"
  />`,
})
export class OverviewEdgeComponent implements NgDiagramEdgeTemplate {
  private readonly view = inject(OverviewViewState);
  readonly edge = input.required<Edge>();
  protected readonly data = computed(() => this.edge().data as OverviewEdgeData);
  protected readonly critical = computed(() => this.view.showCritical() && this.data().critical);
}
