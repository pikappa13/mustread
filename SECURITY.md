# Sicurezza

## Versioni supportate

Le correzioni di sicurezza vengono rilasciate solo sull'ultima versione pubblicata.

## Segnalare una vulnerabilità

Non aprire una issue pubblica per problemi di sicurezza.
Usa la segnalazione privata di GitHub: scheda **Security → Report a vulnerability** del repository.

Indica la versione, la configurazione della share (senza percorsi o nomi reali) e i passi per riprodurre il problema.

## Modello di sicurezza

MustRead non ha un server proprio: il confine di sicurezza sono i permessi NTFS/SMB della cartella condivisa.
`authorized_senders.json` e `requireAuthorizedSenders` limitano l'invio dall'applicazione, ma un utente con permesso di scrittura su `inbox\` può creare messaggi direttamente.
I messaggi sono file JSON in chiaro: non usare MustRead per informazioni riservate.
Vedi [docs/setup.md](docs/setup.md) e [docs/limitations.md](docs/limitations.md).
