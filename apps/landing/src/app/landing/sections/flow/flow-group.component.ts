import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlay } from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import type { GroupNode, NgDiagramGroupNodeTemplate } from 'ng-diagram';
import type { FlowOrderData } from './flow-data';

/** Gruppo = ordine figlio (semilavorato): riquadro tratteggiato con intestazione. */
@Component({
  selector: 'app-flow-group',
  imports: [NgIcon, HlmBadge],
  providers: [provideIcons({ lucidePlay })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }
  `,
  template: `
    <div class="bg-muted/40 h-full w-full rounded-lg border-2 border-dashed">
      <div class="flex flex-col gap-1 px-3 pt-2 pb-1">
        <div class="flex items-center justify-between gap-2">
          <span class="truncate text-sm font-semibold">Ordine figlio {{ node().data.code }}</span>
          <span hlmBadge variant="outline" class="shrink-0 px-1.5">
            <ng-icon name="lucidePlay" aria-hidden="true" /> {{ node().data.status }}
          </span>
        </div>
        <p class="text-muted-foreground truncate text-xs">{{ node().data.material }} · {{ node().data.quantity }} pz</p>
        <div class="flex items-center gap-2" aria-hidden="true">
          <div class="bg-muted h-1.5 flex-1 overflow-hidden rounded-full">
            <div class="bg-primary h-full" [style.width.%]="node().data.progress"></div>
          </div>
          <span class="text-muted-foreground w-9 text-right text-xs tabular-nums">{{ node().data.progress }}%</span>
        </div>
      </div>
    </div>
  `,
})
export class FlowGroupComponent implements NgDiagramGroupNodeTemplate<FlowOrderData> {
  readonly node = input.required<GroupNode<FlowOrderData>>();
}
