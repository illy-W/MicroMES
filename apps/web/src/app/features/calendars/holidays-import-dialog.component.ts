import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { toast } from '@spartan-ng/brain/sonner';
import type { ItalianHolidaysPreviewModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { toFailure } from '../materials/materials.service';
import { formatDate } from './calendar-time';
import { CalendarsStore } from './calendars.store';

@Component({
  selector: 'app-holidays-import-dialog',
  imports: [HlmDialogImports, HlmButton, HlmCheckbox, HlmInput, HlmLabel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="max-h-[92vh] overflow-y-auto sm:max-w-lg">
          <div class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>Importa festività italiane</h2>
              <p hlmDialogDescription>Scegli l’anno, controlla l’elenco e deseleziona le festività che non vuoi come chiusura. Pasqua e Pasquetta sono calcolate.</p>
            </hlm-dialog-header>

            <form class="flex items-end gap-3" (submit)="$event.preventDefault(); loadPreview()">
              <div class="grid gap-1.5">
                <label hlmLabel for="hol-year">Anno</label>
                <input hlmInput id="hol-year" class="h-8 w-28" type="number" min="2000" max="2100" [value]="year()" (input)="year.set(+$any($event.target).value)" />
              </div>
              <button hlmBtn size="sm" type="submit" variant="outline" [disabled]="loading() || !validYear()">{{ loading() ? 'Caricamento…' : 'Mostra festività' }}</button>
            </form>

            @if (error()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">{{ error() }}</p>
            }

            @if (preview(); as p) {
              <fieldset>
                <legend class="mb-2 text-sm font-medium">Festività {{ p.year }}</legend>
                <ul class="max-h-72 overflow-auto rounded-md border">
                  @for (h of p.holidays; track h.date) {
                    <li class="hover:bg-muted flex items-center gap-2 px-3 py-2">
                      <hlm-checkbox
                        [inputId]="'hol-' + h.date"
                        [disabled]="h.alreadyPresent"
                        [checked]="selected().has(h.date)"
                        (checkedChange)="toggle(h.date, $event)"
                      />
                      <label hlmLabel [for]="'hol-' + h.date" class="flex-1 cursor-pointer" [class.text-muted-foreground]="h.alreadyPresent">
                        <span class="tabular-nums">{{ formatDate(h.date) }}</span>
                        <span> {{ h.name }}</span>
                        @if (h.alreadyPresent) {
                          <span class="text-muted-foreground text-xs"> — già presente</span>
                        }
                      </label>
                    </li>
                  }
                </ul>
                <p class="text-muted-foreground mt-2 text-xs" aria-live="polite">{{ selected().size }} selezionate su {{ importable() }} importabili.</p>
              </fieldset>
            }

            <hlm-dialog-footer>
              <button hlmBtn type="button" variant="outline" hlmDialogClose>Annulla</button>
              <button hlmBtn type="button" [disabled]="importing() || !selected().size" (click)="confirm()">
                {{ importing() ? 'Importazione…' : 'Importa' + (selected().size ? ' (' + selected().size + ')' : '') }}
              </button>
            </hlm-dialog-footer>
          </div>
        </hlm-dialog-content>
      </ng-template>
    </hlm-dialog>
  `,
})
export class HolidaysImportDialogComponent implements OnInit {
  private readonly store = inject(CalendarsStore);

  readonly calendarId = input.required<number>();
  readonly defaultYear = input<number>(new Date().getFullYear());
  readonly imported = output<void>();
  readonly closed = output<void>();

  protected readonly year = signal(new Date().getFullYear());
  protected readonly preview = signal<ItalianHolidaysPreviewModel | null>(null);
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly loading = signal(false);
  protected readonly importing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly formatDate = formatDate;

  protected readonly validYear = computed(() => Number.isInteger(this.year()) && this.year() >= 2000 && this.year() <= 2100);
  protected readonly importable = computed(() => this.preview()?.holidays.filter((h) => !h.alreadyPresent).length ?? 0);

  ngOnInit(): void {
    this.year.set(this.defaultYear());
    void this.loadPreview();
  }

  protected toggle(date: string, on: boolean): void {
    const next = new Set(this.selected());
    if (on) next.add(date);
    else next.delete(date);
    this.selected.set(next);
  }

  protected async loadPreview(): Promise<void> {
    if (!this.validYear()) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const p = await this.store.holidaysPreview(this.calendarId(), this.year());
      this.preview.set(p);
      this.selected.set(new Set(p.holidays.filter((h) => !h.alreadyPresent).map((h) => h.date)));
    } catch (e) {
      this.preview.set(null);
      this.selected.set(new Set());
      this.error.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  protected async confirm(): Promise<void> {
    const p = this.preview();
    if (!p) return;
    this.importing.set(true);
    this.error.set(null);
    try {
      const r = await this.store.importHolidays(this.calendarId(), { year: p.year, dates: [...this.selected()] });
      toast.success(`Festività importate: ${r.created} create, ${r.skipped} saltate`);
      this.imported.emit();
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.importing.set(false);
    }
  }
}
