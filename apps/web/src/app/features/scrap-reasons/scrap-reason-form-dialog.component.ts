import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { ScrapReasonModel } from '@micromes/api-client/models';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { toFailure } from '../materials/materials.service';
import { ProcessSegmentsStore } from '../process-segments/process-segments.store';
import { ScrapReasonsStore } from './scrap-reasons.store';

@Component({
  selector: 'app-scrap-reason-form-dialog',
  imports: [ReactiveFormsModule, HlmDialogImports, HlmButton, HlmCheckbox, HlmInput, HlmLabel, HlmSwitch, HlmTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog state="open" (closed)="closed.emit()">
      <ng-template hlmDialogPortal>
        <hlm-dialog-content class="sm:max-w-lg">
          <form [formGroup]="form" (ngSubmit)="submit()" class="grid gap-4">
            <hlm-dialog-header>
              <h2 hlmDialogTitle>{{ reason() ? 'Modifica motivo di scarto' : 'Nuovo motivo di scarto' }}</h2>
              <p hlmDialogDescription>I campi contrassegnati con * sono obbligatori.</p>
            </hlm-dialog-header>

            @if (formError()) {
              <p class="text-destructive border-destructive/40 rounded-md border px-3 py-2 text-sm" role="alert">
                {{ formError() }}
              </p>
            }

            <div class="grid gap-1.5">
              <label hlmLabel for="sr-code">Codice *</label>
              <input hlmInput id="sr-code" formControlName="code" maxlength="50" [attr.aria-invalid]="fieldError('code') ? true : null" [attr.aria-describedby]="fieldError('code') ? 'sr-code-err' : null" />
              @if (fieldError('code'); as e) {
                <p id="sr-code-err" class="text-destructive text-xs">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="sr-name">Nome *</label>
              <input hlmInput id="sr-name" formControlName="name" maxlength="200" [attr.aria-invalid]="fieldError('name') ? true : null" [attr.aria-describedby]="fieldError('name') ? 'sr-name-err' : null" />
              @if (fieldError('name'); as e) {
                <p id="sr-name-err" class="text-destructive text-xs">{{ e }}</p>
              }
            </div>
            <div class="grid gap-1.5">
              <label hlmLabel for="sr-desc">Descrizione</label>
              <textarea hlmTextarea id="sr-desc" formControlName="description" maxlength="1000"></textarea>
              @if (fieldError('description'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </div>

            <fieldset class="grid gap-2">
              <legend class="text-sm font-medium">Fasi di processo</legend>
              <p class="text-muted-foreground text-xs" id="sr-phases-hint">
                Nessuna fase selezionata = il motivo vale per tutte le fasi.
                @if (selected().size) {
                  Selezionate: {{ selected().size }}.
                }
              </p>
              <input
                hlmInput
                class="h-8"
                type="search"
                placeholder="Cerca fase"
                aria-label="Cerca fase di processo"
                [value]="segmentSearch()"
                (input)="segmentSearch.set($any($event.target).value)"
              />
              @if (segments.loadError()) {
                <p class="text-destructive text-xs" role="alert">{{ segments.loadError() }}</p>
              }
              <ul class="max-h-48 overflow-auto rounded-md border" aria-describedby="sr-phases-hint">
                @for (s of visibleSegments(); track s.id) {
                  <li class="hover:bg-muted flex items-center gap-2 px-3 py-2">
                    <hlm-checkbox [inputId]="'sr-seg-' + s.id" [checked]="selected().has(s.id)" (checkedChange)="toggle(s.id, $event)" />
                    <label hlmLabel [for]="'sr-seg-' + s.id" class="flex-1 cursor-pointer">
                      <span class="font-medium">{{ s.code }}</span>
                      <span class="text-muted-foreground"> {{ s.name }}{{ s.isActive ? '' : ' (disattivata)' }}</span>
                    </label>
                  </li>
                } @empty {
                  <li class="text-muted-foreground px-3 py-2 text-sm">Nessuna fase trovata.</li>
                }
              </ul>
              @if (fieldError('processSegmentIds'); as e) {
                <p class="text-destructive text-xs" role="alert">{{ e }}</p>
              }
            </fieldset>

            @if (reason()) {
              <div class="flex items-center gap-2">
                <hlm-switch inputId="sr-active" formControlName="isActive" />
                <label hlmLabel for="sr-active">Attivo (proposto negli scarti)</label>
              </div>
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
export class ScrapReasonFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly store = inject(ScrapReasonsStore);
  protected readonly segments = inject(ProcessSegmentsStore);

  readonly reason = input<ScrapReasonModel | null>(null);
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private readonly serverErrors = signal<Record<string, string>>({});
  protected readonly selected = signal<ReadonlySet<number>>(new Set());
  protected readonly segmentSearch = signal('');

  /** Fasi attive, più quelle già collegate anche se disattivate. */
  protected readonly visibleSegments = computed(() => {
    const q = this.segmentSearch().trim().toLowerCase();
    const sel = this.selected();
    return this.segments
      .segments()
      .filter((s) => (s.isActive || sel.has(s.id)) && (!q || s.code.toLowerCase().includes(q) || s.name.toLowerCase().includes(q)));
  });

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    isActive: [true],
  });

  ngOnInit(): void {
    void this.segments.load();
    const r = this.reason();
    if (r) {
      this.form.patchValue({ code: r.code, name: r.name, description: r.description ?? '', isActive: r.isActive });
      this.selected.set(new Set(r.processSegments.map((s) => s.id)));
    }
  }

  protected toggle(id: number, checked: boolean): void {
    const next = new Set(this.selected());
    if (checked) next.add(id);
    else next.delete(id);
    this.selected.set(next);
  }

  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !(c.touched || c.dirty)) return null;
    if (c.hasError('required')) return 'Campo obbligatorio';
    const max = c.getError('maxlength');
    if (max) return `Massimo ${max.requiredLength} caratteri`;
    return null;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const base = {
      code: v.code.trim().toUpperCase(),
      name: v.name.trim(),
      description: v.description.trim() || null,
      processSegmentIds: [...this.selected()],
    };
    this.saving.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    try {
      const r = this.reason();
      if (r) await this.store.update(r.id, { ...base, isActive: v.isActive });
      else await this.store.create(base);
      this.saved.emit();
    } catch (e) {
      const f = toFailure(e);
      if (f.status === 409) this.serverErrors.set({ code: f.message });
      else if (f.status === 422) {
        this.serverErrors.set(f.fieldErrors);
        if (!Object.keys(f.fieldErrors).length) this.formError.set(f.message);
      } else this.formError.set(f.message);
    } finally {
      this.saving.set(false);
    }
  }
}
