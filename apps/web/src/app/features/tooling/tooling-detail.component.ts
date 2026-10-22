import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCircleCheck, lucideCircleOff } from '@ng-icons/lucide';
import type { ToolingDetailModel, ToolingModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { toFailure } from '../materials/materials.service';
import { ToolingStore } from './tooling.store';

/** Dettaglio attrezzatura: macchine compatibili, passi di ciclo e lavori aperti che la usano. */
@Component({
  selector: 'app-tooling-detail',
  imports: [HlmBadge, HlmButton, HlmSkeleton, NgIcon, RouterLink],
  providers: [provideIcons({ lucideArrowLeft, lucideCircleCheck, lucideCircleOff })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div>
        <button hlmBtn variant="ghost" size="sm" type="button" (click)="back.emit()"><ng-icon name="lucideArrowLeft" /> Tutte le attrezzature</button>
      </div>
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="flex flex-col gap-1">
          <h2 class="flex flex-wrap items-center gap-2 text-lg font-semibold">
            {{ tooling().name }}
            <span class="text-muted-foreground text-sm font-normal">{{ tooling().code }}</span>
            <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
              @if (tooling().isActive) { <ng-icon name="lucideCircleCheck" /> Attiva } @else { <ng-icon name="lucideCircleOff" /> Disattivata }
            </span>
          </h2>
          <p class="text-muted-foreground text-sm">
            Quantità {{ tooling().quantity }} · montaggio {{ tooling().setupMinutes }} min
          </p>
          @if (tooling().description) {
            <p class="text-sm">{{ tooling().description }}</p>
          }
        </div>
        <button hlmBtn size="sm" variant="outline" type="button" (click)="edit.emit(tooling())">Modifica attrezzatura</button>
      </div>

      @if (error()) {
        <p class="text-destructive text-sm" role="alert">{{ error() }}</p>
      } @else if (!detail()) {
        <div aria-busy="true" aria-label="Caricamento dettaglio"><hlm-skeleton class="h-32 w-full" /></div>
      } @else if (detail(); as d) {
        <div class="grid gap-4 lg:grid-cols-3">
          <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="td-eq">
            <h3 id="td-eq" class="text-sm font-semibold">Macchine compatibili</h3>
            @if (d.equipment.length) {
              <ul class="flex flex-col gap-1 text-sm">
                @for (e of d.equipment; track e.id) {
                  <li>
                    <span class="font-medium">{{ e.code }}</span> <span class="text-muted-foreground">{{ e.name }}{{ e.isActive ? '' : ' (disattivata)' }}</span>
                  </li>
                }
              </ul>
            } @else {
              <p class="text-muted-foreground text-sm">Tutte le macchine.</p>
            }
          </section>
          <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="td-steps">
            <h3 id="td-steps" class="text-sm font-semibold">Passi di ciclo che la richiedono</h3>
            @if (d.routingSteps.length) {
              <ul class="flex flex-col gap-1 text-sm">
                @for (s of d.routingSteps; track s.id) {
                  <li><span class="font-medium">{{ s.code }}</span> <span class="text-muted-foreground">{{ s.name }}</span></li>
                }
              </ul>
            } @else {
              <p class="text-muted-foreground text-sm">Nessun passo di ciclo.</p>
            }
            <a hlmBtn variant="link" size="sm" class="h-auto w-fit p-0" routerLink="/materials">Apri i materiali per vedere i cicli</a>
          </section>
          <section class="flex flex-col gap-2 rounded-md border p-4" aria-labelledby="td-wo">
            <h3 id="td-wo" class="text-sm font-semibold">Lavori aperti che la usano</h3>
            @if (d.workOrders.length) {
              <ul class="flex flex-col gap-1 text-sm">
                @for (w of d.workOrders; track w.id) {
                  <li><span class="font-medium">{{ w.code }}</span> <span class="text-muted-foreground">{{ w.name }}</span></li>
                }
              </ul>
            } @else {
              <p class="text-muted-foreground text-sm">Nessun lavoro aperto.</p>
            }
            <a hlmBtn variant="link" size="sm" class="h-auto w-fit p-0" routerLink="/work-orders">Apri gli ordini di lavoro</a>
          </section>
        </div>
      }
    </div>
  `,
})
export class ToolingDetailComponent {
  private readonly store = inject(ToolingStore);

  readonly tooling = input.required<ToolingModel>();
  readonly back = output<void>();
  readonly edit = output<ToolingModel>();

  protected readonly detail = signal<ToolingDetailModel | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const t = this.tooling();
      void t.updatedAt;
      untracked(() => void this.load(t.id));
    });
  }

  private async load(id: number): Promise<void> {
    this.error.set(null);
    try {
      this.detail.set(await this.store.detail(id));
    } catch (e) {
      this.error.set(toFailure(e).message);
    }
  }
}
