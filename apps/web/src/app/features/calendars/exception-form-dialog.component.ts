import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { provideBrnCalendarI18n } from '@spartan-ng/brain/calendar';
import type { CalendarExceptionKind, CalendarExceptionModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCalendarRange } from '@spartan-ng/helm/calendar';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { toFailure } from '../materials/materials.service';
import { formatDate, fromIsoDate, hasErrors, rangesToRequest, slotModelsToRanges, TimeRange, toIsoDate, validateRanges } from './calendar-time';
import { CalendarsStore } from './calendars.store';
import { TimeRangesEditorComponent } from './time-ranges-editor.component';

export const KIND_LABELS: Record<CalendarExceptionKind, string> = {
  Closed: 'Chiusura',
  Custom: 'Orario speciale',
};

const IT_MONTHS = Array.from({ length: 12 }, (_, m) => new Intl.DateTimeFormat('it-IT', { month: 'long' }).format(new Date(2000, m, 1)));
const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);
const IT_WEEKDAYS = [0, 1, 2, 3, 4, 5, 6].map((d) => new Intl.DateTimeFormat('it-IT', { weekday: 'short' }).format(new Date(2024, 0, 7 + d)));

@Component({
  selector: 'app-exception-form-dialog',
  imports: [HlmDialogImports, HlmButton, HlmCalendarRange, HlmCheckbox, HlmInput, HlmLabel, HlmSelectImports, TimeRangesEditorComponent],
  providers: [
    provideBrnCalendarI18n({
      firstDayOfWeek: () => 1,
      months: () => IT_MONTHS.map(cap) as never,
      formatMonth: (m) => cap(IT_MONTHS[m]),
      formatHeader: (m, y) => `${cap(IT_MONTHS[m])} ${y}`,
      // indice 0 = domenica (come i giorni JS); l'ordine di visualizzazione lo decide weekStartsOn
      formatWeekdayName: (i) => cap(IT_WEEKDAYS[i % 7]),
      labelWeekday: (i) => cap(IT_WEEKDAYS[i % 7]),
      labelPrevious: () => 'Mese precedente',
      labelNext: () => 'Mese successivo',
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
          <form (submit)="$event.preventDefault(); submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ exception() ? 'Modifica eccezione' : 'Nuova eccezione' }}</h2>
              <p hlmDialogDescription>
                @if (isEquipment()) {
                  Fermi macchina (manutenzione, guasto) o orari speciali della macchina: prevalgono sulle eccezioni del calendario.
                } @else {
                  Chiusure (festività, ferie) o orari speciali che sostituiscono l’orario settimanale nei giorni scelti.
                }
              </p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ formError() }}</p>
            }

            <div class="grid gap-4 sm:grid-cols-[auto_1fr]">
              <fieldset class="grid gap-1.5">
                <legend class="mb-1 text-sm font-medium">Periodo *</legend>
                <hlm-calendar-range
                  [startDate]="start()"
                  [endDate]="end()"
                  [weekStartsOn]="1"
                  [defaultFocusedDate]="start()"
                  (startDateChange)="start.set($event)"
                  (endDateChange)="end.set($event)"
                />
              </fieldset>
              <div class="grid content-start gap-4">
                <p class="text-sm" aria-live="polite">
                  @if (periodText(); as t) {
                    {{ t }}
                  } @else {
                    <span class="text-muted-foreground">Seleziona il primo e l’ultimo giorno (clicca due volte lo stesso giorno per un solo giorno).</span>
                  }
                </p>
                @if (submitted() && !start()) {
                  <p class="text-destructive text-xs" role="alert">Seleziona il periodo.</p>
                }
                <div class="grid gap-1.5">
                  <label hlmLabel for="ex-kind">Tipo *</label>
                  <hlm-select [value]="kind()" [itemToString]="kindLabel" (valueChange)="kind.set($event ?? 'Closed')">
                    <hlm-select-trigger id="ex-kind" class="w-full" aria-label="Tipo di eccezione">
                      <hlm-select-value />
                    </hlm-select-trigger>
                    <hlm-select-content *hlmSelectPortal>
                      <hlm-select-item value="Closed">{{ kindLabel('Closed') }}</hlm-select-item>
                      <hlm-select-item value="Custom">{{ kindLabel('Custom') }}</hlm-select-item>
                    </hlm-select-content>
                  </hlm-select>
                </div>
                <div class="grid gap-1.5">
                  <label hlmLabel for="ex-reason">Motivo *</label>
                  <input
                    hlmInput
                    id="ex-reason"
                    maxlength="200"
                    [value]="reason()"
                    (input)="reason.set($any($event.target).value)"
                    [attr.aria-invalid]="submitted() && !reason().trim() ? true : null"
                  />
                  @if (submitted() && !reason().trim()) {
                    <p class="text-destructive text-xs" role="alert">Campo obbligatorio</p>
                  }
                </div>
                @if (!isEquipment()) {
                <div class="flex items-center gap-2">
                  <hlm-checkbox inputId="ex-yearly" [disabled]="!singleDay()" [checked]="singleDay() && yearly()" (checkedChange)="yearly.set($event)" />
                  <label hlmLabel for="ex-yearly">Ricorrente ogni anno</label>
                </div>
                @if (!singleDay()) {
                  <p class="text-muted-foreground -mt-2 text-xs">Disponibile solo per un singolo giorno (es. festività fisse).</p>
                }
                }
              </div>
            </div>

            @if (kind() === 'Custom') {
              <fieldset class="grid gap-2">
                <legend class="mb-1 text-sm font-medium">Fasce orarie dei giorni selezionati *</legend>
                <app-time-ranges-editor label="Orario speciale" emptyText="Nessuna fascia: aggiungine almeno una." [(ranges)]="slots" />
                @if (submitted() && slotsError(); as e) {
                  <p class="text-destructive text-sm" role="alert">{{ e }}</p>
                }
              </fieldset>
            }

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="submit" [disabled]="saving()">{{ saving() ? 'Salvataggio…' : 'Salva' }}</button>
            </hlm-dialog-footer>
          </form>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class ExceptionFormDialogComponent implements OnInit {
  private readonly store = inject(CalendarsStore);

  /** Id del calendario, o della macchina se `owner` è «equipment». */
  readonly calendarId = input.required<number>();
  readonly owner = input<'calendar' | 'equipment'>('calendar');
  readonly exception = input<CalendarExceptionModel | null>(null);
  /** Anno suggerito per il mese iniziale del calendario. */
  readonly year = input<number>(new Date().getFullYear());
  readonly saved = output<CalendarExceptionModel>();
  readonly closed = output<void>();

  protected readonly start = signal<Date | undefined>(undefined);
  protected readonly end = signal<Date | undefined>(undefined);
  protected readonly kind = signal<CalendarExceptionKind>('Closed');
  protected readonly reason = signal('');
  protected readonly yearly = signal(false);
  protected readonly slots = signal<TimeRange[]>([]);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly isEquipment = computed(() => this.owner() === 'equipment');
  protected readonly kindLabel = (k: CalendarExceptionKind | null): string => (k ? KIND_LABELS[k] : '');

  protected readonly singleDay = computed(() => {
    const s = this.start();
    const e = this.end();
    return !!s && (!e || toIsoDate(s) === toIsoDate(e));
  });

  protected readonly periodText = computed(() => {
    const s = this.start();
    if (!s) return null;
    const e = this.end();
    return !e || toIsoDate(s) === toIsoDate(e) ? `Giorno: ${formatDate(toIsoDate(s))}` : `Dal ${formatDate(toIsoDate(s))} al ${formatDate(toIsoDate(e))}`;
  });

  protected readonly slotsError = computed(() => {
    if (this.kind() !== 'Custom') return null;
    const s = this.slots();
    if (!s.length) return 'Inserisci almeno una fascia oraria.';
    if (hasErrors(validateRanges(s))) return 'Correggi le fasce orarie evidenziate.';
    return null;
  });

  ngOnInit(): void {
    const x = this.exception();
    if (x) {
      this.start.set(fromIsoDate(x.fromDate));
      this.end.set(fromIsoDate(x.toDate));
      this.kind.set(x.kind);
      this.reason.set(x.reason);
      this.yearly.set(x.isRecurringYearly);
      this.slots.set(slotModelsToRanges(x.slots));
    }
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    const s = this.start();
    if (!s || !this.reason().trim() || this.slotsError()) return;
    const e = this.end() ?? s;
    const req = {
      fromDate: toIsoDate(s),
      toDate: toIsoDate(e),
      kind: this.kind(),
      isRecurringYearly: !this.isEquipment() && this.singleDay() && this.yearly(),
      reason: this.reason().trim(),
      slots: this.kind() === 'Custom' ? rangesToRequest(this.slots()) : [],
    };
    this.saving.set(true);
    this.formError.set(null);
    try {
      const x = this.exception();
      const id = this.calendarId();
      const eq = this.isEquipment();
      this.saved.emit(
        x
          ? await (eq ? this.store.updateEquipmentException(id, x.id, req) : this.store.updateException(id, x.id, req))
          : await (eq ? this.store.createEquipmentException(id, req) : this.store.createException(id, req)),
      );
    } catch (err) {
      this.formError.set(toFailure(err).message);
    } finally {
      this.saving.set(false);
    }
  }
}
