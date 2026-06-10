# Setup MustRead

## Prerequisiti

- Windows 10 / Windows 11
- .NET 8 Runtime (oppure usa il build self-contained)
- Una cartella condivisa accessibile in lettura/scrittura da tutti i PC

## 1. Creare la cartella condivisa

Sul server (o su qualsiasi PC accessibile in rete):

```
\\SERVER\MustRead\
```

Struttura minima da creare manualmente (o lasciare che MustRead la crei automaticamente):

```
MustRead\
├── clients\
├── inbox\
│   ├── users\
│   ├── computers\
│   └── all\
├── history\
└── config\
```

### Permessi cartella condivisa

| Cartella/File          | Tutti i client | Solo mittenti |
|------------------------|:--------------:|:-------------:|
| config\ (lettura)      | ✔              |               |
| clients\ (scrittura)   | ✔              |               |
| inbox\ (lettura)       | ✔              |               |
| inbox\ (scrittura)     |                | ✔             |
| history\ (scrittura)   | ✔              |               |

## 2. Configurare ogni client

Creare il file:

```
C:\ProgramData\MustRead\settings.json
```

Copiare il contenuto da `examples\settings.example.json` e adattare:

```json
{
  "serverPath": "\\\\SERVER\\MustRead",
  "enableSender": true,
  "enableReceiver": true,
  "pollingSeconds": 3,
  "allowSendToAll": true,
  "applicationName": "MustRead"
}
```

- `enableSender`: mettere `false` sui PC che non devono poter inviare messaggi
- `enableReceiver`: mettere `false` per disabilitare completamente la ricezione
- `pollingSeconds`: frequenza di controllo nuovi messaggi

## 3. (Opzionale) Configurare i mittenti autorizzati

Copiare `examples\authorized_senders.example.json` in:

```
\\SERVER\MustRead\config\authorized_senders.json
```

e inserire le identità Windows autorizzate all'invio:

```json
[
  "DOMAIN\\admin1",
  "DOMAIN\\sender1"
]
```

Se il file non esiste, chiunque con `enableSender: true` può inviare.

## 4. Avviare MustRead

Eseguire `MustRead.exe`. L'applicazione si minimizza nella tray icon.

- **Doppio clic** sull'icona → apre la schermata di invio
- **Clic destro** → menu con "Invia messaggio", "Storico", "Esci"

## 5. Avvio automatico con Windows

MustRead include la funzione integrata di autostart:

1. Clic destro sull'icona nella tray → **Impostazioni**
2. Spuntare **"Avvia automaticamente con Windows"**

MustRead aggiunge automaticamente una voce in `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` — senza richiedere privilegi di amministratore.

Per deployment aziendale su più PC, usare Group Policy o uno script di logon che aggiunga la stessa voce di registro per ogni utente.
