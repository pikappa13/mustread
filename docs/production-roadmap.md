# Piano completo di evoluzione di MustRead

## 1. Obiettivo

Portare MustRead dall'attuale MVP pubblico `v1.0.1` a un prodotto distribuibile in modo affidabile in reti aziendali Windows, incluse postazioni condivise e server RDP multi-sessione.

Il risultato deve garantire:

- recapito prevedibile e conferme di lettura durevoli;
- comportamento corretto con più utenti e processi sullo stesso computer;
- sicurezza basata su ACL SMB documentate e autorizzazioni applicative fail-closed;
- build, test, firma e rilascio riproducibili;
- diagnostica sufficiente per capire perché un messaggio non è stato inviato, mostrato o registrato;
- installazione e aggiornamento gestibili centralmente;
- documentazione coerente con le funzionalità realmente disponibili.

MustRead resterà uno strumento operativo interno: non dovrà essere presentato come sistema di emergenza certificato, canale legalmente opponibile o sostituto di PEC e sistemi safety-critical.

---

## 2. Baseline verificata

Stato di partenza:

- applicazione WinForms su .NET 8 per Windows;
- backend basato su cartella SMB, senza database o servizio centrale;
- invio a utente, computer o tutti;
- ricezione tramite polling e finestra fullscreen con conferma obbligatoria;
- heartbeat dei client e rilevamento sessioni RDP;
- storico `sent.jsonl`, `shown.jsonl` e `read.jsonl`;
- release pubblica `v1.0.1` self-contained;
- 104 test locali superati;
- suite di test, piani interni e output di coverage non versionati;
- nessuna CI, installer o firma Authenticode;
- copertura complessiva attuale circa 31,6%, con UI non coperta.

Questa baseline è sufficiente per un pilot limitato, non ancora per un rollout aziendale ampio.

---

## 3. Invarianti da preservare

Ogni intervento deve rispettare queste regole:

1. Un messaggio non deve riaprirsi dopo una conferma registrata correttamente.
2. Un messaggio consegnato a più destinatari non deve essere eliminato dalla share dal primo destinatario che lo legge.
3. Una conferma non può essere dichiarata riuscita se lo stato durevole non è stato salvato.
4. Due conferme concorrenti non devono causare perdita di dati o corruzione dei file.
5. Un guasto SMB non deve bloccare indefinitamente la UI, ma deve restare visibile e recuperabile.
6. Utenti e sessioni RDP devono mantenere stati separati quando il requisito di consegna lo richiede.
7. I permessi SMB/NTFS sono il confine di sicurezza primario; i controlli UI non devono essere considerati una protezione sufficiente.
8. Nessuna credenziale, percorso locale sensibile o configurazione aziendale deve entrare nel repository o nei log pubblici.
9. Ogni funzionalità dichiarata nel README deve essere dimostrata da test o da una procedura di verifica ripetibile.

---

## 4. Strategia di rilascio

### v1.1 — Fondamenta e affidabilità

Obiettivo: rendere il nucleo verificabile, concorrente e sicuro per un pilot reale.

Include:

- repository e CI;
- nuovo stato di lettura per utente/sessione;
- scritture atomiche e retry durevoli;
- logging e diagnostica;
- sicurezza SMB e autorizzazioni;
- test multi-processo e RDP;
- installer e firma, almeno nel canale di release.

### v1.2 — Operatività e UX

Obiettivo: rendere il prodotto gestibile dagli amministratori e comprensibile agli utenti.

Include:

- ricevute al mittente;
- storico strutturato, filtri ed export;
- severità e suoni;
- accessibilità e multilingua;
- retention e pulizia automatica;
- registro persistente dei destinatari conosciuti.

### v1.3 — Distribuzione aziendale

Obiettivo: semplificare rollout e amministrazione su reti più grandi.

Include:

- gruppi e reparti;
- integrazione opzionale con Active Directory;
- policy amministrative;
- aggiornamenti controllati;
- test di carico e compatibilità su più versioni Windows Server.

### v2.0 — Architetture opzionali

Da affrontare solo dopo aver stabilizzato il backend SMB:

- backend HTTP opzionale;
- console amministrativa centralizzata;
- eventuale servizio Windows per casi compatibili con la sessione interattiva;
- build legacy .NET Framework 4.8, solo se esiste un requisito commerciale concreto.

---

## 5. Piano di lavoro dettagliato

## Fase 0 — Repository, tracciabilità e build riproducibile

### Attività

- Correggere il pattern non valido `C:\ProgramData\MustRead\` in `.gitignore`.
- Rimuovere l'esclusione di `src/MustRead.Tests/` e versionare tutti i 104 test.
- Decidere quali piani interni rendere pubblici e spostare le decisioni obbligatorie in `docs/`.
- Aggiungere una solution `MustRead.sln` o `MustRead.slnx` con applicazione e test.
- Aggiungere `AGENTS.md` con invarianti tecnici, comandi di verifica e regole di release.
- Aggiornare il README da 79 a 104 test e distinguere funzionalità presenti, limitazioni e roadmap.
- Sostituire il badge statico con lo stato della CI reale.
- Aggiungere `CHANGELOG.md`, convenzione di versionamento e procedura di release.
- Separare chiaramente sorgenti, artefatti generati e pacchetti pubblicati.
- Aggiornare con prudenza i pacchetti di test, verificando eventuali breaking change.

### CI minima

Per ogni push e pull request:

1. restore con lock file o dipendenze bloccate;
2. build Release con warning trattati come errori;
3. esecuzione di tutti i test;
4. raccolta coverage;
5. scansione pacchetti vulnerabili;
6. verifica formattazione;
7. publish self-contained `win-x64` come artefatto non firmato di CI.

Per i tag:

1. build da checkout pulito;
2. firma del binario;
3. creazione installer e pacchetto portabile;
4. generazione SHA-256 e SBOM;
5. smoke test dell'artefatto finale;
6. pubblicazione release solo dopo approvazione.

### Criteri di accettazione

- Un clone pulito esegue build e test con un singolo comando documentato.
- La CI parte automaticamente e blocca merge con build o test rossi.
- I test sono presenti nel repository pubblico.
- README, versione assembly, tag e asset di release riportano la stessa versione.

---

## Fase 1 — Conferme di lettura durevoli e concorrenza

### 1.1 Definire la semantica di consegna

Documentare e testare separatamente:

- messaggio per utente: una conferma per identità utente o una per sessione;
- messaggio per computer: una conferma per computer o una per ogni utente collegato;
- broadcast: una conferma per client, utente o sessione;
- comportamento quando la stessa persona ha più sessioni RDP;
- comportamento quando un destinatario entra online dopo l'invio.

La decisione consigliata è usare una chiave di consegna esplicita:

```text
messageId + recipientScope + domain + username + computerName + sessionId
```

Il `sessionId` va incluso solo nei tipi di consegna che richiedono una conferma per sessione.

### 1.2 Sostituire lo stato globale dei messaggi letti

- Non usare un unico `read_messages.json` condiviso da tutti gli utenti della macchina.
- Salvare lo stato in un percorso per identità, oppure in un archivio locale con record dotati di chiave di consegna.
- Proteggere aggiornamenti concorrenti tra processi, non solo tra thread della stessa istanza.
- Fare merge dello stato esistente prima di ogni scrittura.
- Usare scrittura temporanea, flush e rename atomico.
- Conservare una copia recuperabile in caso di file corrotto.
- Migrare automaticamente il formato `v1.0.x`, senza rimostrare tutti i vecchi messaggi.

### 1.3 Rendere affidabile `MarkAsRead`

Flusso richiesto:

1. disabilitare il pulsante per impedire doppie conferme;
2. registrare localmente la conferma in modo atomico;
3. chiudere l'alert solo dopo il successo locale;
4. accodare la sincronizzazione SMB in una outbox persistente;
5. ritentare in background fino al successo o fino a intervento amministrativo;
6. mostrare uno stato diagnostico se la conferma remota è ancora pendente;
7. impedire che l'uscita dell'app perda i job in coda.

Il log remoto può essere asincrono, ma non deve essere fire-and-forget senza persistenza.

### 1.4 Correggere il locking dei file

- Eliminare il fallback che scrive senza lock dopo il timeout.
- Restituire un risultato esplicito: successo, timeout, errore permessi, share offline.
- Aggiungere backoff con jitter e cancellazione.
- Usare file temporanei e rename atomico per JSON completi.
- Per JSONL, mantenere lock esclusivo fino a flush completato.
- Valutare un file per evento al posto di un unico JSONL conteso; un processo di compattazione può generare viste aggregate.
- Ripulire lock orfani senza interpretare la semplice esistenza del file come lock attivo.

### 1.5 Validazione dei messaggi

- Introdurre `schemaVersion`.
- Validare ID, tipo target, destinatario, date, titolo e corpo.
- Imporre lunghezze massime e rifiutare payload eccessivi.
- Usare `DateTimeOffset.UtcNow` per nuovi record.
- Aumentare l'entropia del `messageId` usando GUID completo o identificatore equivalente.
- Mettere in quarantena file malformati con motivo diagnostico.
- Se un handler UI fallisce, rimuovere correttamente il messaggio dallo stato attivo o ritentarlo.

### Test obbligatori

- due processi confermano messaggi diversi contemporaneamente;
- due processi confermano lo stesso messaggio;
- due sessioni RDP dello stesso utente;
- due utenti diversi sullo stesso server;
- share offline durante conferma e successivo reconnect;
- arresto immediato del processo dopo il click;
- lock mantenuto oltre il timeout;
- file locale troncato o corrotto;
- crash tra scrittura temporanea e rename;
- 1.000 messaggi e 100 conferme concorrenti senza perdita.

### Criteri di accettazione

- Zero conferme perse nei test concorrenti.
- Nessuna scrittura avviene senza lock o senza strategia atomica equivalente.
- Una conferma locale sopravvive a crash e riavvio.
- Una conferma remota pendente viene sincronizzata dopo il ritorno della rete.
- Il comportamento multi-sessione coincide con la semantica documentata.

---

## Fase 2 — Sicurezza e modello permessi

### Attività

- Redigere un threat model essenziale: mittente non autorizzato, modifica messaggi, cancellazione inbox, lettura dati, spoofing heartbeat, manomissione storico e denial of service.
- Definire gruppi Windows separati per amministratori, mittenti e destinatari.
- Pubblicare una matrice ACL SMB e NTFS pronta da applicare.
- Rendere `authorized_senders.json` obbligatorio nei deployment protetti, oppure introdurre una policy esplicita `authorizationMode`.
- Fare fallire l'autorizzazione in modo sicuro se il file esiste ma è illeggibile o malformato.
- Ripetere il controllo di autorizzazione dentro `MessageSenderService.Send()`, non solo prima di aprire la form.
- Validare e normalizzare tutti i componenti usati nei percorsi per impedire traversal o nomi non validi.
- Impedire a un ricevitore ordinario di scrivere nell'inbox o modificare la configurazione.
- Limitare dimensioni, numero e frequenza dei messaggi.
- Non scrivere contenuti sensibili nei log diagnostici per impostazione predefinita.
- Valutare firma HMAC dei messaggi solo se la gestione sicura delle chiavi è realizzabile; non presentarla come sostituto delle ACL.
- Documentare privacy, conservazione delle ricevute e accesso allo storico.

### Criteri di accettazione

- Un utente non autorizzato non può inviare modificando solo la configurazione locale.
- Un destinatario non può alterare inbox, configurazione o storico oltre alle operazioni strettamente necessarie.
- I test coprono file di autorizzazione assente, malformato, inaccessibile e aggiornato durante l'esecuzione.
- La guida di setup include comandi o schermate esatte per applicare e verificare le ACL.

---

## Fase 3 — Logging, diagnostica e resilienza di rete

### Attività

- Introdurre logging strutturato locale con livelli e rotazione.
- Registrare errori attualmente ignorati dai `catch` vuoti, senza interrompere l'app per errori recuperabili.
- Aggiungere uno stato tray: connesso, degradato, offline, sincronizzazione pendente.
- Mostrare ultimo heartbeat riuscito, ultimo polling, numero job pendenti e ultimo errore sintetico.
- Aggiungere un comando “Verifica configurazione” che controlli:
  - raggiungibilità share;
  - permessi di lettura/scrittura richiesti per il ruolo;
  - presenza e validità configurazione;
  - latenza approssimativa;
  - accesso alle directory obbligatorie.
- Usare timeout e backoff per tutte le operazioni SMB.
- Evitare polling sovrapposti se un ciclo dura più dell'intervallo configurato.
- Gestire sospensione, resume, cambio rete e riconnessione.
- Esportare un pacchetto diagnostico privo di contenuti dei messaggi e credenziali.

### Criteri di accettazione

- Nessun errore operativo significativo viene ignorato senza almeno una traccia locale.
- L'utente distingue chiaramente “messaggio confermato localmente” da “ricevuta sincronizzata”.
- Una share irraggiungibile non blocca la UI e non produce loop aggressivi.
- Il pacchetto diagnostico è sufficiente per capire il punto di guasto senza esporre messaggi riservati.

---

## Fase 4 — Test end-to-end e matrice piattaforme

### Livelli di test

1. **Unitari:** modelli, validazione, serializzazione, autorizzazione e retry.
2. **Integrazione filesystem:** share simulata, lock, migrazione e corruzione.
3. **Multi-processo:** più istanze reali contro la stessa directory.
4. **UI automation:** configurazione iniziale, tray, invio, coda alert e conferma.
5. **End-to-end SMB:** almeno due macchine o VM Windows.
6. **RDP:** più sessioni simultanee su Windows Server.
7. **Installazione/upgrade:** nuova installazione, upgrade e disinstallazione.

### Matrice minima

- Windows 10 22H2 x64, finché supportato dal prodotto;
- Windows 11 corrente x64;
- Windows Server 2019;
- Windows Server 2022;
- console locale e RDP;
- utente standard e amministratore;
- DPI 100%, 150% e 200%;
- un monitor e multi-monitor;
- share disponibile, lenta, intermittente e non autorizzata.

### Target di coverage

- servizi core: almeno 85% linee e 75% branch;
- progetto complessivo: almeno 70% linee, senza usare il numero come sostituto degli E2E;
- zero form critiche completamente prive di test;
- tutti i bug di produzione devono ricevere un test di regressione.

---

## Fase 5 — Packaging, firma e aggiornamenti

### Attività

- Creare un installer MSI o MSIX adatto a deployment via GPO/Intune.
- Mantenere anche un pacchetto portabile self-contained, se utile.
- Installare in un percorso stabile sotto `Program Files` e mantenere i dati in una directory appropriata con ACL definite.
- Gestire autostart per utente e policy aziendale senza richiedere elevazione a ogni modifica non amministrativa.
- Aggiungere manifest DPI e compatibilità Windows.
- Acquistare/configurare un certificato Authenticode e timestamp server.
- Firmare EXE e installer; verificare la firma in CI prima della pubblicazione.
- Produrre SHA-256 e SBOM per ogni asset.
- Definire canali stable e, se necessario, preview.
- Implementare controllo aggiornamenti solo se può rispettare proxy, policy e ambienti offline; altrimenti documentare l'upgrade amministrato.
- Testare rollback e conservazione della configurazione.

### Criteri di accettazione

- Installazione silenziosa e disinstallazione funzionano su utente standard tramite distribuzione amministrata.
- Upgrade da `v1.0.1` conserva configurazione e stato senza duplicare notifiche.
- Windows mostra un editore verificato.
- Hash, versione, firma e tag sono verificati automaticamente.

---

## Fase 6 — Storico, ricevute e retention

### Ricevute al mittente

- Modellare una ricevuta distinta per destinatario, non un solo file `<messageId>.json` sovrascrivibile.
- Chiave consigliata: `messageId + deliveryKey`.
- Memorizzare ricevute in modo idempotente.
- Notificare il mittente al reconnect senza duplicati.
- Mostrare consegne previste, mostrate, lette e pendenti.
- Distinguere “letto” da eventuale chiusura automatica.

### Storico amministrativo

- Sostituire la vista di JSON grezzo con una tabella strutturata.
- Aggiungere ricerca, filtri per data, destinatario, computer e stato.
- Aggiungere dettaglio del messaggio e timeline inviato/mostrato/letto.
- Aggiungere export CSV con escaping e codifica corretti.
- Gestire file grandi senza caricarli interamente in memoria.

### Retention

- Configurare giorni di conservazione separati per inbox, heartbeat, log e storico.
- Archiviare o eliminare solo dopo aver rispettato ricevute pendenti e policy.
- Rendere il cleanup concorrente e idempotente.
- Aggiungere modalità dry-run e report delle eliminazioni.
- Definire backup e ripristino della share.

### Criteri di accettazione

- Il mittente vede una ricevuta per ogni destinatario previsto.
- Nessuna ricevuta viene duplicata dopo riavvio.
- Lo storico resta usabile con almeno 100.000 eventi.
- La retention non elimina messaggi ancora da consegnare.

---

## Fase 7 — UX, accessibilità e localizzazione

### Attività

- Spostare tutte le stringhe UI in file `.resx`.
- Supportare almeno italiano e inglese con fallback sicuro.
- Consentire cambio lingua dalle impostazioni.
- Aggiungere `AccessibleName`, `AccessibleDescription`, ordine tab e scorciatoie coerenti.
- Verificare contrasto, focus visibile, screen reader e navigazione tastiera.
- Gestire testi lunghi, scaling DPI e schermi piccoli.
- Gestire multi-monitor: definire se l'alert appare sul monitor attivo o su tutti.
- Aggiungere livelli `Info`, `Warning`, `Critical` con stile accessibile, non basato solo sul colore.
- Aggiungere suono configurabile con opzione silenziosa e rispetto delle policy.
- Se viene introdotto un timeout, non registrarlo come “letto”: usare uno stato separato come `expired` o `autoClosed`.
- Rendere titolo e corpo configurabili con limiti espliciti e anteprima.

### Criteri di accettazione

- Tutti i flussi principali sono completabili solo da tastiera.
- Italiano e inglese non contengono stringhe mancanti.
- UI verificata a 100%, 150% e 200% DPI.
- Nessun timeout produce una falsa conferma di lettura.

---

## Fase 8 — Destinatari offline, gruppi e Active Directory

### Registro destinatari

- Conservare i client conosciuti oltre la sola finestra “online”.
- Mostrare stato online/offline e ultimo contatto.
- Consentire invio esplicito a un destinatario offline conosciuto.
- Evitare duplicati dovuti a rinomina computer, cambio dominio o sessioni multiple.

### Gruppi e reparti

- Definire gruppi statici in configurazione versionata.
- Espandere il gruppo in consegne individuali tracciabili.
- Salvare lo snapshot dei membri al momento dell'invio.
- Applicare autorizzazioni specifiche per broadcast e gruppi sensibili.

### Active Directory opzionale

- Integrare AD solo dietro feature flag.
- Supportare ricerca utenti e gruppi con timeout e cache.
- Funzionare ancora senza AD in workgroup o reti isolate.
- Non memorizzare password di dominio.
- Testare domini non raggiungibili, trust multipli e nomi internazionali.

---

## Fase 9 — Scalabilità e backend opzionale

Prima di dichiarare un numero di client supportato:

- definire scenari di carico realistici;
- misurare polling, apertura file e contesa sul file server;
- testare 10, 50, 100, 500 e 1.000 client simulati;
- misurare latenza p50, p95 e p99;
- fissare limiti supportati e requisiti del file server;
- introdurre jitter nel polling per evitare picchi sincronizzati;
- valutare sharding delle directory quando contengono molti file.

Un backend HTTP va progettato solo se SMB non soddisfa più requisiti di scala, audit o reti distribuite. Dovrà mantenere lo stesso modello di messaggio e migrazione, con autenticazione, TLS, code durevoli e osservabilità.

---

## 6. Suddivisione in epic e dipendenze

| ID | Epic | Priorità | Dipende da | Taglia |
|---|---|---:|---|---:|
| E0 | Repository, test versionati e CI | P0 | — | M |
| E1 | Semantica di consegna e nuovo stato locale | P0 | E0 | L |
| E2 | Locking, outbox e conferme durevoli | P0 | E1 | L |
| E3 | Sicurezza, ACL e autorizzazioni | P0 | E0 | M |
| E4 | Logging, diagnostica e resilienza SMB | P0 | E2 | L |
| E5 | Test E2E multi-processo e RDP | P0 | E1–E4 | L |
| E6 | Installer, firma e release pipeline | P0 | E0, E5 | L |
| E7 | Ricevute, storico strutturato e retention | P1 | E2, E4 | L |
| E8 | Accessibilità, severità e multilingua | P1 | E0 | L |
| E9 | Destinatari offline e gruppi | P1 | E1, E7 | L |
| E10 | Active Directory opzionale | P2 | E9 | L |
| E11 | Test di scala e limiti supportati | P1 | E4, E5 | M |
| E12 | Backend HTTP opzionale | P3 | E7, E11 | XL |
| E13 | Porting legacy .NET Framework 4.8 | P3 | E5 | XL |

Le taglie indicano complessità relativa, non una stima temporale: `M`, `L`, `XL` vanno spezzate in issue implementabili e verificabili singolarmente.

---

## 7. Ordine consigliato delle prime issue

1. Correggere `.gitignore` e versionare i test.
2. Aggiungere solution e comando unico `verify`.
3. Creare CI build/test/coverage/security.
4. Formalizzare la semantica User/Computer/All/RDP.
5. Aggiungere test multi-processo che riproducono il lost update.
6. Implementare archivio locale versionato per delivery key.
7. Rendere atomiche le scritture e rimuovere il fallback senza lock.
8. Introdurre outbox persistente per le ricevute remote.
9. Sostituire i `catch` vuoti con logging strutturato.
10. Aggiungere stato tray e verifica connessione.
11. Rafforzare autorizzazione e pubblicare matrice ACL.
12. Costruire ambiente E2E con due client e un server SMB.
13. Eseguire test RDP multi-sessione.
14. Creare installer e pipeline di firma.
15. Pubblicare `v1.1.0` solo dopo il superamento di tutti i gate.

---

## 8. Gate obbligatori per v1.1.0

La release non è pronta finché tutti i punti seguenti non sono verificati:

- [ ] worktree pulito e tag sul commit corretto;
- [ ] build Release riproducibile da clone pulito;
- [ ] suite completa versionata e verde in CI;
- [ ] nessuna vulnerabilità nota ad alta o critica severità;
- [ ] test multi-processo e RDP superati;
- [ ] conferma locale resistente a crash e riavvio;
- [ ] sincronizzazione SMB recuperata dopo disconnessione;
- [ ] nessuna scrittura di fallback senza lock;
- [ ] ACL validate con account mittente e destinatario reali;
- [ ] test UI essenziali e verifica DPI/accessibilità completati;
- [ ] installer e portabile testati su macchina pulita;
- [ ] EXE e installer firmati e timestampati;
- [ ] upgrade da `v1.0.1` verificato;
- [ ] README, setup, limitazioni e changelog aggiornati;
- [ ] hash SHA-256 e SBOM allegati alla release;
- [ ] smoke test dell'asset scaricato dalla release, non solo dell'output locale;

---

## 9. Definition of Done per ogni attività

Un'attività è completata solo quando:

1. il comportamento atteso è documentato;
2. il codice gestisce successo, errore, timeout e concorrenza pertinenti;
3. esistono test automatici adeguati;
4. i test di regressione precedenti restano verdi;
5. non vengono introdotti warning o vulnerabilità note rilevanti;
6. la documentazione utente e amministrativa è aggiornata;
7. la modifica è verificata sull'artefatto Release quando coinvolge runtime o packaging;
8. non restano credenziali, dati reali o artefatti temporanei nel repository;
9. il commit è piccolo, revisionabile e collegato alla relativa issue;
10. i criteri di accettazione dell'epic risultano dimostrati, non solo dichiarati.

---

## 10. Decisioni da chiudere prima dello sviluppo v1.1

Le seguenti scelte influenzano il modello dati e non vanno lasciate implicite:

1. Un messaggio utente richiede una conferma per persona o per ogni sessione?
2. Un messaggio computer richiede una conferma per macchina o per ogni utente connesso?
3. Il mittente deve vedere “mostrato” e “letto” per ogni destinatario?
4. Per quanto tempo vanno conservati messaggi, ricevute e storico?
5. Il deployment target usa GPO, Intune, script o installazione manuale?
6. È disponibile un certificato di code signing?
7. Quali versioni di Windows e Windows Server devono essere supportate ufficialmente?
8. Serve davvero compatibilità Windows 7/Server 2016 oppure può restare fuori scope?
9. I gruppi arriveranno da file statici o Active Directory?
10. Qual è il carico target: numero di client, messaggi al giorno e picchi simultanei?

Fino alla chiusura di queste decisioni si può completare la Fase 0 e preparare i test che evidenziano i problemi correnti, ma non va cristallizzato il nuovo formato delle ricevute.
