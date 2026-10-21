import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleAlert, lucideCircleCheck, lucideRefreshCw, lucideTriangleAlert, lucideWand } from '@ng-icons/lucide';
import type { PlanAlertModel, PlanningScope } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { PlanPreviewDialogComponent } from './plan-preview-dialog.component';
import { PlanningTimelineComponent } from './planning-timeline.component';
import { PlanningStore } from './planning.store';

@Component({
  selector: 'app-planning',
  imports: [HlmButton, HlmBadge, HlmEmptyImports, HlmSkeleton, NgIcon, RouterLink, PlanPreviewDialogComponent, PlanningTimelineComponent],
  providers: [provideIcons({ lucideCircleAlert, lucideCircleCheck, lucideRefreshCw, lucideTriangleAlert, lucideWand })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="flex flex-col gap-1">
          <h1 class="text-xl font-semibold">Pianificazione</h1>
          <p class="text-muted-foreground text-sm">Assegna macchine e date ai lavori aperti, con anteprima prima di applicare.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <button hlmBtn type="button" (click)="open('Unplanned')">
            <ng-icon name="lucideWand" aria-hidden="true" /> Pianifica non pianificati
          </button>
          <button hlmBtn type="button" variant="outline" (click)="open('All')">
            <ng-icon name="lucideRefreshCw" aria-hidden="true" /> Ripianifica tutto
          </button>
        </div>
      </div>

      <app-planning-timeline />

      <section aria-labelledby="alerts-title" class="flex flex-col gap-3">
        <div class="flex flex-wrap items-center gap-2">
          <h2 id="alerts-title" class="text-base font-semibold">Segnalazioni</h2>
          <span hlmBadge variant="destructive" class="gap-1 px-1.5">
            <ng-icon name="lucideCircleAlert" size="12" aria-hidden="true" /> {{ store.criticalCount() }} critiche
          </span>
          <span hlmBadge variant="secondary" class="gap-1 px-1.5">
            <ng-icon name="lucideTriangleAlert" size="12" aria-hidden="true" /> {{ store.warningCount() }} avvisi
          </span>
        </div>

        @if (store.loading() && !store.alerts().length) {
          <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento segnalazioni">
            @for (i of [1, 2, 3]; track i) { <hlm-skeleton class="h-14 w-full" /> }
          </div>
        } @else if (store.loadError()) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
              <div hlmEmptyTitle>Impossibile caricare le segnalazioni</div>
              <div hlmEmptyDescription>{{ store.loadError() }}</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" (click)="store.loadAlerts()">Riprova</button></hlm-empty-content>
          </hlm-empty>
        } @else if (!store.alerts().length) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideCircleCheck" /></hlm-empty-media>
              <div hlmEmptyTitle>Nessuna segnalazione: il piano è coerente</div>
            </hlm-empty-header>
          </hlm-empty>
        } @else {
          <ul class="grid gap-2">
            @for (a of sorted(); track $index) {
              <li class="flex items-start gap-3 rounded-md border p-3">
                <ng-icon [name]="a.severity === 'Critical' ? 'lucideCircleAlert' : 'lucideTriangleAlert'" [class.text-destructive]="a.severity === 'Critical'" class="mt-0.5 shrink-0" size="18" aria-hidden="true" />
                <div class="flex min-w-0 flex-1 flex-col gap-0.5">
                  <p class="text-sm font-medium">
                    <span class="sr-only">{{ a.severity === 'Critical' ? 'Critica: ' : 'Avviso: ' }}</span>{{ a.message }}
                  </p>
                  <p class="text-muted-foreground text-sm">{{ a.suggestion }}</p>
                  @if (a.productionOrderId; as oid) {
                    <p class="flex flex-wrap gap-x-4 text-sm">
                      <a class="underline underline-offset-2" [routerLink]="['/production-orders', oid]">Ordine {{ a.productionOrderCode }}</a>
                      <a class="underline underline-offset-2" routerLink="/work-orders" [queryParams]="{ order: oid }">Lavori dell'ordine</a>
                    </p>
                  }
                </div>
              </li>
            }
          </ul>
        }
      </section>
    </div>

    @if (dialogScope(); as s) {
      <app-plan-preview-dialog [scope]="s" (closed)="dialogScope.set(null)" (applied)="dialogScope.set(null); store.loadAlerts()" />
    }
  `,
})
export class PlanningComponent implements OnInit {
  protected readonly store = inject(PlanningStore);
  protected readonly dialogScope = signal<PlanningScope | null>(null);

  protected readonly sorted = computed<PlanAlertModel[]>(() =>
    [...this.store.alerts()].sort((a, b) => Number(b.severity === 'Critical') - Number(a.severity === 'Critical')),
  );

  ngOnInit(): void {
    void this.store.loadAlerts();
  }

  protected open(s: PlanningScope): void {
    this.dialogScope.set(s);
  }
}
