# Backend: cartella condivisa di rete

MustRead usa una cartella SMB condivisa come unico "server". Non richiede
database, web server o cloud.

## Struttura completa

```
\\SERVER\MustRead\
│
├── clients\
│   ├── PC001_DOMAIN_user1_1.json      ← heartbeat di ogni sessione attiva
│   └── RDSERVER01_DOMAIN_user2_5.json
│
├── inbox\
│   ├── users\
│   │   ├── DOMAIN_user1\              ← messaggi per utente specifico
│   │   │   └── 20260610-184500-8f3a.json
│   │   └── DOMAIN_user2\
│   │
│   ├── computers\
│   │   ├── PC001\                     ← messaggi per computer specifico
│   │   └── PC002\
│   │
│   └── all\                           ← messaggi broadcast
│       └── 20260610-190000-1ab2.json
│
├── history\
│   ├── sent.jsonl                     ← un record JSON per riga
│   ├── shown.jsonl
│   └── read.jsonl
│
└── config\
    ├── settings.json                  ← configurazione globale (opzionale)
    └── authorized_senders.json        ← lista mittenti autorizzati
```

## Formato file messaggio

```json
{
  "messageId": "20260610-184500-8f3a",
  "senderDomain": "DOMAIN",
  "senderUsername": "sender1",
  "senderComputer": "PC-SENDER",
  "targetType": "User",
  "target": "DOMAIN\\user1",
  "title": "MESSAGGIO IMPORTANTE",
  "body": "Testo del messaggio.",
  "createdAt": "2026-06-10T18:45:00",
  "requiresAcknowledgement": true
}
```

## Formato heartbeat client

```json
{
  "clientId": "PC001_DOMAIN_user1_1",
  "domain": "DOMAIN",
  "username": "user1",
  "computerName": "PC001",
  "sessionId": 1,
  "sessionName": "Console",
  "sessionType": "Local",
  "lastSeen": "2026-06-10T18:45:00"
}
```

## Accesso concorrente

I file JSONL in `history\` possono essere scritti da più client contemporaneamente.
MustRead usa un meccanismo di lock via file sidecar `.lock` con retry e backoff
per evitare corruzione dei dati. Se il lock non si ottiene entro ~4,5 secondi la
scrittura fallisce e viene registrata nel log locale: non si scrive mai senza lock.
I file JSON completi (messaggi, heartbeat) sono scritti su file temporaneo e poi
rinominati, così un lettore non vede mai un file a metà.

## Pulizia

MustRead non cancella automaticamente i messaggi dall'inbox dopo la lettura.
I messaggi vengono ignorati (non mostrati di nuovo) perché l'ID viene salvato
per utente in `%LOCALAPPDATA%\MustRead\read_messages.json`.
Lo stato è separato per utente Windows: su PC condivisi e server RDP la
conferma di un utente non influisce sugli altri. Al primo avvio viene importato
il vecchio file `C:\ProgramData\MustRead\read_messages.json` della v1.0.x,
per non rimostrare messaggi già letti.

I destinatari non cancellano mai i file dei messaggi. La pulizia la fanno le istanze
con l'invio abilitato e autorizzato, un minuto dopo l'avvio e poi ogni ora:

- **Messaggi scaduti.** Con `messageRetentionDays` maggiore di 0 il mittente scrive nel
  messaggio la scadenza (`expiresAt`). Dopo la scadenza il messaggio non viene più
  mostrato e viene rimosso dall'inbox insieme al suo file `.lock`. I messaggi senza
  `expiresAt` (inviati prima della 1.1) vengono rimossi `messageRetentionDays` giorni
  dopo la creazione. Con `messageRetentionDays` a 0 (default) restano per sempre, come
  nelle versioni precedenti.
- **Client inattivi.** I file in `clients\` di sessioni non viste da più di 7 giorni
  vengono rimossi; un client che torna li riscrive al primo heartbeat.

Il polling non rilegge i messaggi già confermati: il nome del file è l'ID del
messaggio, quindi viene saltato senza aprirlo. Il costo di un polling dipende dai
messaggi nuovi, non da quanti ne restano nell'inbox.
