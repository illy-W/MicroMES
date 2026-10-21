# MicroMES - Product

## Vision

MicroMES è un MES leggero per micro imprese manifatturiere (laboratori, piccole officine, artigiani con 1-20 persone). Risponde a poche domande, ogni giorno, senza un consulente:

- Cosa devo produrre, per quando, e a che punto siamo?
- Cosa posso avviare adesso e cosa sta aspettando cosa?
- Cosa è stato prodotto davvero, in quanto tempo e con quanti scarti?

Principi che guidano ogni scelta:

1. **Semplice prima che completo.** Ogni funzione ha un valore di default sensato; il caso comune non richiede configurazione. Le funzioni avanzate sono opzionali e restano fuori dal percorso principale.
2. **Niente errori silenziosi.** Il sistema impedisce gli stati impossibili (cicli, dipendenze orfane, transizioni non valide) e, quando blocca, dice cosa fare. Mai un cambiamento che altera i dati senza che l'utente lo veda.
3. **I dati storici non cambiano.** Il rilascio di un ordine fa un'istantanea di ciclo e distinta: modificare l'anagrafica dopo non altera gli ordini già rilasciati.
4. **Vocabolario standard.** ISA-95 (IEC 62264) in forma ridotta, in modo che i dati restino integrabili con ERP e altri sistemi. Ogni deviazione è documentata.
5. **Una sola fonte di verità per le azioni.** Il backend dice quali azioni sono consentite (`allowedActions`); l'interfaccia le mostra e basta.

Fuori scopo (per ora): contabilità e costi, magazzino e giacenze, acquisti, pianificazione a capacità finita, integrazioni con macchine (livello 1-2).

## Reference standard

ISA-95 (IEC 62264) is the reference for vocabulary and conceptual model (Material, Equipment, Personnel, Process Segment, Work Order, ...). It is adopted in reduced form: names and relationships are aligned with the standard, but only what the requirements need is implemented. Every deviation is documented in the feature spec and in `doc/glossary.md`. The job state machine follows *OPC UA for ISA-95 Part 4: Job Control*.

Where the standard is silent or too heavy for a micro business, the reference is how widely used products (Odoo MRP, ERPNext, SAP PP) solved the same problem, documented in the spec of the feature.

## User roles

| Ruolo | Chi è | Cosa fa |
|---|---|---|
| Responsabile di produzione | Titolare o capo officina | Mantiene anagrafiche (materiali, distinta, cicli), crea e rilascia ordini, segue l'avanzamento nella panoramica. |
| Operatore | Chi lavora al banco o alla macchina | Vede i lavori pronti, li avvia, mette in pausa, termina, registra buoni e scarti (vista operatore: da realizzare). |

Autenticazione e permessi per ruolo: non ancora implementati.

## Core flows

1. **Anagrafica:** materiali -> distinta base (BOM) -> fasi di processo -> ciclo (routing) di ogni semilavorato e prodotto finito.
2. **Pianificazione:** creazione dell'ordine di produzione (stato *Pianificato*), poi rilascio: il sistema crea gli ordini figli per i semilavorati con ciclo e gli ordini di lavoro per ogni passo, con dipendenze.
3. **Esecuzione:** ogni ordine di lavoro passa per *In attesa -> Pronto -> In esecuzione -> (In pausa) -> Terminato*; le registrazioni accumulano buoni, scarti e tempi.
4. **Controllo:** panoramica grafica dell'ordine (stato per lavoro, percorso più lungo, ordini figli), lista ordini di lavoro.

Prossimi flussi: collegamento componenti-passi (`specs/bom-step-link`), vista operatore, dettaglio ordine di lavoro.
