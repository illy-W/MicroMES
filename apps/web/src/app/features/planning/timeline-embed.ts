import { InjectionToken } from '@angular/core';

/**
 * Configurazione della timeline incorporata in un'altra pagina (es. scheda «Pianificazione» della macchina).
 * Se fornita: filtro macchina fisso, nessuna sincronizzazione con l'URL, barra dei filtri e dello zoom nascoste.
 */
export interface TimelineEmbed {
  equipmentId: number;
}

export const TIMELINE_EMBED = new InjectionToken<TimelineEmbed>('TIMELINE_EMBED');
