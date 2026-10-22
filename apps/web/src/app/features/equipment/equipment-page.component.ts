import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideCircleCheck, lucideCircleOff, lucideSearchX, lucideStar, lucideTriangleAlert } from '@ng-icons/lucide';
import { map } from 'rxjs';
import type { EquipmentModel } from '@micromes/api-client/models';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { toFailure } from '../materials/materials.service';
import { EquipmentFormDialogComponent } from './equipment-form-dialog.component';
import { EquipmentOverviewTabComponent } from './equipment-overview-tab.component';
import { EquipmentPageStore } from './equipment-page.store';
import { EquipmentSegmentsTabComponent } from './equipment-segments-tab.component';
import { EquipmentStore } from './equipment.store';

export const EQUIPMENT_TABS = [
  { key: 'panoramica', label: 'Panoramica' },
  { key: 'fasi', label: 'Fasi e attrezzature' },
  { key: 'calendario', label: 'Calendario' },
  { key: 'pianificazione', label: 'Pianificazione' },
  { key: 'storico', label: 'Storico' },
] as const;
type TabKey = (typeof EQUIPMENT_TABS)[number]['key'];

/** Pagina di dettaglio macchina (`/equipment/:id`). */
@Component({
  selector: 'app-equipment-page',
  imports: [
    HlmAlertDialogImports,
    HlmBadge,
    HlmButton,
    HlmEmptyImports,
    HlmSkeleton,
    HlmTabsImports,
    NgIcon,
    RouterLink,
    EquipmentFormDialogComponent,
    EquipmentOverviewTabComponent,
    EquipmentSegmentsTabComponent,
  ],
  providers: [
    EquipmentPageStore,
    provideIcons({ lucideArrowLeft, lucideCircleCheck, lucideCircleOff, lucideSearchX, lucideStar, lucideTriangleAlert }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      @if (store.loading() && !store.equipment()) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento macchina">
          <hlm-skeleton class="h-8 w-64" /><hlm-skeleton class="h-24 w-full" />
        </div>
      } @else if (store.notFound()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideSearchX" /></hlm-empty-media>
            <h1 hlmEmptyTitle #title tabindex="-1">Macchina non trovata</h1>
            <div hlmEmptyDescription>La macchina richiesta non esiste o è stata eliminata.</div>
          </hlm-empty-header>
          <hlm-empty-content><a hlmBtn routerLink="/equipment">Torna all'elenco</a></hlm-empty-content>
        </hlm-empty>
      } @else if (store.error()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <h1 hlmEmptyTitle #title tabindex="-1">Impossibile caricare la macchina</h1>
            <div hlmEmptyDescription>{{ store.error() }}</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="reload()">Riprova</button>
            <a hlmBtn variant="outline" routerLink="/equipment">Torna all'elenco</a>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (store.equipment(); as m) {
        <div>
          <a hlmBtn variant="ghost" size="sm" routerLink="/equipment"><ng-icon name="lucideArrowLeft" aria-hidden="true" /> Torna all'elenco</a>
        </div>
        <header class="flex flex-wrap items-start justify-between gap-3">
          <div class="flex flex-col gap-1">
            <h1 #title tabindex="-1" class="flex flex-wrap items-center gap-2 text-xl font-semibold outline-none">
              {{ m.name }}
              <span class="text-muted-foreground text-base font-normal">{{ m.code }}</span>
              <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
                @if (m.isActive) { <ng-icon name="lucideCircleCheck" aria-hidden="true" /> Attiva } @else { <ng-icon name="lucideCircleOff" aria-hidden="true" /> Disattivata }
              </span>
            </h1>
            <p class="text-muted-foreground text-sm">
              Capacità {{ m.capacity }} · efficienza {{ m.efficiencyPercent }}% · calendario:
              <span class="text-foreground">{{ calendarName() }}</span>
              @if (!m.workCalendarId) { <span>(predefinito)</span> }
            </p>
            @if (defaultSegments().length) {
              <p class="flex flex-wrap items-center gap-1.5 text-sm">
                <span class="text-muted-foreground">Predefinita per:</span>
                @for (p of defaultSegments(); track p.id) {
                  <span hlmBadge variant="outline" class="border-primary/40 bg-primary/10 text-primary gap-1"><ng-icon name="lucideStar" class="size-3 fill-current" aria-hidden="true" /> {{ p.code }}</span>
                }
              </p>
            }
          </div>
          <div class="flex flex-wrap gap-2">
            <button hlmBtn size="sm" variant="outline" type="button" (click)="formOpen.set(true)">Modifica</button>
            @if (m.isActive) {
              <button hlmBtn size="sm" variant="outline" type="button" (click)="askDeactivate()">Disattiva</button>
            } @else {
              <button hlmBtn size="sm" variant="outline" type="button" [disabled]="busy()" (click)="reactivate(m)">Riattiva</button>
            }
            <a hlmBtn size="sm" variant="outline" routerLink="/planning" [queryParams]="{ machine: m.id }">Apri nella timeline</a>
          </div>
        </header>
        @if (actionError()) {
          <p class="text-destructive text-sm" role="alert">{{ actionError() }}</p>
        }

        <hlm-tabs [tab]="tab()" (tabActivated)="setTab($any($event))">
          <hlm-tabs-list aria-label="Sezioni della macchina">
            @for (t of tabs; track t.key) {
              <button [hlmTabsTrigger]="t.key" type="button">{{ t.label }}</button>
            }
          </hlm-tabs-list>
        </hlm-tabs>

        <div role="region" [attr.aria-label]="tabLabel()">
          @switch (tab()) {
            @case ('panoramica') {
              @if (store.overview(); as o) {
                <app-equipment-overview-tab [overview]="o" />
              } @else if (store.overviewError()) {
                <div class="flex flex-col items-start gap-2" role="alert">
                  <p class="text-sm">Impossibile caricare la panoramica: {{ store.overviewError() }}</p>
                  <button hlmBtn size="sm" type="button" (click)="reload()">Riprova</button>
                </div>
              } @else {
                <div aria-busy="true" aria-label="Caricamento panoramica"><hlm-skeleton class="h-40 w-full" /></div>
              }
            }
            @case ('fasi') {
              <app-equipment-segments-tab [equipment]="m" />
            }
            @default {
              <p class="text-muted-foreground rounded-md border p-4 text-sm">In arrivo</p>
            }
          }
        </div>
      }
    </div>

    @if (formOpen() && store.equipment(); as m) {
      <app-equipment-form-dialog [equipment]="m" (saved)="onSaved()" (closed)="formOpen.set(false)" />
    }

    @if (confirmOpen() && store.equipment(); as m) {
      <hlm-alert-dialog state="open" (closed)="confirmOpen.set(false)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Disattivare la macchina?</h2>
              <p hlmAlertDialogDescription>
                {{ m.code }} — {{ m.name }} non sarà più offerta per le fasi, ma i collegamenti restano. Potrai riattivarla in qualsiasi momento.
              </p>
            </hlm-alert-dialog-header>
            @if (actionError()) {
              <p class="text-destructive text-sm" role="alert">{{ actionError() }}</p>
            }
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Annulla</button>
              <button hlmBtn type="button" variant="destructive" [disabled]="busy()" (click)="confirmDeactivate(m)">Disattiva</button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }
  `,
})
export class EquipmentPageComponent {
  protected readonly store = inject(EquipmentPageStore);
  private readonly list = inject(EquipmentStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly tabs = EQUIPMENT_TABS;
  private readonly id = toSignal(this.route.paramMap.pipe(map((p) => Number(p.get('id')))), { initialValue: NaN });
  private readonly qTab = toSignal(this.route.queryParamMap.pipe(map((p) => p.get('tab'))), { initialValue: null });
  protected readonly tab = computed<TabKey>(() => EQUIPMENT_TABS.find((t) => t.key === this.qTab())?.key ?? 'panoramica');
  protected readonly tabLabel = computed(() => EQUIPMENT_TABS.find((t) => t.key === this.tab())?.label ?? '');

  protected readonly formOpen = signal(false);
  protected readonly confirmOpen = signal(false);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  private readonly title = viewChild<ElementRef<HTMLElement>>('title');
  private focusedFor: number | null = null;

  protected readonly calendarName = computed(() => {
    const m = this.store.equipment();
    return m?.effectiveCalendar?.name ?? (m?.workCalendarId ? 'Calendario proprio' : 'Predefinito aziendale');
  });
  protected readonly defaultSegments = computed(() => this.store.equipment()?.processSegments.filter((p) => p.isDefault) ?? []);

  constructor() {
    effect(() => {
      const id = this.id();
      if (Number.isNaN(id)) {
        this.store.notFound.set(true);
        return;
      }
      void this.store.load(id);
    });
    // Sposta il focus sul titolo a ogni navigazione verso una macchina.
    effect(() => {
      const el = this.title()?.nativeElement;
      const id = this.id();
      if (el && this.focusedFor !== id) {
        this.focusedFor = id;
        el.focus();
      }
    });
  }

  protected setTab(t: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: t === 'panoramica' ? null : t },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected reload(): void {
    void this.store.load(this.id());
  }

  protected onSaved(): void {
    this.formOpen.set(false);
    this.reload();
  }

  protected askDeactivate(): void {
    this.actionError.set(null);
    this.confirmOpen.set(true);
  }

  protected async confirmDeactivate(m: EquipmentModel): Promise<void> {
    this.busy.set(true);
    this.actionError.set(null);
    try {
      await this.list.deactivate(m.id);
      this.confirmOpen.set(false);
      this.reload();
    } catch (e) {
      this.actionError.set(toFailure(e).message);
    } finally {
      this.busy.set(false);
    }
  }

  protected async reactivate(m: EquipmentModel): Promise<void> {
    this.busy.set(true);
    this.actionError.set(null);
    try {
      await this.list.update(m.id, {
        code: m.code,
        name: m.name,
        description: m.description,
        isActive: true,
        workCalendarId: m.workCalendarId,
        capacity: m.capacity,
        efficiencyPercent: m.efficiencyPercent,
        processSegmentIds: m.processSegments.map((p) => p.id),
      });
      this.reload();
    } catch (e) {
      this.actionError.set(toFailure(e).message);
    } finally {
      this.busy.set(false);
    }
  }
}
