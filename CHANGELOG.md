# Changelog

Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/) e il progetto usa il [Semantic Versioning](https://semver.org/lang/it/).

## [Non rilasciato]

### Aggiunto
- Scadenza dei messaggi con `messageRetentionDays`: il mittente scrive `expiresAt` nel messaggio, i messaggi scaduti non vengono mostrati e le istanze mittenti autorizzate li rimuovono dall'inbox ogni ora.
- Rimozione automatica da `clients\` delle sessioni non viste da più di 7 giorni.
- Impostazione `heartbeatSeconds` (default 10), separata da `pollingSeconds`.
- Su più monitor gli schermi secondari vengono oscurati finché l'alert non è confermato.
- Riavvio automatico di MustRead da parte di Windows dopo un crash o un blocco.

### Modificato
- Il polling salta i messaggi già confermati senza aprirne il file: il carico sulla share non cresce più con il numero di messaggi conservati.
- L'heartbeat non viene più scritto a ogni polling ma ogni `heartbeatSeconds`; la soglia "online" è il maggiore tra 3 heartbeat e 5 polling, quindi i client rc.1 restano visibili.
- Il testo lungo dell'alert scorre invece di essere tagliato e il titolo va a capo.
- L'alert viene riportato in primo piano ogni due secondi senza prendere il focus.
- Gli errori imprevisti mostrano un messaggio breve con il percorso del log invece dello stack trace.

### Corretto
- Il carattere `&` nel titolo, nel testo o nel mittente dell'alert veniva nascosto o sottolineato ("R&D" appariva come "RD").
- Un messaggio per `DOM_A\B` veniva mostrato anche a `DOM\A_B` (stessa cartella inbox): ora conta il destinatario scritto nel messaggio.

## [1.1.0-rc.1] - 2026-10-10

Prima release candidate della 1.1. Vedi [docs/delivery-semantics.md](docs/delivery-semantics.md)
per le regole di consegna e conferma.

### Aggiunto
- Installer MSI per-machine con proprietà `SERVERPATH` e `AUTOSTART`, aggiornamenti che conservano la configurazione, smoke test di installazione/aggiornamento/disinstallazione in CI.
- Percorso della share da registro: default dell'MSI (`HKLM\SOFTWARE\MustRead`) e criteri di gruppo (`HKLM\SOFTWARE\Policies\MustRead`, con precedenza).
- Coda locale persistente per lo storico: le conferme di lettura sopravvivono a share irraggiungibile, crash e chiusura dell'app e vengono sincronizzate in seguito.
- Stato della connessione nella tray, con avviso quando la share non è raggiungibile e numero di conferme da sincronizzare.
- "Verifica connessione": controllo di configurazione, raggiungibilità, permessi e coda, con report copiabile e accesso ai log.
- Avvio automatico dal menu della tray.
- Test multi-processo reali (`MustRead.TestHost`), documentazione delle regole di consegna, matrice ACL con comandi `icacls`, `AGENTS.md`.
- Release con MSI, zip ed exe in bozza e `SHA256SUMS.txt`; i tag con suffisso diventano pre-release.
- Log diagnostico locale per utente in `%LOCALAPPDATA%\MustRead\logs` (14 giorni, senza testo dei messaggi).
- Opzione `requireAuthorizedSenders` per negare l'invio quando `authorized_senders.json` manca.
- Solution `MustRead.sln`, suite di test versionata e CI GitHub Actions (build con warning come errori, test, coverage, scansione vulnerabilità, publish).

### Modificato
- Il polling rilegge lo stato letto scritto da altre sessioni dello stesso utente.
- Errori di rete registrati una volta per interruzione, non a ogni ciclo; jitter su polling e heartbeat.
- Lo stato dei messaggi letti è per utente (`%LOCALAPPDATA%\MustRead\read_messages.json`) invece che per macchina; il vecchio file v1.0.x viene importato al primo avvio.
- Lo stato letto viene unito a quello su disco sotto lock: più istanze dello stesso utente (es. sessioni RDP) non si sovrascrivono le conferme.
- Gli ID messaggio hanno 12 caratteri casuali invece di 4.

### Corretto
- L'avvio automatico impostato dalla finestra impostazioni (elevata) finiva nel profilo dell'amministratore invece che dell'utente.
- "Avvia con Windows" falliva sui profili senza la chiave `Run`.
- Un secondo avvio automatico non mostra più "MustRead è già in esecuzione".
- `FileLockHelper` non scrive più senza lock dopo il timeout: solleva `TimeoutException` e l'errore finisce nel log.
- Le scritture di file JSON completi sono atomiche (file temporaneo + rename).
- L'autorizzazione del mittente è ricontrollata dentro `Send()` e un file mittenti illeggibile nega sempre l'invio.
- Destinatari con caratteri non validi o `..` vengono rifiutati (niente path traversal sulla share); validati tipo destinatario, lunghezza e `allowSendToAll`.
- Polling sovrapposti su share lente non si accumulano più.
- Se la UI non riesce a mostrare un messaggio, questo viene riproposto al polling successivo invece di restare bloccato.
- Un file di stato letto corrotto viene salvato come `.corrupt` prima di essere sostituito.

## [1.0.1] - 2026-06-20
### Corretto
- Il pulsante "HO LETTO" chiude la notifica al primo clic (la notifica non viene più riaperta dal polling).

## [1.0.0] - 2026-06-10
- Prima release pubblica.
