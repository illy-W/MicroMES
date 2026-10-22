import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarClock,
  lucideChevronRight,
  lucideCircleCheck,
  lucideCircleDot,
  lucideCircleOff,
  lucideClock,
  lucideFileQuestion,
  lucideLock,
  lucideOctagonX,
  lucidePause,
  lucidePlay,
  lucideSend,
  lucideTriangleAlert,
} from '@ng-icons/lucide';
import {
  columnVisibilityFeature,
  createColumnHelper,
  createExpandedRowModel,
  type ExpandedState,
  injectTable,
  rowExpandingFeature,
  tableFeatures,
} from '@tanstack/angular-table';
import {
  ProductionOrderStatus,
  type LockAllResultModel,
  type ProductionOrderDetailModel,
  type ProductionOrderTreeModel,
  type WorkOrderLinkModel,
} from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { HlmLabel } from '@spartan-ng/helm/label';
import { ProductionOrdersService } from '@micromes/api-client/services/production-orders';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmProgressImports } from '@spartan-ng/helm/progress';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { firstValueFrom, map } from 'rxjs';
import { PlanningService } from '@micromes/api-client/services/planning';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { CalendarsStore } from '../calendars/calendars.store';
import { PlanPreviewDialogComponent } from '../planning/plan-preview-dialog.component';
import { PlanStatusBadgeComponent } from '../planning/plan-status-badge.component';
import { formatDateTime } from '../planning/timeline-time';
import { unitLabel } from '../materials/material-labels';
import { toFailure } from '../materials/materials.service';
import { formatMinutes } from '../routing/routing-metrics';
import { EstimatedEndComponent } from './estimated-end.component';
import { ProductionOrderOverviewComponent } from './overview/overview.component';
import { ProductionOrderConfirmDialogComponent } from './production-order-confirm-dialog.component';
import { ProductionOrderFormDialogComponent } from './production-order-form-dialog.component';
import { ACTIONS, canEditOrder, formatDate, formatQty, orderStatus, workStatus } from './production-order-labels';
import { ProductionOrdersStore } from './production-orders.store';

const features = tableFeatures({
  columnVisibilityFeature,
  rowExpandingFeature,
  expandedRowModel: createExpandedRowModel(),
});
const columnHelper = createColumnHelper<typeof features, ProductionOrderTreeModel>();
const columns = columnHelper.columns([
  columnHelper.accessor('code', { id: 'code', header: 'Ordine' }),
  columnHelper.accessor('materialCode', { id: 'material', header: 'Materiale' }),
  columnHelper.accessor('quantity', { id: 'quantity', header: 'Quantità' }),
  columnHelper.accessor('status', { id: 'status', header: 'Stato' }),
  columnHelper.accessor('progressPercent', { id: 'progress', header: 'Avanzamento' }),
]);

@Component({
  selector: 'app-production-order-detail',
  imports: [
    RouterLink,
    HlmTableImports,
    HlmEmptyImports,
    HlmProgressImports,
    HlmTabsImports,
    HlmAlertDialogImports,
    HlmBadge,
    HlmCheckbox,
    HlmLabel,
    HlmButton,
    PlanPreviewDialogComponent,
    PlanStatusBadgeComponent,
    HlmSkeleton,
    NgIcon,
    ProductionOrderFormDialogComponent,
    ProductionOrderConfirmDialogComponent,
    ProductionOrderOverviewComponent,
    EstimatedEndComponent,
  ],
  providers: [
    provideIcons({
      lucideCalendarClock,
      lucideChevronRight,
      lucideCircleCheck,
      lucideCircleDot,
      lucideCircleOff,
      lucideClock,
      lucideFileQuestion,
      lucideLock,
      lucideOctagonX,
      lucidePause,
      lucidePlay,
      lucideSend,
      lucideTriangleAlert,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <a hlmBtn variant="link" size="sm" class="h-auto w-fit p-0" routerLink="/production-orders">← Ordini</a>

      @if (loading() && !order()) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento ordine">
          <hlm-skeleton class="h-8 w-80" />
          <hlm-skeleton class="h-20 w-full" />
          <hlm-skeleton class="h-40 w-full" />
        </div>
      } @else if (notFound()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideFileQuestion" /></hlm-empty-media>
            <div hlmEmptyTitle>Ordine non trovato</div>
            <div hlmEmptyDescription>L’ordine richiesto non esiste o è stato rimosso.</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <a hlmBtn variant="outline" routerLink="/production-orders">Torna agli ordini</a>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (loadError()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare l’ordine</div>
            <div hlmEmptyDescription>{{ loadError() }}</div>
          </hlm-empty-header>
          <hlm-empty-content>
            <button hlmBtn type="button" (click)="reload()">Riprova</button>
          </hlm-empty-content>
        </hlm-empty>
      } @else if (order(); as o) {
        @let st = status(o.status);
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div class="flex flex-col gap-1">
            <h1 class="flex flex-wrap items-center gap-3 text-xl font-semibold">
              {{ o.code }}
              <span hlmBadge variant="outline" class="px-1.5" [class]="st.tone"><ng-icon [name]="st.icon" /> {{ st.label }}</span>
            </h1>
            <p class="text-sm">
              <span class="font-medium">{{ o.materialCode }}</span>
              <span class="text-muted-foreground"> {{ o.materialName }}</span>
            </p>
            <p class="text-muted-foreground text-sm tabular-nums">
              Priorità {{ o.priority }} · Inizio richiesto {{ date(o.requestedStart) }} · Fine richiesta {{ date(o.requestedEnd) }}
            </p>
            @if (o.estimatedEnd) {
              <p class="text-sm">
                <app-estimated-end [estimatedEnd]="o.estimatedEnd" [requestedEnd]="o.requestedEnd" [planned]="hasPlan()" />
              </p>
            }
            @if (planBlockVisible()) {
              <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm" data-testid="plan-block">
                <span class="text-muted-foreground">Piano</span>
                @if (hasPlan()) {
                  <span class="tabular-nums">{{ planDate(o.plannedStart) }} – {{ planDate(o.plannedEnd) }}</span>
                }
                <app-plan-status-badge [status]="o.planStatus" />
                @if (o.lockedWorkOrdersCount > 0) {
                  <span hlmBadge variant="outline" class="gap-1 px-1.5" data-testid="locked-badge">
                    <ng-icon name="lucideLock" size="12" aria-hidden="true" /> {{ o.lockedWorkOrdersCount }} fissati
                  </span>
                }
                @if (can('LockAll')) {
                  <button hlmBtn size="sm" variant="ghost" type="button" (click)="openLockAll('lock')">Fissa tutti i lavori aperti</button>
                }
                @if (can('UnlockAll')) {
                  <button hlmBtn size="sm" variant="ghost" type="button" (click)="openLockAll('unlock')">Sblocca tutti</button>
                }
                <button hlmBtn size="sm" variant="outline" type="button" (click)="planOpen.set(true)">Pianifica</button>
                @if (hasPlan()) {
                  <button hlmBtn size="sm" variant="ghost" type="button" (click)="removeOpen.set(true)">Rimuovi piano</button>
                }
              </div>
            } @else if (o.status === 'Planned') {
              <p class="text-muted-foreground text-sm">Il piano sarà disponibile dopo il rilascio dell’ordine.</p>
            }
            @if (o.parentOrderId) {
              <p class="text-muted-foreground text-sm">
                Ordine figlio di
                <a class="text-primary hover:underline" [routerLink]="['/production-orders', o.parentOrderId]">{{ o.parentOrderCode }}</a>
              </p>
            }
            @if (o.notes) {
              <p class="text-muted-foreground text-sm">{{ o.notes }}</p>
            }
          </div>
          <div class="flex flex-wrap gap-2">
            @if (canEdit()) {
              <button hlmBtn size="sm" variant="outline" type="button" (click)="formOpen.set(true)">Modifica</button>
            }
            @if (can('Release')) {
              <button hlmBtn size="sm" type="button" (click)="confirm.set('Release')">Rilascia</button>
            }
            @if (can('Cancel')) {
              <button hlmBtn size="sm" variant="destructive" type="button" (click)="confirm.set('Cancel')">Annulla</button>
            }
            @if (can('Abort')) {
              <button hlmBtn size="sm" variant="destructive" type="button" (click)="confirm.set('Abort')">Interrompi</button>
            }
          </div>
        </div>

        @if (o.planStatus === 'Late' || o.planStatus === 'Stale') {
          <p class="text-destructive flex flex-wrap items-center gap-2 rounded-md border px-3 py-2 text-sm" role="status" data-testid="plan-banner">
            <ng-icon [name]="o.planStatus === 'Late' ? 'lucideTriangleAlert' : 'lucideClock'" aria-hidden="true" />
            {{ o.planStatus === 'Late' ? 'Il piano di questo ordine prevede un ritardo rispetto alla data richiesta.' : 'Il piano di questo ordine è obsoleto.' }}
            <a class="underline" routerLink="/planning">Apri la pianificazione</a>
          </p>
        }

        <dl class="grid grid-cols-2 gap-x-6 gap-y-4 rounded-md border p-4 md:grid-cols-3 lg:grid-cols-5">
          <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground text-xs">Quantità richiesta</dt>
            <dd class="text-sm font-medium tabular-nums">{{ qty(o.quantity) }} {{ unit(o.unitOfMeasure) }}</dd>
          </div>
          <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground text-xs">Quantità prodotta</dt>
            <dd class="flex items-center gap-2 text-sm font-medium tabular-nums">
              {{ qty(o.summary.producedQuantity) }} {{ unit(o.unitOfMeasure) }}
              <hlm-progress class="w-16" [value]="o.progressPercent" aria-label="Avanzamento ordine">
                <hlm-progress-indicator />
              </hlm-progress>
              <span class="text-muted-foreground text-xs">{{ o.progressPercent }}%</span>
            </dd>
          </div>
          <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground text-xs">Contenuto di lavoro</dt>
            <dd class="text-sm font-medium tabular-nums">{{ minutes(o.summary.workContentMinutes) }}</dd>
          </div>
          <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground text-xs">Durata minima</dt>
            <dd class="text-sm font-medium tabular-nums">{{ minutes(o.summary.minimumDurationMinutes) }}</dd>
          </div>
          <div class="flex flex-col gap-1">
            <dt class="text-muted-foreground text-xs">Tempo lavorato · Scarti</dt>
            <dd class="text-sm font-medium tabular-nums">
              {{ minutes(o.summary.workedMinutes) }} · {{ qty(o.summary.scrapQuantity) }} {{ unit(o.unitOfMeasure) }}
            </dd>
          </div>
        </dl>

        <hlm-tabs class="relative" [tab]="activeTab()" (tabActivated)="userTab.set($any($event))">
          <hlm-tabs-list aria-label="Sezioni dell’ordine">
            <button hlmTabsTrigger="overview" type="button">Panoramica</button>
            <button hlmTabsTrigger="work" type="button">Lavori ({{ o.workOrders.length }})</button>
            <button hlmTabsTrigger="materials" type="button">Materiali richiesti ({{ o.materials.length }})</button>
            <button hlmTabsTrigger="children" type="button">Ordini figli ({{ o.children.length }})</button>
          </hlm-tabs-list>

          <div hlmTabsContent="work" class="pt-4">
            @if (!o.workOrders.length) {
              <p class="text-muted-foreground text-sm">Nessun lavoro: i lavori vengono generati al rilascio dell’ordine.</p>
            } @else {
              <div class="mb-2 flex justify-end">
                <a hlmBtn variant="outline" size="sm" [routerLink]="['/work-orders']" [queryParams]="{ order: o.id }">Apri in Ordini di lavoro</a>
              </div>
              <div class="w-full overflow-auto rounded-md border">
                <div hlmTableContainer>
                  <table hlmTable>
                    <thead hlmTHead class="bg-muted">
                      <tr hlmTr>
                        <th hlmTh>Lavoro</th>
                        <th hlmTh>Fase</th>
                        @if (hasMachines(o.workOrders)) {
                          <th hlmTh>Macchina</th>
                        }
                        <th hlmTh class="text-right">Pianificato</th>
                        <th hlmTh>Stato</th>
                        <th hlmTh class="text-right">Buono / Scarto</th>
                        <th hlmTh class="text-right">Lavorato</th>
                        <th hlmTh>Predecessori</th>
                        <th hlmTh>Successori</th>
                      </tr>
                    </thead>
                    <tbody hlmTBody>
                      @for (w of o.workOrders; track w.id) {
                        @let ws = workStatus(w.status);
                        <tr hlmTr class="hover:bg-muted/50 cursor-pointer" (click)="openWork()">
                          <td hlmTd class="font-medium">
                            <a
                              class="hover:underline"
                              [routerLink]="['/work-orders']"
                              [queryParams]="{ order: o.id }"
                              [attr.aria-label]="'Apri il lavoro ' + w.code + ' in Ordini di lavoro'"
                              (click)="$event.stopPropagation()"
                              >{{ w.code }}</a
                            >
                          </td>
                          <td hlmTd>{{ w.processSegmentName }}</td>
                          @if (hasMachines(o.workOrders)) {
                            <td hlmTd>
                              @if (w.status === 'Running' && w.currentEquipment; as cur) {
                                <span class="font-medium">{{ cur.code }}</span>
                              } @else if (w.assignedEquipment; as eq) {
                                <span class="font-medium">{{ eq.code }}</span>
                                @if (!eq.isEnabled) {
                                  <span hlmBadge variant="outline" class="text-destructive ml-1 px-1.5">non più abilitata</span>
                                }
                              }
                            </td>
                          }
                          <td hlmTd class="text-right tabular-nums">{{ minutes(w.plannedMinutes) }}</td>
                          <td hlmTd>
                            <span hlmBadge variant="outline" class="px-1.5" [class]="ws.tone"><ng-icon [name]="ws.icon" /> {{ ws.label }}</span>
                          </td>
                          <td hlmTd class="text-right tabular-nums">{{ qty(w.goodQuantity) }} / {{ qty(w.scrapQuantity) }}</td>
                          <td hlmTd class="text-right tabular-nums">{{ minutes(w.workedMinutes) }}</td>
                          <td hlmTd class="text-muted-foreground">{{ links(w.predecessors) }}</td>
                          <td hlmTd class="text-muted-foreground">{{ links(w.successors) }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          </div>

          <div hlmTabsContent="materials" class="pt-4">
            @if (!o.materials.length) {
              <p class="text-muted-foreground text-sm">Nessun materiale richiesto.</p>
            } @else {
              <div class="w-full overflow-auto rounded-md border">
                <div hlmTableContainer>
                  <table hlmTable>
                    <thead hlmTHead class="bg-muted">
                      <tr hlmTr>
                        <th hlmTh>Materiale</th>
                        <th hlmTh>Lavoro</th>
                        <th hlmTh class="text-right">Quantità</th>
                        <th hlmTh>Ordine figlio</th>
                      </tr>
                    </thead>
                    <tbody hlmTBody>
                      @for (m of o.materials; track m.id) {
                        <tr hlmTr>
                          <td hlmTd>
                            <span class="font-medium">{{ m.materialCode }}</span>
                            <span class="text-muted-foreground"> {{ m.materialName }}</span>
                          </td>
                          <td hlmTd class="text-muted-foreground">{{ m.workOrderCode }}</td>
                          <td hlmTd class="text-right tabular-nums">{{ qty(m.requiredQuantity) }} {{ unit(m.unitOfMeasure) }}</td>
                          <td hlmTd>
                            @if (m.childProductionOrderId) {
                              <a class="text-primary hover:underline" [routerLink]="['/production-orders', m.childProductionOrderId]">{{ m.childProductionOrderCode }}</a>
                            } @else {
                              —
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          </div>

          <div hlmTabsContent="children" class="pt-4">
            @if (!o.children.length) {
              <p class="text-muted-foreground text-sm">Questo ordine non ha ordini figli.</p>
            } @else {
              <div class="w-full overflow-auto rounded-md border">
                <div hlmTableContainer>
                  <table hlmTable>
                    <thead hlmTHead class="bg-muted">
                      @for (hg of table.getHeaderGroups(); track hg.id) {
                        <tr hlmTr>
                          @for (h of hg.headers; track h.id) {
                            <th hlmTh [class.text-right]="h.column.id === 'quantity'">{{ h.column.columnDef.header }}</th>
                          }
                        </tr>
                      }
                    </thead>
                    <tbody hlmTBody>
                      @for (row of table.getRowModel().rows; track row.id) {
                        @let c = row.original;
                        @let cs = status(c.status);
                        <tr hlmTr>
                          <td hlmTd>
                            <div class="flex items-center gap-1" [style.padding-left.rem]="row.depth * 1.5">
                              @if (row.getCanExpand()) {
                                <button
                                  hlmBtn
                                  variant="ghost"
                                  size="icon-sm"
                                  type="button"
                                  [attr.aria-expanded]="row.getIsExpanded()"
                                  [attr.aria-label]="(row.getIsExpanded() ? 'Comprimi ' : 'Espandi ') + c.code"
                                  (click)="row.toggleExpanded()"
                                >
                                  <ng-icon name="lucideChevronRight" class="transition-transform" [class.rotate-90]="row.getIsExpanded()" />
                                </button>
                              } @else {
                                <span class="inline-block size-7 shrink-0" aria-hidden="true"></span>
                              }
                              <a class="font-medium hover:underline" [routerLink]="['/production-orders', c.id]">{{ c.code }}</a>
                            </div>
                          </td>
                          <td hlmTd>
                            <span class="font-medium">{{ c.materialCode }}</span>
                            <span class="text-muted-foreground"> {{ c.materialName }}</span>
                          </td>
                          <td hlmTd class="text-right tabular-nums">{{ qty(c.quantity) }} {{ unit(c.unitOfMeasure) }}</td>
                          <td hlmTd>
                            <span hlmBadge variant="outline" class="px-1.5" [class]="cs.tone"><ng-icon [name]="cs.icon" /> {{ cs.label }}</span>
                          </td>
                          <td hlmTd>
                            <div class="flex min-w-36 items-center gap-2">
                              <hlm-progress class="flex-1" [value]="c.progressPercent" [attr.aria-label]="'Avanzamento ' + c.code">
                                <hlm-progress-indicator />
                              </hlm-progress>
                              <span class="text-muted-foreground w-10 text-right text-xs tabular-nums">{{ c.progressPercent }}%</span>
                            </div>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          </div>

          <!--
            La panoramica sta fuori dai pannelli dei tab (che si distruggono): resta montata e, da inattiva, si
            nasconde senza display:none (romperebbe le misure di ngDiagram) e si rende inerte.
          -->
          @if (showOverview()) {
            <div
              role="tabpanel"
              aria-label="Panoramica"
              class="pt-4"
              [class]="activeTab() === 'overview' ? 'pt-4' : 'pointer-events-none invisible absolute inset-x-0 top-0 h-0 overflow-hidden'"
              [attr.aria-hidden]="activeTab() === 'overview' ? null : 'true'"
              [attr.inert]="activeTab() === 'overview' ? null : ''"
            >
              <app-production-order-overview [orderId]="o.id" [refreshKey]="o.status + '|' + refreshTick()" [requestedEnd]="o.requestedEnd" [planned]="hasPlan()" />
            </div>
          } @else if (activeTab() === 'overview') {
            <p class="text-muted-foreground pt-4 text-sm">{{ overviewMessage() }}</p>
          }
        </hlm-tabs>

        @if (planOpen()) {
          <app-plan-preview-dialog scope="Order" [productionOrderId]="o.id" (applied)="planOpen.set(false); reloadAll()" (closed)="planOpen.set(false)" />
        }
        @if (removeOpen()) {
          <hlm-alert-dialog state="open" (closed)="removeOpen.set(false)">
            <ng-template hlmAlertDialogPortal>
              <hlm-alert-dialog-content>
                <hlm-alert-dialog-header>
                  <h2 hlmAlertDialogTitle>Rimuovere il piano?</h2>
                  <p hlmAlertDialogDescription>Le date previste dei lavori aperti di {{ o.code }} saranno cancellate.</p>
                </hlm-alert-dialog-header>
                <hlm-alert-dialog-footer>
                  <button hlmAlertDialogCancel>Indietro</button>
                  <button hlmBtn type="button" variant="destructive" [disabled]="removing()" (click)="removePlan(o.id)">
                    {{ removing() ? 'Rimozione…' : 'Rimuovi piano' }}
                  </button>
                </hlm-alert-dialog-footer>
              </hlm-alert-dialog-content>
            </ng-template>
          </hlm-alert-dialog>
        }
        @if (lockAllMode(); as mode) {
          <hlm-alert-dialog state="open" (closed)="lockAllMode.set(null)">
            <ng-template hlmAlertDialogPortal>
              <hlm-alert-dialog-content>
                <hlm-alert-dialog-header>
                  <h2 hlmAlertDialogTitle>{{ mode === 'lock' ? 'Fissare tutti i lavori aperti?' : 'Sbloccare tutti i lavori?' }}</h2>
                  <p hlmAlertDialogDescription>
                    @if (mode === 'lock') {
                      I lavori aperti pianificati di {{ o.code }} saranno fissati e la ripianificazione non li sposterà.
                    } @else {
                      {{ o.lockedWorkOrdersCount }} {{ o.lockedWorkOrdersCount === 1 ? 'lavoro fissato' : 'lavori fissati' }} di {{ o.code }} {{ o.lockedWorkOrdersCount === 1 ? 'sarà sbloccato' : 'saranno sbloccati' }}.
                    }
                  </p>
                </hlm-alert-dialog-header>
                <div class="flex items-center gap-2">
                  <hlm-checkbox inputId="lock-all-children" [checked]="includeChildren()" (checkedChange)="includeChildren.set($event)" />
                  <label hlmLabel for="lock-all-children">Includi gli ordini figli</label>
                </div>
                <hlm-alert-dialog-footer>
                  <button hlmAlertDialogCancel>Indietro</button>
                  <button hlmBtn type="button" [disabled]="lockAllBusy()" (click)="runLockAll(o.id, mode)">
                    {{ lockAllBusy() ? 'Operazione in corso…' : mode === 'lock' ? 'Fissa tutti' : 'Sblocca tutti' }}
                  </button>
                </hlm-alert-dialog-footer>
              </hlm-alert-dialog-content>
            </ng-template>
          </hlm-alert-dialog>
        }
        @if (lockResult(); as lr) {
          <hlm-alert-dialog state="open" (closed)="lockResult.set(null)">
            <ng-template hlmAlertDialogPortal>
              <hlm-alert-dialog-content>
                <hlm-alert-dialog-header>
                  <h2 hlmAlertDialogTitle>{{ lr.locked }} {{ lr.locked === 1 ? 'lavoro fissato' : 'lavori fissati' }}</h2>
                  <p hlmAlertDialogDescription>{{ lr.skipped.length }} {{ lr.skipped.length === 1 ? 'lavoro non fissato' : 'lavori non fissati' }}.</p>
                </hlm-alert-dialog-header>
                @if (lr.skipped.length) {
                  <ul class="grid max-h-60 gap-1.5 overflow-auto text-sm" aria-label="Lavori non fissati">
                    @for (k of lr.skipped; track k.workOrderId) {
                      <li class="rounded-md border p-2"><span class="font-medium">{{ k.code }}</span>: {{ k.reason }}</li>
                    }
                  </ul>
                }
                <hlm-alert-dialog-footer>
                  <button hlmAlertDialogCancel>Chiudi</button>
                </hlm-alert-dialog-footer>
              </hlm-alert-dialog-content>
            </ng-template>
          </hlm-alert-dialog>
        }
        @if (formOpen()) {
          <app-production-order-form-dialog [order]="o" (saved)="formOpen.set(false); reload()" (closed)="formOpen.set(false)" />
        }
        @if (confirm(); as action) {
          <app-production-order-confirm-dialog
            [order]="o"
            [action]="action"
            (done)="confirm.set(null); reload()"
            (closed)="confirm.set(null)"
          />
        }
      }
    </div>
  `,
})
export class ProductionOrderDetailComponent {
  private readonly store = inject(ProductionOrdersStore);
  private readonly router = inject(Router);

  private readonly orderId = toSignal(inject(ActivatedRoute).paramMap.pipe(map((p) => Number(p.get('id')))), {
    initialValue: 0,
  });

  protected readonly order = signal<ProductionOrderDetailModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly notFound = signal(false);
  protected readonly formOpen = signal(false);
  protected readonly planOpen = signal(false);
  protected readonly removeOpen = signal(false);
  protected readonly removing = signal(false);
  protected readonly lockAllMode = signal<'lock' | 'unlock' | null>(null);
  protected readonly includeChildren = signal(false);
  protected readonly lockAllBusy = signal(false);
  protected readonly lockResult = signal<LockAllResultModel | null>(null);
  private readonly ordersApi = inject(ProductionOrdersService);
  /** Incrementato dopo applica/rimuovi piano: ricarica anche la panoramica. */
  protected readonly refreshTick = signal(0);
  private readonly calendars = inject(CalendarsStore);
  private readonly planning = inject(PlanningService);
  protected readonly hasPlan = computed(() => !!this.order()?.plannedEnd);
  protected readonly planBlockVisible = computed(() => {
    const s = this.order()?.status;
    return s === ProductionOrderStatus.Released || s === ProductionOrderStatus.InProgress;
  });
  protected readonly planDate = (iso?: string | null): string => (iso ? formatDateTime(iso, this.calendars.timeZone()) : '—');
  protected readonly confirm = signal<string | null>(null);
  /** Scheda scelta dall'utente; finché è null vale il default (sempre Panoramica). */
  protected readonly userTab = signal<string | null>(null);

  /** La panoramica ha senso dal rilascio in poi; un ordine annullato senza lavori non ne ha. */
  protected readonly showOverview = computed(() => {
    const o = this.order();
    return !!o && o.status !== ProductionOrderStatus.Planned && !(o.status === ProductionOrderStatus.Cancelled && !o.workOrders.length);
  });
  protected readonly activeTab = computed(() => this.userTab() ?? 'overview');
  protected readonly overviewMessage = computed(() =>
    this.order()?.status === ProductionOrderStatus.Cancelled
      ? 'L’ordine è stato annullato prima del rilascio: non ci sono lavori da mostrare.'
      : 'L’ordine non è ancora stato rilasciato: i lavori vengono creati al rilascio',
  );

  protected readonly status = orderStatus;
  protected readonly workStatus = workStatus;
  protected readonly qty = formatQty;
  protected readonly unit = unitLabel;
  protected readonly date = formatDate;
  protected readonly minutes = formatMinutes;
  protected readonly actions = ACTIONS;

  protected readonly canEdit = computed(() => {
    const o = this.order();
    return !!o && canEditOrder(o);
  });

  private readonly expanded = signal<ExpandedState>({});

  protected readonly table = injectTable(() => ({
    features,
    columns,
    data: this.order()?.children ?? [],
    getSubRows: (r: ProductionOrderTreeModel) => r.children,
    autoResetExpanded: false,
    state: { expanded: this.expanded() },
    onExpandedChange: (updater) => {
      this.expanded.set(typeof updater === 'function' ? updater(this.expanded()) : updater);
    },
    getRowId: (r: ProductionOrderTreeModel) => String(r.id),
  }));

  constructor() {
    void this.calendars.loadTimeZone();
    effect(() => {
      const id = this.orderId();
      untracked(() => {
        this.order.set(null);
        this.expanded.set({});
        this.userTab.set(null);
        if (id) void this.load(id);
      });
    });
  }

  protected openWork(): void {
    void this.router.navigate(['/work-orders'], { queryParams: { order: this.orderId() } });
  }

  protected can(action: string): boolean {
    return !!this.order()?.allowedActions.includes(action);
  }

  /** La colonna compare solo se almeno un lavoro ha una macchina (fasi senza macchine: nessun rumore). */
  protected hasMachines(works: readonly { assignedEquipment?: unknown; currentEquipment?: unknown }[]): boolean {
    return works.some((w) => !!w.assignedEquipment || !!w.currentEquipment);
  }

  protected links(items: WorkOrderLinkModel[]): string {
    return items.length ? items.map((i) => i.code).join(', ') : '—';
  }

  protected reloadAll(): void {
    this.refreshTick.update((n) => n + 1);
    this.reload();
  }

  protected openLockAll(mode: 'lock' | 'unlock'): void {
    this.includeChildren.set(false);
    this.lockAllMode.set(mode);
  }

  protected async runLockAll(id: number, mode: 'lock' | 'unlock'): Promise<void> {
    this.lockAllBusy.set(true);
    try {
      const params = { includeChildren: this.includeChildren() };
      if (mode === 'lock') {
        const r = await firstValueFrom(this.ordersApi.lockAllProductionOrderWorkOrders(id, params));
        this.lockAllMode.set(null);
        if (r.skipped.length) this.lockResult.set(r);
        else toast.success(`${r.locked} ${r.locked === 1 ? 'lavoro fissato' : 'lavori fissati'}`);
      } else {
        const r = await firstValueFrom(this.ordersApi.unlockAllProductionOrderWorkOrders(id, params));
        this.lockAllMode.set(null);
        toast.success(`${r.unlocked} ${r.unlocked === 1 ? 'lavoro sbloccato' : 'lavori sbloccati'}`);
      }
      this.reloadAll();
    } catch (e) {
      toast.error(toFailure(e).message);
    } finally {
      this.lockAllBusy.set(false);
    }
  }

  protected async removePlan(id: number): Promise<void> {
    this.removing.set(true);
    try {
      const r = await firstValueFrom(this.planning.removeOrderPlan(id));
      toast.success(`Piano rimosso: ${r.clearedWorkOrders} ${r.clearedWorkOrders === 1 ? 'lavoro' : 'lavori'} non più pianificati`);
      const kept = r.keptLocked ?? [];
      if (kept.length) {
        toast.warning(`${kept.length} ${kept.length === 1 ? 'lavoro fissato non è stato toccato' : 'lavori fissati non sono stati toccati'}: ${kept.join(', ')}`, {
          description: 'Sbloccali dalla pagina Lavori per poterli rimuovere dal piano.',
        });
      }
      this.removeOpen.set(false);
      this.reloadAll();
    } catch (e) {
      toast.error(toFailure(e).message);
    } finally {
      this.removing.set(false);
    }
  }

  protected reload(): void {
    void this.load(this.orderId());
  }

  private async load(id: number): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    this.notFound.set(false);
    try {
      this.order.set(await this.store.get(id));
    } catch (e) {
      const f = toFailure(e);
      if (f.status === 404) this.notFound.set(true);
      else this.loadError.set(f.message);
    } finally {
      this.loading.set(false);
    }
  }
}
