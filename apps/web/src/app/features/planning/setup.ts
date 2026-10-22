/** Larghezza minima (px) di una barra perché mostri la parte di attrezzaggio. */
export const SETUP_MIN_BAR_PX = 16;

/**
 * Larghezza (px) della parte di attrezzaggio all'inizio della barra, sull'asse compresso:
 * `toX(inizio + minuti)` meno `toX(inizio)`, tagliata alla barra. `null` se non va disegnata (zero minuti o barra troppo stretta).
 */
export function setupSegmentWidth(input: { startMs: number; endMs: number; setupMinutes?: number | null; barWidth: number; toX: (ms: number) => number }): number | null {
  const { startMs, endMs, setupMinutes, barWidth, toX } = input;
  if (!setupMinutes || setupMinutes <= 0 || barWidth < SETUP_MIN_BAR_PX) return null;
  const end = Math.min(startMs + setupMinutes * 60_000, endMs);
  const w = Math.min(toX(end) - toX(startMs), barWidth);
  return w >= 1 ? w : null;
}

export interface SetupDelta {
  /** `up` = più attrezzaggio (peggio), `down` = meno (meglio). */
  direction: 'up' | 'down' | 'same';
  icon: string;
  text: string;
  /** Testo per lettori di schermo. */
  sr: string;
}

/** Sintesi «Attrezzaggi: prima → dopo min» con variazione (icona + testo, mai solo colore). */
export function setupDelta(before: number | null | undefined, after: number | null | undefined): SetupDelta | null {
  if (before == null && after == null) return null;
  const b = before ?? 0;
  const a = after ?? 0;
  const d = a - b;
  const base = `Attrezzaggi: ${b} → ${a} min`;
  if (d > 0) return { direction: 'up', icon: 'lucideTrendingUp', text: `${base} (+${d})`, sr: `${base}, ${d} minuti in più` };
  if (d < 0) return { direction: 'down', icon: 'lucideTrendingDown', text: `${base} (${d})`, sr: `${base}, ${-d} minuti in meno` };
  return { direction: 'same', icon: 'lucideMinus', text: `${base} (invariato)`, sr: `${base}, invariato` };
}

/** Testo breve «attrezzaggio N min» (vuoto se zero). */
export function setupLabel(minutes?: number | null, full?: boolean | null): string {
  return minutes && minutes > 0 ? `attrezzaggio ${minutes} min${full ? ' (completo)' : ''}` : '';
}

/** Nomi delle attrezzature, separati da virgola. */
export function toolingNames(tooling?: readonly { code: string; name: string }[] | null): string {
  return (tooling ?? []).map((t) => t.code).join(', ');
}

/** Tratteggio diagonale (pattern, non solo colore) con il colore dell'ordine (`--oc`) a bassa opacità. */
export const SETUP_HATCH =
  'bg-[repeating-linear-gradient(135deg,transparent_0,transparent_3px,color-mix(in_oklab,var(--oc)_55%,transparent)_3px,color-mix(in_oklab,var(--oc)_55%,transparent)_5px)]';
