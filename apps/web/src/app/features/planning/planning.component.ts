import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideRefreshCw, lucideWand } from '@ng-icons/lucide';
import type { PlanningScope } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { PlanningAlertsComponent } from './planning-alerts.component';
import { PlanPreviewDialogComponent } from './plan-preview-dialog.component';
import { PlanningTimelineComponent } from './planning-timeline.component';
import { PlanningStore } from './planning.store';

@Component({
  selector: 'app-planning',
  imports: [HlmButton, NgIcon, PlanningAlertsComponent, PlanPreviewDialogComponent, PlanningTimelineComponent],
  providers: [provideIcons({ lucideRefreshCw, lucideWand })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="-mx-2 flex min-w-0 h-[calc(100svh-8rem)] min-h-[32rem] flex-col gap-3">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h1 class="text-xl font-semibold" title="Assegna macchine e date ai lavori aperti, con anteprima prima di applicare.">Pianificazione</h1>
        <div class="flex flex-wrap items-center gap-2">
          <button hlmBtn type="button" (click)="open('Unplanned')">
            <ng-icon name="lucideWand" aria-hidden="true" /> Pianifica non pianificati
          </button>
          <button hlmBtn type="button" variant="outline" (click)="open('All')">
            <ng-icon name="lucideRefreshCw" aria-hidden="true" /> Ripianifica tutto
          </button>
          <app-planning-alerts />
        </div>
      </div>

      <app-planning-timeline class="flex min-h-0 min-w-0 flex-1 flex-col" />
    </div>

    @if (dialogScope(); as s) {
      <app-plan-preview-dialog [scope]="s" (closed)="dialogScope.set(null)" (applied)="dialogScope.set(null); store.loadAlerts()" />
    }
  `,
})
export class PlanningComponent implements OnInit {
  protected readonly store = inject(PlanningStore);
  protected readonly dialogScope = signal<PlanningScope | null>(null);

  ngOnInit(): void {
    void this.store.loadAlerts();
  }

  protected open(s: PlanningScope): void {
    this.dialogScope.set(s);
  }
}
