import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgDiagramPortComponent, type NgDiagramNodeTemplate, type Node } from 'ng-diagram';
import { HlmBadge } from '@spartan-ng/helm/badge';

export interface StepNodeData {
  stepId: number | null;
  processSegmentId: number;
  code: string;
  name: string;
  standardSetup: number;
  standardRun: number;
  operationNumber: number | null;
  setupMinutes: number | null;
  runMinutesPerUnit: number | null;
  notes: string | null;
  [key: string]: unknown;
}

const fmt = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 2 });

/**
 * Nodo del diagramma (custom: ng-diagram non ha nodi stilati). Disegna la propria card con i token semantici,
 * mostra numero operazione, fase e tempi effettivi; porte a sinistra (target) e a destra (source).
 */
@Component({
  selector: 'app-routing-node',
  imports: [NgDiagramPortComponent, HlmBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="bg-card text-card-foreground flex w-56 flex-col gap-1.5 rounded-lg border p-3 shadow-sm"
      [class.ring-ring]="node().selected"
      [class.ring-2]="node().selected"
      [class.border-primary]="node().selected"
    >
      <div class="flex items-center justify-between gap-2">
        <span hlmBadge variant="secondary" class="tabular-nums">Op. {{ node().data.operationNumber ?? '—' }}</span>
        <span class="text-muted-foreground truncate text-xs font-medium">{{ node().data.code }}</span>
      </div>
      <span class="truncate text-sm font-medium">{{ node().data.name }}</span>
      <span class="text-muted-foreground flex items-center gap-1 text-xs tabular-nums">
        {{ fmt(setup()) }} + {{ fmt(run()) }} min/u
        @if (overridden()) {
          <span hlmBadge variant="outline" class="px-1 py-0 text-[10px]">modificato</span>
        }
      </span>
    </div>
    <ng-diagram-port id="in" side="left" type="target" />
    <ng-diagram-port id="out" side="right" type="source" />
  `,
})
export class RoutingNodeComponent implements NgDiagramNodeTemplate<StepNodeData> {
  readonly node = input.required<Node<StepNodeData>>();

  protected setup(): number {
    const d = this.node().data;
    return d.setupMinutes ?? d.standardSetup;
  }
  protected run(): number {
    const d = this.node().data;
    return d.runMinutesPerUnit ?? d.standardRun;
  }
  protected overridden(): boolean {
    const d = this.node().data;
    return d.setupMinutes !== null || d.runMinutesPerUnit !== null;
  }
  protected fmt(n: number): string {
    return fmt.format(n);
  }
}
