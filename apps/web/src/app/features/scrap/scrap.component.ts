import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChartNoAxesColumn, lucideTriangleAlert } from '@ng-icons/lucide';
import type { ScrapReportModel } from '@micromes/api-client/models';
import { ScrapService } from '@micromes/api-client/services/scrap';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmProgressImports } from '@spartan-ng/helm/progress';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';
import { formatQty } from '../production-orders/production-order-labels';

interface Bar {
  key: string;
  label: string;
  detail: string;
  /** 0..100, larghezza della barra. */
  pct: number;
  value: string;
  highlight?: boolean;
}

const pad = (n: number) => String(n).padStart(2, '0');
const toDateInput = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const daysAgo = (n: number) => {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return d;
};
const pctFmt = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 1, style: 'percent' });

const PRESETS = [
  { id: '7', label: 'Ultimi 7 giorni' },
  { id: '30', label: 'Ultimi 30 giorni' },
  { id: '90', label: 'Ultimi 90 giorni' },
  { id: 'month', label: 'Mese corrente' },
] as const;

function pieces(n: number): string {
  return n === 1 ? 'pezzo' : 'pezzi';
}

@Component({
  selector: 'app-scrap',
  imports: [NgTemplateOutlet, HlmButton, HlmEmptyImports, HlmInput, HlmLabel, HlmProgressImports, HlmSkeleton, NgIcon],
  providers: [provideIcons({ lucideChartNoAxesColumn, lucideTriangleAlert })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4 p-4">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold">Scarti</h1>
        <p class="text-muted-foreground text-sm">Dove si scarta di più nel periodo, per motivo, fase e materiale.</p>
      </div>

      <div class="flex flex-wrap items-end gap-x-4 gap-y-3">
        <div class="grid gap-1.5">
          <label hlmLabel for="sc-from">Dal</label>
          <input hlmInput id="sc-from" class="h-8" type="date" [value]="from()" [max]="to()" (change)="setFrom($any($event.target).value)" />
        </div>
        <div class="grid gap-1.5">
          <label hlmLabel for="sc-to">Al</label>
          <input hlmInput id="sc-to" class="h-8" type="date" [value]="to()" [min]="from()" (change)="setTo($any($event.target).value)" />
        </div>
        <div class="flex flex-wrap gap-2" role="group" aria-label="Periodi predefiniti">
          @for (p of presets; track p.id) {
            <button hlmBtn size="sm" type="button" [variant]="preset() === p.id ? 'secondary' : 'outline'" [attr.aria-pressed]="preset() === p.id" (click)="applyPreset(p.id)">
              {{ p.label }}
            </button>
          }
        </div>
      </div>

      @if (loading() && !report()) {
        <div class="grid gap-3" aria-busy="true" aria-label="Caricamento report scarti">
          <hlm-skeleton class="h-20 w-full" />
          <hlm-skeleton class="h-48 w-full" />
        </div>
      } @else if (error()) {
        <hlm-empty>
          <hlm-empty-header>
            <hlm-empty-media variant="icon"><ng-icon name="lucideTriangleAlert" /></hlm-empty-media>
            <div hlmEmptyTitle>Impossibile caricare il report</div>
            <div hlmEmptyDescription>{{ error() }}</div>
          </hlm-empty-header>
          <hlm-empty-content><button hlmBtn type="button" (click)="load()">Riprova</button></hlm-empty-content>
        </hlm-empty>
      } @else if (report(); as r) {
        @if (!r.goodQuantity && !r.scrapQuantity) {
          <hlm-empty>
            <hlm-empty-header>
              <hlm-empty-media variant="icon"><ng-icon name="lucideChartNoAxesColumn" /></hlm-empty-media>
              <div hlmEmptyTitle>Nessuna produzione nel periodo</div>
              <div hlmEmptyDescription>Prova un periodo più ampio.</div>
            </hlm-empty-header>
            <hlm-empty-content>
              <button hlmBtn type="button" variant="outline" (click)="applyPreset('90')">Ultimi 90 giorni</button>
            </hlm-empty-content>
          </hlm-empty>
        } @else {
          <dl class="grid grid-cols-1 gap-3 sm:grid-cols-3" [attr.aria-busy]="loading()">
            <div class="rounded-md border p-4">
              <dt class="text-muted-foreground text-xs">Pezzi buoni</dt>
              <dd class="text-2xl font-semibold tabular-nums">{{ qty(r.goodQuantity) }}</dd>
            </div>
            <div class="rounded-md border p-4">
              <dt class="text-muted-foreground text-xs">Pezzi scartati</dt>
              <dd class="text-2xl font-semibold tabular-nums">{{ qty(r.scrapQuantity) }}</dd>
            </div>
            <div class="rounded-md border p-4">
              <dt class="text-muted-foreground text-xs">Percentuale di scarto</dt>
              <dd class="text-2xl font-semibold tabular-nums">{{ rate(r.scrapRate) }}</dd>
            </div>
          </dl>

          @if (noReason(); as nr) {
            <p class="bg-muted rounded-md px-3 py-2 text-sm" role="note">
              <strong class="tabular-nums">{{ rate(nr.share) }}</strong> degli scarti ({{ qty(nr.quantity) }} {{ pieces(nr.quantity) }}) è <strong>senza motivo</strong>:
              chiedere di indicare il motivo in chiusura rende il report più utile.
            </p>
          }

          <div class="grid gap-4 xl:grid-cols-3">
            <section class="rounded-md border p-4" aria-labelledby="sc-h-reason">
              <h2 id="sc-h-reason" class="mb-3 text-sm font-medium">Per motivo</h2>
              @if (reasonBars().length) {
                <ol class="grid gap-3">
                  @for (b of reasonBars(); track b.key) {
                    <ng-container *ngTemplateOutlet="bar; context: { $implicit: b }" />
                  }
                </ol>
              } @else {
                <p class="text-muted-foreground text-sm">Nessuno scarto nel periodo.</p>
              }
              <p class="text-muted-foreground mt-3 text-xs">Quota sul totale degli scarti.</p>
            </section>
            <section class="rounded-md border p-4" aria-labelledby="sc-h-seg">
              <h2 id="sc-h-seg" class="mb-3 text-sm font-medium">Per fase</h2>
              @if (segmentBars().length) {
                <ol class="grid gap-3">
                  @for (b of segmentBars(); track b.key) {
                    <ng-container *ngTemplateOutlet="bar; context: { $implicit: b }" />
                  }
                </ol>
              } @else {
                <p class="text-muted-foreground text-sm">Nessun dato.</p>
              }
              <p class="text-muted-foreground mt-3 text-xs">Percentuale di scarto sulla produzione della fase.</p>
            </section>
            <section class="rounded-md border p-4" aria-labelledby="sc-h-mat">
              <h2 id="sc-h-mat" class="mb-3 text-sm font-medium">Per materiale</h2>
              @if (materialBars().length) {
                <ol class="grid gap-3">
                  @for (b of materialBars(); track b.key) {
                    <ng-container *ngTemplateOutlet="bar; context: { $implicit: b }" />
                  }
                </ol>
              } @else {
                <p class="text-muted-foreground text-sm">Nessun dato.</p>
              }
              <p class="text-muted-foreground mt-3 text-xs">Percentuale di scarto sulla produzione del materiale.</p>
            </section>
          </div>
        }
      }
    </div>

    <ng-template #bar let-b>
      <li class="grid gap-1">
        <div class="flex items-baseline justify-between gap-2 text-sm">
          <span class="min-w-0 truncate" [class.font-semibold]="b.highlight" [title]="b.label">{{ b.label }}</span>
          <span class="shrink-0 tabular-nums" [class.font-semibold]="b.highlight">{{ b.value }}</span>
        </div>
        <hlm-progress class="h-2" [value]="b.pct" [attr.aria-label]="b.label + ': ' + b.value">
          <hlm-progress-indicator />
        </hlm-progress>
        <span class="text-muted-foreground text-xs tabular-nums">{{ b.detail }}</span>
      </li>
    </ng-template>
  `,
})
export class ScrapComponent implements OnInit {
  private readonly api = inject(ScrapService);

  protected readonly presets = PRESETS;
  protected readonly qty = formatQty;
  protected readonly pieces = pieces;
  protected readonly rate = (v: number): string => pctFmt.format(v);

  protected readonly from = signal(toDateInput(daysAgo(30)));
  protected readonly to = signal(toDateInput(new Date()));
  protected readonly preset = signal<string | null>('30');
  protected readonly report = signal<ScrapReportModel | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly noReason = computed(() => {
    const r = this.report()?.byReason.find((x) => x.scrapReasonId == null);
    return r && r.quantity > 0 ? r : null;
  });

  protected readonly reasonBars = computed<Bar[]>(() =>
    (this.report()?.byReason ?? [])
      .slice()
      .sort((a, b) => b.quantity - a.quantity)
      .map((r) => ({
        key: String(r.scrapReasonId ?? 'none'),
        label: r.scrapReasonId == null ? 'Senza motivo' : r.name,
        detail: `${formatQty(r.quantity)} ${pieces(r.quantity)}`,
        pct: Math.min(100, r.share * 100),
        value: pctFmt.format(r.share),
        highlight: r.scrapReasonId == null,
      })),
  );

  protected readonly segmentBars = computed<Bar[]>(() => this.rateBars((this.report()?.byProcessSegment ?? []).map((s) => ({ key: String(s.processSegmentId), label: `${s.code} · ${s.name}`, ...s }))));
  protected readonly materialBars = computed<Bar[]>(() => this.rateBars((this.report()?.byMaterial ?? []).map((m) => ({ key: String(m.materialId), label: `${m.code} · ${m.name}`, ...m }))));

  ngOnInit(): void {
    void this.load();
  }

  private rateBars(items: { key: string; label: string; goodQuantity: number; scrapQuantity: number; scrapRate: number }[]): Bar[] {
    const sorted = items.slice().sort((a, b) => b.scrapRate - a.scrapRate || b.scrapQuantity - a.scrapQuantity);
    const max = Math.max(0, ...sorted.map((i) => i.scrapRate));
    return sorted.map((i) => ({
      key: i.key,
      label: i.label,
      detail: `${formatQty(i.scrapQuantity)} ${i.scrapQuantity === 1 ? 'scarto' : 'scarti'} su ${formatQty(i.goodQuantity + i.scrapQuantity)} ${pieces(i.goodQuantity + i.scrapQuantity)}`,
      pct: max > 0 ? (i.scrapRate / max) * 100 : 0,
      value: pctFmt.format(i.scrapRate),
    }));
  }

  protected setFrom(v: string): void {
    if (!v) return;
    this.from.set(v);
    this.preset.set(null);
    void this.load();
  }

  protected setTo(v: string): void {
    if (!v) return;
    this.to.set(v);
    this.preset.set(null);
    void this.load();
  }

  protected applyPreset(id: string): void {
    const now = new Date();
    this.from.set(toDateInput(id === 'month' ? new Date(now.getFullYear(), now.getMonth(), 1) : daysAgo(Number(id))));
    this.to.set(toDateInput(now));
    this.preset.set(id);
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      // Giorni interi in ora locale: dall'inizio del primo alla fine dell'ultimo.
      const from = new Date(`${this.from()}T00:00:00`).toISOString();
      const to = new Date(`${this.to()}T23:59:59.999`).toISOString();
      this.report.set(await firstValueFrom(this.api.getScrapReport({ from, to })));
    } catch (e) {
      this.error.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }
}
