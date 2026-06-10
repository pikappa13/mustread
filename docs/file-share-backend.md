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
MustRead usa un meccanismo di lock via file sidecar `.lock` con retry automatico
per evitare corruzione dei dati.

## Pulizia

MustRead non cancella automaticamente i messaggi dall'inbox dopo la lettura.
I messaggi vengono ignorati (non mostrati di nuovo) perché l'ID viene salvato
in `C:\ProgramData\MustRead\read_messages.json` su ogni client.

Periodicamente si può svuotare manualmente le cartelle inbox o implementare
uno script di cleanup.
