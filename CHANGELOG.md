# Changelog

Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/) e il progetto usa il [Semantic Versioning](https://semver.org/lang/it/).

## [Non rilasciato]

### Aggiunto
- Log diagnostico locale per utente in `%LOCALAPPDATA%\MustRead\logs` (14 giorni, senza testo dei messaggi).
- Opzione `requireAuthorizedSenders` per negare l'invio quando `authorized_senders.json` manca.
- Solution `MustRead.sln`, suite di test versionata e CI GitHub Actions (build con warning come errori, test, coverage, scansione vulnerabilità, publish).

### Modificato
- Lo stato dei messaggi letti è per utente (`%LOCALAPPDATA%\MustRead\read_messages.json`) invece che per macchina; il vecchio file v1.0.x viene importato al primo avvio.
- Lo stato letto viene unito a quello su disco sotto lock: più istanze dello stesso utente (es. sessioni RDP) non si sovrascrivono le conferme.
- Gli ID messaggio hanno 12 caratteri casuali invece di 4.

### Corretto
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
