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
Per i deployment protetti impostare `"requireAuthorizedSenders": true` nel
`settings.json` dei client: in quel caso, se il file manca, nessuno può inviare.

Se il file esiste ma è illeggibile o malformato, l'invio viene sempre negato
(fail-closed). L'autorizzazione viene ricontrollata a ogni invio, non solo
all'apertura della finestra.

> I permessi NTFS/SMB della share restano il vero confine di sicurezza: un utente
> che può scrivere direttamente in `inbox\` può creare messaggi senza passare da
> MustRead. Concedere la scrittura su `inbox\` solo ai mittenti.

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

## 6. Log diagnostici

Ogni utente ha un log locale in `%LOCALAPPDATA%\MustRead\logs\mustread-AAAAMMGG.log`
(conservato 14 giorni). Contiene errori di rete, lock e permessi, ma mai il
testo dei messaggi. È il primo posto da controllare se un messaggio non arriva
o una conferma non compare nello storico.
