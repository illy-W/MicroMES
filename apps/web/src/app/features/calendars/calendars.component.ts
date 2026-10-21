import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarDays, lucideCircleCheck, lucideCircleOff, lucidePlus, lucideSearch, lucideSearchX, lucideStar, lucideTriangleAlert } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import type { WorkCalendarModel } from '@micromes/api-client/models';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { toFailure } from '../materials/materials.service';
import { CalendarDetailComponent } from './calendar-detail.component';
import { CalendarDuplicateDialogComponent } from './calendar-duplicate-dialog.component';
import { CalendarFormDialogComponent } from './calendar-form-dialog.component';
import { CalendarRowActionsComponent } from './calendar-row-actions.component';
import { formatHours } from './calendar-time';
import { CalendarsStore } from './calendars.store';

@Component({
  selector: 'app-calendars',
  imports: [
    HlmAlertDialogImports, HlmBadge, HlmButton, HlmEmptyImports, HlmInput, HlmLabel, HlmSkeleton, HlmSwitch, HlmTableImports, NgIcon,
    CalendarDetailComponent, CalendarDuplicateDialogComponent, CalendarFormDialogComponent, CalendarRowActionsComponent,
  ],
  providers: [provideIcons({ lucideCalendarDays, lucideCircleCheck, lucideCircleOff, lucidePlus, lucideSearch, lucideSearchX, lucideStar, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      @if (selected(); as cal) {
        <app-calendar-detail [calendar]="cal" (back)="selectedId.set(null)" (edit)="openEdit($event)" />
      } @else {
        <div class="flex flex-col gap-1">
          <h1 class="text-xl font-semibold">Calendari</h1>
          <p class="text-muted-foreground text-sm">Orari di lavoro settimanali e relative eccezioni (festività, ferie, orari speciali). Il calendario predefinito vale per le macchine che non ne hanno uno proprio.</p>
        </div>

        <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
          <div class="relative">
            <ng-icon name="lucideSearch" class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2" aria-hidden="true" />
            <input hlmInput class="h-8 w-80 pl-8" type="search" placeholder="Cerca per codice, nome o descrizione" aria-label="Cerca calendari" [value]="search()" (input)="search.set($any($event.target).value)" />
          </div>
          <div class="flex items-center gap-2">
            <hlm-switch inputId="show-inactive-cal" [checked]="showInactive()" (checkedChange)="showInactive.set($event)" />
            <label hlmLabel for="show-inactive-cal">Mostra disattivati</label>
          </div>
          <button hlmBtn size="sm" class="ml-auto" type="button" (click)="openCreate()"><ng-icon name="lucidePlus" /> Nuovo calendario</button>
        </div>

        @if (store.loading() && !store.items().length) {
          <div class="flex flex-col gap-2" aria-busy="true" aria-label="Caricamento calendari">
            @for (i of [1, 2, 3]; track i) { <hlm-skeleton class="h-10 w-full" /> }
          </div>
        } @else if (store.loadError()) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
              <div hlmEmptyTitle>Impossibile caricare i calendari</div>
              <div hlmEmptyDescription>{{ store.loadError() }}</div>
            </hlm-empty-header>
            <hlm-empty-content><button hlmBtn type="button" (click)="store.load()">Riprova</button></hlm-empty-content>
          </hlm-empty>
        } @else if (!filtered().length) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon [name]="store.items().length ? 'lucideSearchX' : 'lucideCalendarDays'" /></hlm-empty-media>
              <div hlmEmptyTitle>{{ store.items().length ? 'Nessun risultato' : 'Nessun calendario' }}</div>
              <div hlmEmptyDescription>{{ store.items().length ? 'Nessun calendario corrisponde ai filtri.' : 'Crea il primo calendario di lavoro.' }}</div>
            </hlm-empty-header>
          </hlm-empty>
        } @else {
          <div class="w-full overflow-auto rounded-md border">
            <div hlmTableContainer>
              <table hlmTable>
                <thead hlmTHead class="bg-muted sticky top-0 z-10">
                  <tr hlmTr>
                    <th hlmTh>Codice</th>
                    <th hlmTh>Nome</th>
                    <th hlmTh>Ore settimanali</th>
                    <th hlmTh>Predefinito</th>
                    <th hlmTh>Stato</th>
                    <th hlmTh><span class="sr-only">Azioni</span></th>
                  </tr>
                </thead>
                <tbody hlmTBody>
                  @for (c of filtered(); track c.id) {
                    <tr hlmTr [class.text-muted-foreground]="!c.isActive">
                      <td hlmTd class="font-medium">{{ c.code }}</td>
                      <td hlmTd>
                        <button type="button" class="hover:underline focus-visible:ring-ring/50 rounded-sm text-left outline-none focus-visible:ring-[3px]" (click)="selectedId.set(c.id)">{{ c.name }}</button>
                      </td>
                      <td hlmTd class="tabular-nums">{{ hours(c.weeklyHours) }}</td>
                      <td hlmTd>
                        @if (c.isDefault) {
                          <span hlmBadge variant="outline" class="border-primary/40 bg-primary/10 text-primary gap-1 font-semibold">
                            <ng-icon name="lucideStar" class="size-3 fill-current" aria-hidden="true" /> Predefinito
                          </span>
                        } @else {
                          <span class="text-muted-foreground">—</span>
                        }
                      </td>
                      <td hlmTd>
                        <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
                          @if (c.isActive) { <ng-icon name="lucideCircleCheck" /> Attivo } @else { <ng-icon name="lucideCircleOff" /> Disattivato }
                        </span>
                      </td>
                      <td hlmTd>
                        <app-calendar-row-actions [calendar]="c" (open)="selectedId.set($event.id)" (edit)="openEdit($event)" (duplicate)="duplicating.set($event)" (setDefault)="askDefault($event)" (deactivate)="askDeactivate($event)" />
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
          <span class="text-muted-foreground text-sm tabular-nums" aria-live="polite">{{ filtered().length }} {{ filtered().length === 1 ? 'calendario' : 'calendari' }}</span>
        }
      }
    </div>

    @if (formOpen()) {
      <app-calendar-form-dialog [calendar]="editing()" (saved)="onSaved($event)" (closed)="formOpen.set(false)" />
    }
    @if (duplicating(); as src) {
      <app-calendar-duplicate-dialog [source]="src" (saved)="onDuplicated($event)" (closed)="duplicating.set(null)" />
    }

    @if (confirm(); as c) {
      <hlm-alert-dialog state="open" (closed)="confirm.set(null)">
        <ng-template hlmAlertDialogPortal>
          <hlm-alert-dialog-content>
            <hlm-alert-dialog-header>
              <h2 hlmAlertDialogTitle>{{ c.kind === 'default' ? 'Impostare come predefinito?' : 'Disattivare il calendario?' }}</h2>
              <p hlmAlertDialogDescription>
                @if (c.kind === 'default') {
                  «{{ c.calendar.name }}» diventerà il calendario delle macchine che non ne hanno uno proprio. Il predefinito attuale resta disponibile.
                } @else {
                  «{{ c.calendar.name }}» non potrà più essere assegnato alle macchine. Non è possibile se è usato da macchine attive o se è il predefinito.
                }
              </p>
            </hlm-alert-dialog-header>
            @if (confirmError()) {
              <p class="text-destructive text-sm" role="alert">{{ confirmError() }}</p>
            }
            <hlm-alert-dialog-footer>
              <button hlmAlertDialogCancel>Annulla</button>
              <button hlmBtn type="button" [variant]="c.kind === 'default' ? 'default' : 'destructive'" [disabled]="confirming()" (click)="runConfirm(c)">
                {{ c.kind === 'default' ? 'Imposta' : 'Disattiva' }}
              </button>
            </hlm-alert-dialog-footer>
          </hlm-alert-dialog-content>
        </ng-template>
      </hlm-alert-dialog>
    }
  `,
})
export class CalendarsComponent implements OnInit {
  protected readonly store = inject(CalendarsStore);

  protected readonly search = signal('');
  protected readonly showInactive = signal(false);
  protected readonly selectedId = signal<number | null>(null);
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<WorkCalendarModel | null>(null);
  protected readonly duplicating = signal<WorkCalendarModel | null>(null);
  protected readonly confirm = signal<{ kind: 'default' | 'deactivate'; calendar: WorkCalendarModel } | null>(null);
  protected readonly confirming = signal(false);
  protected readonly confirmError = signal<string | null>(null);

  protected readonly selected = computed(() => this.store.items().find((c) => c.id === this.selectedId()) ?? null);
  protected readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const inactive = this.showInactive();
    return this.store.items().filter(
      (c) => (inactive || c.isActive) && (!q || c.code.toLowerCase().includes(q) || c.name.toLowerCase().includes(q) || (c.description ?? '').toLowerCase().includes(q)),
    );
  });

  ngOnInit(): void {
    void this.store.load();
    void this.store.loadTimeZone();
  }

  protected hours(h: number): string {
    return formatHours(h * 60);
  }

  protected openCreate(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }
  protected openEdit(c: WorkCalendarModel): void {
    this.editing.set(c);
    this.formOpen.set(true);
  }
  protected async onSaved(c: WorkCalendarModel): Promise<void> {
    this.formOpen.set(false);
    toast.success(`Calendario ${c.code} salvato`);
    await this.store.load();
  }
  protected async onDuplicated(c: WorkCalendarModel): Promise<void> {
    this.duplicating.set(null);
    toast.success(`Calendario ${c.code} creato`);
    await this.store.load();
  }
  protected askDefault(c: WorkCalendarModel): void {
    this.confirmError.set(null);
    this.confirm.set({ kind: 'default', calendar: c });
  }
  protected askDeactivate(c: WorkCalendarModel): void {
    this.confirmError.set(null);
    this.confirm.set({ kind: 'deactivate', calendar: c });
  }
  protected async runConfirm(c: { kind: 'default' | 'deactivate'; calendar: WorkCalendarModel }): Promise<void> {
    this.confirming.set(true);
    this.confirmError.set(null);
    try {
      if (c.kind === 'default') await this.store.setDefault(c.calendar.id);
      else await this.store.deactivate(c.calendar.id);
      this.confirm.set(null);
      toast.success(c.kind === 'default' ? `${c.calendar.code} è ora il calendario predefinito` : `Calendario ${c.calendar.code} disattivato`);
      await this.store.load();
    } catch (e) {
      this.confirmError.set(toFailure(e).message);
    } finally {
      this.confirming.set(false);
    }
  }
}
