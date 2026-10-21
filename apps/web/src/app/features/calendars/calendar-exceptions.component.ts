import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarPlus, lucideCalendarSearch, lucidePencil, lucideRepeat, lucideTrash2, lucideTriangleAlert } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import type { CalendarExceptionModel } from '@micromes/api-client/models';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { toFailure } from '../materials/materials.service';
import { formatDate, slotModelsToRanges, summarizeRanges } from './calendar-time';
import { CalendarsStore } from './calendars.store';
import { ExceptionFormDialogComponent, KIND_LABELS } from './exception-form-dialog.component';
import { HolidaysImportDialogComponent } from './holidays-import-dialog.component';

const ALL_YEARS = 0;

@Component({
  selector: 'app-calendar-exceptions',
  imports: [
    HlmAlertDialogImports,
    HlmBadge,
    HlmButton,
    HlmEmptyImports,
    HlmLabel,
    HlmSelectImports,
    HlmSkeleton,
    HlmTableImports,
    NgIcon,
    ExceptionFormDialogComponent,
    HolidaysImportDialogComponent,
  ],
  providers: [provideIcons({ lucideCalendarPlus, lucideCalendarSearch, lucidePencil, lucideRepeat, lucideTrash2, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="grid gap-1.5">
          <label hlmLabel for="exc-year">Anno</label>
          <hlm-select [value]="year()" [itemToString]="yearLabel" (valueChange)="year.set($event ?? ALL)">
            <hlm-select-trigger id="exc-year" size="sm" class="w-40" aria-label="Filtra le eccezioni per anno">
              <hlm-select-value />
            </hlm-select-trigger>
            <hlm-select-content *hlmSelectPortal>
              <hlm-select-item [value]="ALL">Tutti gli anni</hlm-select-item>
              @for (y of years(); track y) {
                <hlm-select-item [value]="y">{{ y }}</hlm-select-item>
              }
            </hlm-select-content>
          </hlm-select>
        </div>
        <div class="ml-auto flex gap-2">
          @if (!isEquipment()) {
            <button hlmBtn size="sm" variant="outline" type="button" (click)="importOpen.set(true)">Importa festività italiane</button>
          }
          <button hlmBtn size="sm" type="button" (click)="openCreate()"><ng-icon name="lucideCalendarPlus" /> Nuova eccezione</button>
        </div>
      </div>

      @if (notice(); as n) {
        <div class="border-primary/40 bg-primary/10 flex items-start gap-2 rounded-md border px-3 py-2 text-sm" role="status">
          <ng-icon name="lucideTriangleAlert" class="mt-0.5 shrink-0" aria-hidden="true" />
          <div>
            <p class="font-medium">Eccezione salvata, ma si sovrappone a eccezioni già presenti:</p>
            <ul class="list-disc pl-5">
              @for (o of n; track o.id) {
                <li>{{ period(o.fromDate, o.toDate) }} — {{ kindLabels[o.kind] }}: {{ o.reason }}</li>
              }
            </ul>
            <p class="text-muted-foreground text-xs">Non è un errore: in caso di sovrapposizione prevale la più recente.</p>
          </div>
          <button hlmBtn class="ml-auto" size="sm" variant="ghost" type="button" (click)="notice.set(null)">Chiudi</button>
        </div>
      }

      @if (loading() && !items().length) {
        <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento eccezioni">
          @for (i of [1, 2, 3, 4]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (loadError()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare le eccezioni</div>
            <div hlmEmptyDescription>{{ loadError() }}</div>
          </hlm-empty-header>
          <hlm-empty-content><button hlmBtn type="button" (click)="reload()">Riprova</button></hlm-empty-content>
        </hlm-empty>
      } @else if (!items().length) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideCalendarSearch" /></hlm-empty-media>
            <div hlmEmptyTitle>Nessuna eccezione{{ year() === ALL ? '' : ' nel ' + year() }}</div>
            <div hlmEmptyDescription>{{ isEquipment() ? 'Aggiungi un fermo macchina (manutenzione, guasto) o un orario speciale.' : 'Aggiungi chiusure, ferie o orari speciali, oppure importa le festività italiane.' }}</div>
          </hlm-empty-header>
        </hlm-empty>
      } @else {
        <div class="max-h-[600px] w-full overflow-auto rounded-md border">
          <div hlmTableContainer>
            <table hlmTable>
              <thead hlmTHead class="bg-muted sticky top-0 z-10">
                <tr hlmTr>
                  <th hlmTh>Periodo</th>
                  <th hlmTh>Tipo</th>
                  <th hlmTh>Motivo</th>
                  <th hlmTh>Ogni anno</th>
                  <th hlmTh>Fasce</th>
                  <th hlmTh><span class="sr-only">Azioni</span></th>
                </tr>
              </thead>
              <tbody hlmTBody>
                @for (x of items(); track x.id) {
                  <tr hlmTr>
                    <td hlmTd class="font-medium tabular-nums">{{ period(x.fromDate, x.toDate) }}</td>
                    <td hlmTd>
                      <span hlmBadge [variant]="x.kind === 'Closed' ? 'secondary' : 'outline'">{{ kindLabels[x.kind] }}</span>
                    </td>
                    <td hlmTd>{{ x.reason }}</td>
                    <td hlmTd>
                      @if (x.isRecurringYearly) {
                        <span class="inline-flex items-center gap-1"><ng-icon name="lucideRepeat" aria-hidden="true" /> Sì</span>
                      } @else {
                        <span class="text-muted-foreground">No</span>
                      }
                    </td>
                    <td hlmTd class="tabular-nums">
                      @if (x.kind === 'Custom') {
                        {{ summarize(x) }}
                      } @else {
                        <span class="text-muted-foreground">Chiuso</span>
                      }
                    </td>
                    <td hlmTd class="text-right whitespace-nowrap">
                      <button hlmBtn variant="ghost" size="icon-sm" type="button" [attr.aria-label]="'Modifica eccezione ' + x.reason" (click)="openEdit(x)"><ng-icon name="lucidePencil" /></button>
                      <button hlmBtn variant="ghost" size="icon-sm" type="button" [attr.aria-label]="'Elimina eccezione ' + x.reason" (click)="askDelete(x)"><ng-icon name="lucideTrash2" /></button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
        <span class="text-muted-foreground text-sm tabular-nums" aria-live="polite">{{ items().length }} {{ items().length === 1 ? 'eccezione' : 'eccezioni' }}</span>
      }
    </div>

    @if (formOpen()) {
      <app-exception-form-dialog [calendarId]="calendarId()" [owner]="owner()" [exception]="editing()" (saved)="onSaved($event)" (closed)="formOpen.set(false)" />
    }
    @if (importOpen()) {
      <app-holidays-import-dialog [calendarId]="calendarId()" [defaultYear]="year() === ALL ? thisYear : year()" (imported)="onImported()" (closed)="importOpen.set(false)" />
    }

    @if (toDelete(); as x) {
      <hlm-alert-dialog state="open" (closed)="toDelete.set(null)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>Eliminare l’eccezione?</h2>
              <p hlmAlertDialogDescription>{{ period(x.fromDate, x.toDate) }} — {{ x.reason }}. I calcoli torneranno a seguire l’orario settimanale in quei giorni.</p>
            </hlm-alert-dialog-header>
            @if (deleteError()) {
              <p class="text-destructive text-sm" role="alert">{{ deleteError() }}</p>
            }
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Annulla</button>
              <button hlmBtn type="button" variant="destructive" [disabled]="deleting()" (click)="confirmDelete(x)">Elimina</button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }
  `,
})
export class CalendarExceptionsComponent {
  private readonly store = inject(CalendarsStore);

  /** Id del calendario, o della macchina se `owner` è «equipment». */
  readonly calendarId = input.required<number>();
  readonly owner = input<'calendar' | 'equipment'>('calendar');
  protected readonly isEquipment = computed(() => this.owner() === 'equipment');

  protected readonly ALL = ALL_YEARS;
  protected readonly kindLabels = KIND_LABELS;
  protected readonly thisYear = new Date().getFullYear();
  protected readonly year = signal(this.thisYear);
  protected readonly years = computed(() => Array.from({ length: 7 }, (_, i) => this.thisYear - 2 + i));
  protected readonly yearLabel = (y: number | null): string => (!y ? 'Tutti gli anni' : String(y));

  protected readonly items = signal<CalendarExceptionModel[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly notice = signal<CalendarExceptionModel['overlaps'] | null>(null);

  protected readonly formOpen = signal(false);
  protected readonly importOpen = signal(false);
  protected readonly editing = signal<CalendarExceptionModel | null>(null);
  protected readonly toDelete = signal<CalendarExceptionModel | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  private seq = 0;

  constructor() {
    effect(() => {
      this.calendarId();
      this.year();
      untracked(() => {
        this.notice.set(null);
        void this.reload();
      });
    });
  }

  protected period(from: string, to: string): string {
    return from === to ? formatDate(from) : `${formatDate(from)} – ${formatDate(to)}`;
  }

  protected summarize(x: CalendarExceptionModel): string {
    return summarizeRanges(slotModelsToRanges(x.slots));
  }

  protected async reload(): Promise<void> {
    const n = ++this.seq;
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const r = await (this.isEquipment() ? this.store.equipmentExceptions(this.calendarId(), this.year() || undefined) : this.store.exceptions(this.calendarId(), this.year() || undefined));
      if (n === this.seq) this.items.set(r);
    } catch (e) {
      if (n === this.seq) {
        this.items.set([]);
        this.loadError.set(toFailure(e).message);
      }
    } finally {
      if (n === this.seq) this.loading.set(false);
    }
  }

  protected openCreate(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }

  protected openEdit(x: CalendarExceptionModel): void {
    this.editing.set(x);
    this.formOpen.set(true);
  }

  protected onSaved(x: CalendarExceptionModel): void {
    this.formOpen.set(false);
    this.notice.set(x.overlaps.length ? x.overlaps : null);
    toast.success('Eccezione salvata');
    void this.reload();
  }

  protected onImported(): void {
    this.importOpen.set(false);
    void this.reload();
  }

  protected askDelete(x: CalendarExceptionModel): void {
    this.deleteError.set(null);
    this.toDelete.set(x);
  }

  protected async confirmDelete(x: CalendarExceptionModel): Promise<void> {
    this.deleting.set(true);
    this.deleteError.set(null);
    try {
      await (this.isEquipment() ? this.store.deleteEquipmentException(this.calendarId(), x.id) : this.store.deleteException(this.calendarId(), x.id));
      this.toDelete.set(null);
      toast.success('Eccezione eliminata');
      await this.reload();
    } catch (e) {
      this.deleteError.set(toFailure(e).message);
    } finally {
      this.deleting.set(false);
    }
  }
}
