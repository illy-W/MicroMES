import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarCheck, lucideCalendarX, lucideClock, lucideTriangleAlert } from '@ng-icons/lucide';
import type { PlanStatus } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';

export const PLAN_STATUS: Record<PlanStatus, { label: string; icon: string; tone: string }> = {
  Planned: { label: 'Pianificato', icon: 'lucideCalendarCheck', tone: 'text-muted-foreground' },
  Late: { label: 'In ritardo', icon: 'lucideTriangleAlert', tone: 'text-destructive' },
  Stale: { label: 'Obsoleto', icon: 'lucideClock', tone: 'text-destructive' },
  Unplanned: { label: 'Non pianificato', icon: 'lucideCalendarX', tone: 'text-muted-foreground' },
};

/** Badge dello stato di piano (icona + testo). Non rende nulla per `Planned` se `hidePlanned` è attivo. */
@Component({
  selector: 'app-plan-status-badge',
  imports: [NgIcon, HlmBadge],
  providers: [provideIcons({ lucideCalendarCheck, lucideCalendarX, lucideClock, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible(); as s) {
      <span hlmBadge variant="outline" class="px-1.5" [class]="s.tone">
        <ng-icon [name]="s.icon" aria-hidden="true" /> {{ s.label }}
      </span>
    }
  `,
})
export class PlanStatusBadgeComponent {
  readonly status = input.required<PlanStatus>();
  /** Statuses da non mostrare (es. `['Planned']` nelle celle di tabella). */
  readonly hide = input<readonly PlanStatus[]>([]);

  protected readonly visible = computed(() => (this.hide().includes(this.status()) ? null : PLAN_STATUS[this.status()]));
}
