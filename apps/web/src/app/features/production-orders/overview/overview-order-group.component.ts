import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarClock,
  lucideCircleCheck,
  lucideCircleDot,
  lucideCircleOff,
  lucideClock,
  lucideOctagonX,
  lucidePause,
  lucidePlay,
  lucideSend,
} from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmProgressImports } from '@spartan-ng/helm/progress';
import { NgDiagramSelectionService, type GroupNode, type NgDiagramGroupNodeTemplate } from 'ng-diagram';
import { unitLabel } from '../../materials/material-labels';
import { formatQty } from '../production-order-labels';
import { orderStatusStyle, type OrderGroupData } from './overview-model';

/**
 * Gruppo di un ordine figlio: riquadro tratteggiato (distinto dalle card dei lavori) con intestazione
 * (codice, materiale, quantità, stato, avanzamento). Dimensione e posizione vengono dal layout: nessun
 * adornment di ridimensionamento, nessun trascinamento (sola lettura).
 */
@Component({
  selector: 'app-overview-order-group',
  imports: [NgIcon, HlmBadge, HlmProgressImports],
  providers: [
    provideIcons({
      lucideCalendarClock,
      lucideCircleCheck,
      lucideCircleDot,
      lucideCircleOff,
      lucideClock,
      lucideOctagonX,
      lucidePause,
      lucidePlay,
      lucideSend,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }
  `,
  template: `
    <div
      class="bg-muted/40 h-full w-full rounded-lg border-2 border-dashed"
      [class.border-ring]="node().selected"
    >
      <div
        role="button"
        tabindex="0"
        class="focus-visible:ring-ring focus-visible:ring-offset-background flex cursor-pointer flex-col gap-1 rounded-t-lg px-3 pt-2 pb-1 focus-visible:ring-2 focus-visible:outline-none"
        [attr.aria-label]="ariaLabel()"
        [attr.aria-pressed]="node().selected ? 'true' : 'false'"
        (keydown.enter)="select($event)"
        (keydown.space)="select($event)"
      >
        <div class="flex items-center justify-between gap-2">
          <span class="truncate text-sm font-semibold">{{ node().data.code }}</span>
          <span hlmBadge variant="outline" class="shrink-0 px-1.5" [class]="style().tone">
            <ng-icon [name]="style().icon" aria-hidden="true" /> {{ style().label }}
          </span>
        </div>
        <p class="text-muted-foreground truncate text-xs">
          <span class="text-foreground font-medium">{{ node().data.materialCode }}</span>
          {{ node().data.materialName }} · {{ qty(node().data.quantity) }} {{ unit(node().data.unitOfMeasure) }}
        </p>
        <div class="flex items-center gap-2">
          <hlm-progress class="flex-1" [value]="node().data.progressPercent" aria-hidden="true">
            <hlm-progress-indicator />
          </hlm-progress>
          <span class="text-muted-foreground w-9 text-right text-xs tabular-nums">{{ node().data.progressPercent }}%</span>
        </div>
      </div>
    </div>
  `,
})
export class OverviewOrderGroupComponent implements NgDiagramGroupNodeTemplate<OrderGroupData> {
  private readonly selection = inject(NgDiagramSelectionService);

  readonly node = input.required<GroupNode<OrderGroupData>>();

  protected readonly qty = formatQty;
  protected readonly unit = unitLabel;
  protected readonly style = computed(() => orderStatusStyle(this.node().data.status));
  protected readonly ariaLabel = computed(() => {
    const d = this.node().data;
    return `Ordine figlio ${d.code}, materiale ${d.materialCode}, stato ${this.style().label}, avanzamento ${d.progressPercent}%`;
  });

  protected select(event: Event): void {
    event.preventDefault();
    void this.selection.select([this.node().id]);
  }
}
