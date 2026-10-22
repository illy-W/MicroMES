import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleCheck, lucideCircleOff, lucideStar } from '@ng-icons/lucide';
import type { EquipmentModel, EquipmentToolingModel } from '@micromes/api-client/models';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmTableImports } from '@spartan-ng/helm/table';

/** Scheda «Fasi e attrezzature». */
@Component({
  selector: 'app-equipment-segments-tab',
  imports: [HlmBadge, HlmTableImports, NgIcon, RouterLink],
  providers: [provideIcons({ lucideCircleCheck, lucideCircleOff, lucideStar })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-4">
      <section class="flex flex-col gap-2" aria-labelledby="sg-phases">
        <h3 id="sg-phases" class="text-sm font-semibold">Fasi eseguibili</h3>
        @if (!equipment().processSegments.length) {
          <p class="text-muted-foreground text-sm">Nessuna fase collegata.</p>
        } @else {
          <div class="overflow-auto rounded-md border">
            <div hlmTableContainer>
              <table hlmTable>
                <caption class="sr-only">Fasi eseguibili dalla macchina</caption>
                <thead hlmTHead class="bg-muted"><tr hlmTr><th hlmTh>Codice</th><th hlmTh>Nome</th><th hlmTh>Predefinita</th><th hlmTh>Stato</th><th hlmTh><span class="sr-only">Link</span></th></tr></thead>
                <tbody hlmTBody>
                  @for (p of equipment().processSegments; track p.id) {
                    <tr hlmTr [class.text-muted-foreground]="!p.isActive">
                      <td hlmTd class="font-medium">{{ p.code }}</td>
                      <td hlmTd>{{ p.name }}</td>
                      <td hlmTd>
                        @if (p.isDefault) {
                          <span hlmBadge variant="outline" class="border-primary/40 bg-primary/10 text-primary gap-1"><ng-icon name="lucideStar" class="size-3 fill-current" aria-hidden="true" /> Predefinita</span>
                        } @else { <span class="text-muted-foreground">—</span> }
                      </td>
                      <td hlmTd>
                        <span hlmBadge variant="outline" class="text-muted-foreground px-1.5">
                          @if (p.isActive) { <ng-icon name="lucideCircleCheck" aria-hidden="true" /> Attiva } @else { <ng-icon name="lucideCircleOff" aria-hidden="true" /> Disattivata }
                        </span>
                      </td>
                      <td hlmTd><a class="underline underline-offset-2" routerLink="/process-segments" [attr.aria-label]="'Apri Fasi di processo: ' + p.code">Apri fasi</a></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </section>

      @for (g of groups(); track g.id) {
        <section class="flex flex-col gap-2" [attr.aria-labelledby]="g.id">
          <h3 [id]="g.id" class="text-sm font-semibold">{{ g.title }}</h3>
          @if (!g.items.length) {
            <p class="text-muted-foreground text-sm">Nessuna attrezzatura.</p>
          } @else {
            <div class="overflow-auto rounded-md border">
              <div hlmTableContainer>
                <table hlmTable>
                  <caption class="sr-only">{{ g.title }}</caption>
                  <thead hlmTHead class="bg-muted"><tr hlmTr><th hlmTh>Codice</th><th hlmTh>Nome</th><th hlmTh>Quantità</th><th hlmTh>Montaggio</th><th hlmTh>Stato</th><th hlmTh><span class="sr-only">Link</span></th></tr></thead>
                  <tbody hlmTBody>
                    @for (t of g.items; track t.id) {
                      <tr hlmTr [class.text-muted-foreground]="!t.isActive">
                        <td hlmTd class="font-medium">{{ t.code }}</td>
                        <td hlmTd>{{ t.name }}</td>
                        <td hlmTd class="tabular-nums">{{ t.quantity }}</td>
                        <td hlmTd class="tabular-nums">{{ t.setupMinutes }} min</td>
                        <td hlmTd>
                          <span class="flex flex-wrap gap-1">
                            @if (t.isMountedNow) { <span hlmBadge variant="secondary">Montata ora</span> }
                            @if (!t.isActive) { <span hlmBadge variant="outline" class="text-muted-foreground">Disattivata</span> }
                          </span>
                        </td>
                        <td hlmTd><a class="underline underline-offset-2" routerLink="/tooling" [attr.aria-label]="'Apri Attrezzature: ' + t.code">Apri attrezzature</a></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </section>
      }
    </div>
  `,
})
export class EquipmentSegmentsTabComponent {
  readonly equipment = input.required<EquipmentModel>();
  protected groups(): { id: string; title: string; items: EquipmentToolingModel[] }[] {
    const e = this.equipment();
    return [
      { id: 'sg-tool', title: 'Attrezzature collegate', items: e.compatibleTooling ?? [] },
      { id: 'sg-univ', title: 'Compatibili con tutte le macchine', items: e.universalTooling ?? [] },
    ];
  }
}
