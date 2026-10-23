import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleCheck, lucideCircleDot, lucideClock, lucideCog, lucidePlay } from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { NgDiagramPortComponent, type NgDiagramNodeTemplate, type Node } from 'ng-diagram';
import { FLOW_STATUS, type FlowWorkData } from './flow-data';

/** Nodo-card di un ordine di lavoro: stesso aspetto della panoramica di MicroMES. Sola lettura. */
@Component({
  selector: 'app-flow-node',
  imports: [NgDiagramPortComponent, NgIcon, HlmBadge],
  providers: [provideIcons({ lucideCircleCheck, lucideCircleDot, lucideClock, lucideCog, lucidePlay })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
  `,
  template: `
    <div
      class="bg-card text-card-foreground flex w-[250px] flex-col gap-1.5 rounded-lg border p-3 shadow-sm"
      [class.ring-2]="running()"
      [class.ring-signal]="running()"
      [class.border-signal]="node().data.critical"
      [class.bg-accent]="node().data.critical"
      role="group"
      [attr.aria-label]="label()"
    >
      <div class="flex items-center justify-between gap-2">
        <span class="flex min-w-0 items-baseline gap-2 text-xs">
          <span class="font-semibold tabular-nums">Op. {{ node().data.operation }}</span>
        </span>
        <span hlmBadge variant="outline" class="shrink-0 px-1.5" [class]="style().tone">
          <ng-icon [name]="style().icon" aria-hidden="true" /> {{ style().label }}
        </span>
      </div>
      <span class="truncate text-sm font-medium">{{ node().data.phase }}</span>
      <div class="bg-muted h-1.5 w-full overflow-hidden rounded-full" aria-hidden="true">
        <div class="bg-primary h-full" [style.width.%]="node().data.progress"></div>
      </div>
      <p class="flex items-baseline justify-between gap-2 text-xs tabular-nums">
        <span>{{ node().data.good }} / {{ node().data.planned }} buoni</span>
        <span class="flex shrink-0 items-center gap-1">
          <ng-icon name="lucideCog" size="12" aria-hidden="true" />
          <span class="font-medium">{{ node().data.machine }}</span>
        </span>
      </p>
    </div>
    <ng-diagram-port id="in" side="left" type="target" class="pointer-events-none" />
    <ng-diagram-port id="out" side="right" type="source" class="pointer-events-none" />
  `,
})
export class FlowNodeComponent implements NgDiagramNodeTemplate<FlowWorkData> {
  readonly node = input.required<Node<FlowWorkData>>();
  protected readonly style = computed(() => FLOW_STATUS[this.node().data.status]);
  protected readonly running = computed(() => this.node().data.status === 'Running');
  protected readonly label = computed(() => `${this.node().data.phase}, ${this.style().label}`);
}
