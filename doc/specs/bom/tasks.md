# Tasks: Distinta base (BOM)

## Technical Strategy

- Nessun lavoro Nx: nessun nuovo progetto, nessuna nuova dipendenza né libreria `libs/ui`.
- Backend prima, blocca il frontend: il backend riporta il contratto API (tipi dell'albero, DTO, errori) al lead, poi si rigenera il client. Il frontend parte solo dopo.
- Backend: stesso pattern di `Material` (controller → `BomService` con `Result` → `IBomItemRepository` vuoto → `AppDbContext`), mappatura manuale, nessun CQRS. `BomService` inietta anche `IMaterialRepository`, senza modificarlo.
- L'albero si costruisce in `BomService` leggendo le righe per livello, con limite di 10 livelli. Nessun controllo dei cicli.
- Frontend: tabella helm con TanStack v9 e righe espandibili. Se la feature di espansione non copre il caso, stato di espansione a signal. Nessun nuovo componente spartan, nessun componente custom se non motivato.
- Ordine di esecuzione: [Backend] 1 → 2 → [Frontend] 3 → [Lead] 4.

## 1. Backend

- [x] 1.1 [Backend] Entità `BomItem` (`Id`, `ParentMaterialId`, `ComponentMaterialId`, `Quantity` decimal(18,4), `CreatedAt`, `UpdatedAt`) in `libs/api-core/Persistence/Entities`. `DbSet<BomItem>`, configurazione in `AppDbContext` (indice univoco su prodotto e componente, FK con `Restrict`), migration `AddBomItems`. Verificare che la migration si applichi sul DB locale.
  _Satisfies: REQ-1_
- [x] 1.2 [Backend] `BomFeature`: `IBomItemRepository` (nessun metodo aggiunto), `BomItemRepository`, `Registrar.cs` con `AddRepository`, richiamo da `Program.cs`.
  _Satisfies: REQ-2_
- [x] 1.3 [Backend] `BomService` con `Result` e modelli (nodo dell'albero con `children`, richieste di creazione e modifica). Regole: prodotto di tipo `Intermediate` o `FinishedGood`; componente esistente e attivo; componente diverso dal prodotto; quantità > 0; coppia univoca (409). Albero con limite di 10 livelli.
  _Satisfies: REQ-3_
- [x] 1.4 [Backend] `BomController`: `GET /api/materials/{id}/bom`, `POST /api/materials/{id}/bom/items`, `PUT /api/bom-items/{id}`, `DELETE /api/bom-items/{id}`. Codici: 201, 204, 404, 409, 422. Enum come stringa.
  _Satisfies: REQ-3_
- [x] 1.5 [Backend] Verifica manuale con curl/HTTP delle regole e dei codici di errore (albero a due livelli, duplicato, tipo non valido, componente disattivato). Poi `npx nx build Api`.
  _Satisfies: REQ-1, REQ-2, REQ-3_
- [x] 1.6 [Backend] (ultimo) Rigenerare il contratto: `npx nx run api-client:generate-api`. Riportare al lead i nomi dei tipi e dei metodi generati.
  _Satisfies: REQ-3_

## 2. Frontend

- [x] 2.1 [Frontend] Feature `apps/web/src/app/features/bom`: route lazy `/materials/:id/bom` in `app.routes.ts`, servizio store a signals con il client generato (caricamento albero, aggiungi, modifica, rimuovi), caricamento del materiale per l'intestazione.
  _Satisfies: REQ-4_
- [x] 2.2 [Frontend] Pagina BOM: intestazione (codice, nome, link "Materiali", "Aggiungi componente"), tabella helm con TanStack v9 e righe espandibili (chevron con `aria-expanded`, indentazione per livello), colonne Componente, Tipo, Quantità (`num`), Unità, Stato, Azioni. Stili e markup come in `doc/structure.md` "Tables". Nessun paginatore.
  _Satisfies: REQ-4_
- [x] 2.3 [Frontend] Azioni: menu `…` sulle righe del primo livello ("Modifica quantità", "Rimuovi" destructive con alert-dialog); i sotto-livelli sono di sola lettura con link "Apri distinta". Dialog "Aggiungi componente" (`hlm-select` dei materiali attivi esclusi se stesso, quantità) e dialog di modifica quantità, con gli errori 409 e 422 mostrati nel dialog.
  _Satisfies: REQ-4_
- [x] 2.4 [Frontend] Stati: skeleton, errore con "Riprova", BOM vuota (`hlm-empty`). Testi in italiano, token semantici, nessun colore fisso né `dark:`.
  _Satisfies: REQ-4_
- [x] 2.5 [Frontend] Voce "Distinta base" nel menu `…` di `material-row-actions.component.ts`, prima di "Modifica", solo per `Intermediate` e `FinishedGood`; naviga a `/materials/:id/bom`. Il comportamento esistente non cambia.
  _Satisfies: REQ-5_
- [x] 2.6 [Frontend] `npx nx lint web` e `npx nx build web`. Controllo visivo con i browser tool se disponibili (albero espanso, menu, dialog), altrimenti dichiararlo.
  _Satisfies: REQ-4, REQ-5_

## 3. Documentazione

- [x] 3.1 [Lead] `doc/glossary.md`: aggiungere `BomItem` e le deviazioni da ISA-95. `doc/structure.md`: pattern "tabella ad albero" e pagina `/materials/:id/bom`.
  _Satisfies: REQ-6_

## 4. Verifica finale

- [ ] 4.1 [Lead] `npx nx affected -t lint build`, controllo dei criteri di accettazione di ogni REQ, verifica visiva della pagina BOM nel browser (albero a due livelli).
  _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6_
