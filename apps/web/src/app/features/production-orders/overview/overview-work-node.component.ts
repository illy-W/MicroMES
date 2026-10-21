import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarClock,
  lucideCircleCheck,
  lucideCircleDot,
  lucideCircleOff,
  lucideClock,
  lucideCog,
  lucideOctagonX,
  lucidePause,
  lucidePlay,
  lucideSend,
  lucideTriangleAlert,
} from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmProgressImports } from '@spartan-ng/helm/progress';
import {
  NgDiagramPortComponent,
  NgDiagramSelectionService,
  type NgDiagramNodeTemplate,
  type Node,
} from 'ng-diagram';
import { formatMinutes } from '../../routing/routing-metrics';
import { formatQty } from '../production-order-labels';
import { workProgressPercent, workStatusStyle, type WorkNodeData } from './overview-model';
import { OverviewViewState } from './overview-view-state';

/**
 * Nodo-card di un lavoro (ng-diagram non ha nodi stilati). Struttura e altezza fisse: quando "Aggiorna"
 * cambia solo stati e quantità, la misura non cambia e non serve un nuovo layout.
 * Stato: icona + testo (non solo colore). In esecuzione: anello `primary`. Selezionato: contorno `ring`.
 * Percorso più lungo: bordo `primary` e sfondo `accent`. Focus da tastiera: anello `ring`.
 */
@Component({
  selector: 'app-overview-work-node',
  imports: [NgDiagramPortComponent, NgIcon, HlmBadge, HlmProgressImports],
  providers: [
    provideIcons({
      lucideCalendarClock,
      lucideCircleCheck,
      lucideCircleDot,
      lucideCircleOff,
      lucideClock,
      lucideCog,
      lucideOctagonX,
      lucidePause,
      lucidePlay,
      lucideSend,
      lucideTriangleAlert,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
  `,
  template: `
    <div
      role="button"
      tabindex="0"
      class="bg-card text-card-foreground focus-visible:ring-ring focus-visible:ring-offset-background flex w-72 cursor-pointer flex-col gap-1.5 rounded-lg border p-3 shadow-sm focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none"
      [class.ring-2]="running()"
      [class.ring-primary]="running()"
      [class.border-primary]="highlighted()"
      [class.bg-accent]="highlighted()"
      [class.outline-2]="node().selected"
      [class.outline-offset-2]="node().selected"
      [class.outline-ring]="node().selected"
      [attr.aria-label]="ariaLabel()"
      [attr.aria-pressed]="node().selected ? 'true' : 'false'"
      (keydown.enter)="select($event)"
      (keydown.space)="select($event)"
    >
      <div class="flex items-center justify-between gap-2">
        <span class="flex min-w-0 items-baseline gap-2 text-xs">
          <span class="font-semibold tabular-nums">Op. {{ node().data.operationNumber }}</span>
          <span class="text-muted-foreground truncate">{{ node().data.code }}</span>
          @if (planMarker(); as m) {
            <span class="text-destructive flex shrink-0 items-center gap-0.5 font-medium" [attr.title]="m">
              <ng-icon name="lucideTriangleAlert" size="12" aria-hidden="true" /> {{ m }}
            </span>
          }
        </span>
        <span hlmBadge variant="outline" class="shrink-0 px-1.5" [class]="style().tone">
          <ng-icon [name]="style().icon" aria-hidden="true" /> {{ style().label }}
        </span>
      </div>
      <span class="truncate text-sm font-medium">{{ node().data.phaseName }}</span>
      <hlm-progress [value]="percent()" aria-hidden="true"><hlm-progress-indicator /></hlm-progress>
      <p class="flex items-baseline justify-between gap-2 text-xs tabular-nums">
        <span>{{ qty(node().data.goodQuantity) }} / {{ qty(node().data.plannedQuantity) }} buoni</span>
        @if (node().data.scrapQuantity > 0) {
          <span class="text-destructive">scarto {{ qty(node().data.scrapQuantity) }}</span>
        }
      </p>
      <p class="text-muted-foreground flex items-baseline justify-between gap-2 text-xs tabular-nums">
        <span class="truncate">{{ minutes(node().data.workedMinutes) }} / {{ minutes(node().data.plannedMinutes) }}</span>
        @if (node().data.equipmentCode; as machine) {
          <span class="flex shrink-0 items-center gap-1" [attr.title]="machineTitle()">
            <ng-icon name="lucideCog" size="12" aria-hidden="true" />
            <span class="text-foreground font-medium">{{ machine }}</span>
          </span>
        }
      </p>
    </div>
    <ng-diagram-port id="in" side="left" type="target" class="pointer-events-none" />
    <ng-diagram-port id="out" side="right" type="source" class="pointer-events-none" />
  `,
})
export class OverviewWorkNodeComponent implements NgDiagramNodeTemplate<WorkNodeData> {
  private readonly view = inject(OverviewViewState);
  private readonly selection = inject(NgDiagramSelectionService);

  readonly node = input.required<Node<WorkNodeData>>();

  protected readonly qty = formatQty;
  protected readonly minutes = formatMinutes;
  protected readonly style = computed(() => workStatusStyle(this.node().data.status));
  protected readonly running = computed(() => this.node().data.status === 'Running');
  protected readonly highlighted = computed(() => this.view.showCritical() && this.node().data.critical);
  protected readonly percent = computed(() =>
    workProgressPercent(this.node().data.goodQuantity, this.node().data.plannedQuantity),
  );
  protected readonly ariaLabel = computed(() => {
    const d = this.node().data;
    const machine = d.equipmentCode ? `, ${d.equipmentInUse ? 'macchina in uso' : 'macchina assegnata'} ${d.equipmentCode}` : '';
    return `Lavoro ${d.code}, fase ${d.phaseName}, stato ${this.style().label}${this.planMarker() ? ', piano ' + this.planMarker() : ''}${machine}${this.highlighted() ? ', sul percorso più lungo' : ''}`;
  });
  protected readonly planMarker = computed(() => {
    const p = this.node().data.planStatus;
    return p === 'Late' ? 'ritardo' : p === 'Stale' ? 'obsoleto' : null;
  });
  protected readonly machineTitle = computed(() => {
    const d = this.node().data;
    return `${d.equipmentInUse ? 'Macchina in uso' : 'Macchina assegnata'}: ${d.equipmentCode} ${d.equipmentName ?? ''}`.trim();
  });

  protected select(event: Event): void {
    event.preventDefault();
    void this.selection.select([this.node().id]);
  }
}
