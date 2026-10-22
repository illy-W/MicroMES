import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleAlert, lucideCircleCheck, lucideTriangleAlert, lucideWrench } from '@ng-icons/lucide';
import type { PlanAlertModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmPopoverImports } from '@spartan-ng/helm/popover';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { PlanningStore } from './planning.store';

/** Pulsante "Segnalazioni" con popover raggruppato per gravità. */
@Component({
  selector: 'app-planning-alerts',
  imports: [HlmButton, HlmBadge, HlmPopoverImports, HlmSkeleton, NgIcon, RouterLink],
  providers: [provideIcons({ lucideCircleAlert, lucideCircleCheck, lucideTriangleAlert, lucideWrench })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-popover align="end" [state]="popState()" (stateChanged)="popState.set($event)">
      <button hlmBtn hlmPopoverTrigger type="button" variant="outline" [attr.aria-label]="ariaLabel()">
        @if (total() === 0 && !store.loading()) {
          <ng-icon name="lucideCircleCheck" class="text-muted-foreground" aria-hidden="true" />
          <span class="text-muted-foreground">Nessuna segnalazione</span>
        } @else {
          <ng-icon name="lucideTriangleAlert" aria-hidden="true" />
          Segnalazioni
          @if (store.criticalCount() > 0) {
            <span hlmBadge variant="destructive" class="px-1.5">{{ store.criticalCount() }}</span>
          }
          @if (store.warningCount() > 0) {
            <span hlmBadge variant="secondary" class="px-1.5">{{ store.warningCount() }}</span>
          }
        }
      </button>
      <hlm-popover-content *hlmPopoverPortal="let ctx" class="max-h-[70vh] w-[min(32rem,calc(100vw-2rem))] overflow-y-auto p-0" role="dialog" aria-label="Segnalazioni di pianificazione">
        @if (store.loading() && !store.alerts().length) {
          <div class="flex flex-col gap-2 p-3" aria-busy="true"><hlm-skeleton class="h-14 w-full" /><hlm-skeleton class="h-14 w-full" /></div>
        } @else if (store.loadError()) {
          <div class="flex flex-col items-start gap-2 p-3">
            <p class="text-sm font-medium">Impossibile caricare le segnalazioni</p>
            <p class="text-muted-foreground text-sm">{{ store.loadError() }}</p>
            <button hlmBtn type="button" size="sm" (click)="store.loadAlerts()">Riprova</button>
          </div>
        } @else if (!total()) {
          <p class="text-muted-foreground p-3 text-sm">Nessuna segnalazione: il piano è coerente.</p>
        } @else {
          @for (g of groups(); track g.key) {
            @if (g.items.length) {
              <section [attr.aria-label]="g.title" class="border-b last:border-b-0">
                <h3 class="bg-popover sticky top-0 flex items-center gap-1.5 px-3 py-2 text-xs font-semibold">
                  <ng-icon [name]="g.icon" size="14" [class.text-destructive]="g.key === 'Critical'" aria-hidden="true" />
                  {{ g.title }} ({{ g.items.length }})
                </h3>
                <ul class="flex flex-col">
                  @for (a of g.items; track $index) {
                    <li class="flex flex-col gap-1 px-3 py-2">
                      <p class="text-sm font-medium"><span class="sr-only">{{ g.key === 'Critical' ? 'Critica: ' : 'Avviso: ' }}</span>{{ a.message }}</p>
                      <p class="text-muted-foreground text-sm">{{ a.suggestion }}</p>
                      <p class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                        @if (target(a) || a.productionOrderId) {
                          <button hlmBtn type="button" variant="link" size="sm" class="h-auto p-0" (click)="show(a)">Mostra nel piano</button>
                        }
                        @if (a.toolingId) {
                          <a class="inline-flex items-center gap-1 underline underline-offset-2" routerLink="/tooling" [attr.aria-label]="'Apri Attrezzature: ' + (a.toolingCode ?? '')" (click)="popState.set('closed')"><ng-icon name="lucideWrench" size="12" aria-hidden="true" /> Attrezzatura {{ a.toolingCode }}</a>
                        }
                        @if (a.productionOrderId; as oid) {
                          <a class="underline underline-offset-2" [routerLink]="['/production-orders', oid]" (click)="popState.set('closed')">Ordine {{ a.productionOrderCode }}</a>
                          <a class="underline underline-offset-2" routerLink="/work-orders" [queryParams]="{ order: oid }" (click)="popState.set('closed')">Lavori dell'ordine</a>
                        }
                      </p>
                    </li>
                  }
                </ul>
              </section>
            }
          }
        }
      </hlm-popover-content>
    </hlm-popover>
  `,
})
export class PlanningAlertsComponent {
  protected readonly store = inject(PlanningStore);
  protected readonly popState = signal<'open' | 'closed'>('closed');
  protected readonly total = computed(() => this.store.alerts().length);
  protected readonly ariaLabel = computed(() => {
    const c = this.store.criticalCount();
    const w = this.store.warningCount();
    return c + w === 0 ? 'Segnalazioni: nessuna' : `Segnalazioni: ${c} critiche, ${w} avvisi`;
  });
  protected readonly groups = computed(() => {
    const all: PlanAlertModel[] = this.store.alerts();
    return [
      { key: 'Critical', title: 'Critiche', icon: 'lucideCircleAlert', items: all.filter((a) => a.severity === 'Critical') },
      { key: 'Warning', title: 'Avvisi', icon: 'lucideTriangleAlert', items: all.filter((a) => a.severity !== 'Critical') },
    ];
  });

  protected target(a: PlanAlertModel): number | null {
    return a.workOrderId ?? a.workOrderIds?.[0] ?? null;
  }

  protected show(a: PlanAlertModel): void {
    this.popState.set('closed');
    this.store.showInPlan(this.target(a), a.productionOrderId);
  }
}
