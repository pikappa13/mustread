# MustRead — Notifiche Obbligatorie Windows per Reti Aziendali

> **Strumento open source per inviare notifiche aziendali obbligatorie su rete locale Windows.**  
> Messaggi a schermo intero che i dipendenti devono confermare con **"HO LETTO"**. Nessun server, nessun cloud, nessun database: funziona su cartella condivisa SMB.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)](https://github.com/pikappa13/mustread)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![CI](https://github.com/pikappa13/mustread/actions/workflows/ci.yml/badge.svg)](https://github.com/pikappa13/mustread/actions/workflows/ci.yml)

---

## Cos'è MustRead?

**MustRead** è un sistema di messaggistica interna per ambienti Windows aziendali che garantisce la **conferma di lettura obbligatoria** dei messaggi urgenti.

Quando un amministratore invia un messaggio tramite MustRead, sul PC del destinatario appare una **finestra a schermo intero non chiudibile** — senza pulsante X, senza ESC, senza Alt+F4 — che l'utente può chiudere **solo cliccando "HO LETTO"**.

Perfetto per:
- 📢 **Comunicazioni urgenti aziendali** (manutenzioni, allarmi, avvisi IT)
- 🏭 **Ambienti industriali e manifatturieri** con PC condivisi
- 🏫 **Scuole e università** con laboratori informatici
- 🏥 **Strutture sanitarie** con postazioni multiple
- 🖥️ **Server Terminal Services / RDP** con utenti multipli

---

## Funzionalità principali

| Funzionalità | Descrizione |
|---|---|
| 📺 **Notifica fullscreen** | Occupa tutto lo schermo, sempre in primo piano |
| ✅ **Conferma obbligatoria** | L'utente deve cliccare "HO LETTO" per chiudere |
| 👤 **Invio per utente** | Messaggi diretti a un utente Windows specifico |
| 🖥️ **Invio per computer** | Messaggi diretti a un PC specifico |
| 📣 **Broadcast** | Invio a tutti gli utenti connessi contemporaneamente |
| 📋 **Storico completo** | Log di messaggi inviati, mostrati e confermati |
| 🩺 **Log diagnostico** | Log locale per utente in `%LOCALAPPDATA%\MustRead\logs` |
| 🔌 **Offline delivery** | I messaggi restano in coda se il PC è offline |
| 🖧 **RDP / Terminal Server** | Funziona nelle sessioni Remote Desktop |
| 🔒 **Autorizzazione mittenti** | Solo gli utenti autorizzati possono inviare messaggi |
| ⚙️ **Zero dipendenze** | Solo una cartella SMB condivisa, nessun server |
| 🚀 **Avvio automatico** | Si avvia con Windows tramite registro HKCU |

---

## Come funziona

```
┌─────────────────┐     \\SERVER\MustRead\     ┌─────────────────┐
│   PC MITTENTE   │  ──── inbox/users/... ───▶ │  PC DESTINATARIO│
│                 │                             │                 │
│  [INVIA MSG]    │     cartella SMB            │  ┌───────────┐  │
│                 │                             │  │MESSAGGIO  │  │
└─────────────────┘                             │  │IMPORTANTE │  │
                                                │  │           │  │
                                                │  │[HO LETTO] │  │
                                                │  └───────────┘  │
                                                └─────────────────┘
```

1. Il mittente apre MustRead dalla tray icon e scrive il messaggio
2. Sceglie il destinatario: utente specifico, PC specifico, oppure **tutti**
3. Il messaggio viene scritto come file JSON nella cartella condivisa
4. L'agent sul PC destinatario rileva il file (polling ogni N secondi)
5. Appare la finestra fullscreen — l'utente non può ignorarla
6. Al click su **"HO LETTO"** il messaggio viene registrato nello storico

---

## Requisiti

- **Sistema operativo:** Windows 10 / Windows 11 (x64)
- **Runtime:** [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) oppure build self-contained (incluso)
- **Rete:** Cartella condivisa SMB accessibile in lettura/scrittura da tutti i PC
- **Permessi:** Nessun privilegio di amministratore richiesto per la ricezione

---

## Installazione rapida

### Opzione A — Eseguibile standalone (consigliata)

Scarica `MustRead.exe` dalla [pagina Releases](https://github.com/pikappa13/mustread/releases) e copialo su ogni PC. Non richiede l'installazione di .NET.

### Opzione B — Compilazione da sorgente

```bash
git clone https://github.com/pikappa13/mustread.git
cd mustread/src/MustRead.App
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Configurazione

### 1. Creare la cartella condivisa

Crea una cartella condivisa su un server o PC accessibile in rete:

```
\\SERVER\MustRead\
```

Struttura creata automaticamente da MustRead al primo avvio:

```
\\SERVER\MustRead\
├── clients\          ← presenza online dei client (heartbeat)
├── inbox\
│   ├── users\        ← messaggi per utente (es. DOMAIN_mario)
│   ├── computers\    ← messaggi per computer (es. PC-UFFICIO01)
│   └── all\          ← messaggi broadcast per tutti
├── history\
│   ├── sent.jsonl    ← log messaggi inviati
│   ├── shown.jsonl   ← log messaggi mostrati a schermo
│   └── read.jsonl    ← log conferme di lettura
└── config\
    └── authorized_senders.json   ← utenti autorizzati all'invio
```

### 2. Configurare ogni client

Al primo avvio, MustRead apre automaticamente la schermata di configurazione.  
In alternativa, crea `C:\ProgramData\MustRead\settings.json`:

```json
{
  "serverPath": "\\\\SERVER\\MustRead",
  "enableSender": true,
  "enableReceiver": true,
  "pollingSeconds": 3,
  "allowSendToAll": true,
  "applicationName": "MustRead",
  "requireAuthorizedSenders": false
}
```

| Parametro | Descrizione |
|---|---|
| `serverPath` | Percorso UNC della cartella condivisa |
| `enableSender` | Abilita la schermata di invio (solo per admin) |
| `enableReceiver` | Abilita la ricezione notifiche in background |
| `pollingSeconds` | Frequenza di controllo nuovi messaggi (secondi) |
| `allowSendToAll` | Abilita il broadcast a tutti gli utenti |
| `applicationName` | Nome visualizzato nell'interfaccia |
| `requireAuthorizedSenders` | Se `true`, senza `authorized_senders.json` nessuno può inviare |

### 3. Configurare i mittenti autorizzati (opzionale)

Crea `\\SERVER\MustRead\config\authorized_senders.json`:

```json
[
  "DOMAIN\\admin1",
  "DOMAIN\\it_support"
]
```

Se il file non esiste, chiunque con `enableSender: true` può inviare messaggi, a meno che `requireAuthorizedSenders` sia `true`. Un file illeggibile o malformato nega sempre l'invio.

### 4. Avvio automatico con Windows

Apri **Impostazioni** dalla tray icon e spunta **"Avvia automaticamente con Windows"**.  
MustRead aggiunge una voce in `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` — senza privilegi di amministratore.

---

## Utilizzo

### Inviare un messaggio

1. Doppio clic sull'icona MustRead nella tray (in basso a destra)
2. Seleziona uno o più destinatari dalla lista degli utenti online
3. Scrivi il testo del messaggio
4. Clicca **INVIA**

I destinatari online ricevono la notifica entro `pollingSeconds` secondi.  
I destinatari offline ricevono il messaggio al prossimo avvio di MustRead.

### Ricevere una notifica

MustRead gira in background nella system tray. Quando arriva un messaggio:

- Appare automaticamente una **finestra a schermo intero**
- Non può essere chiusa con ESC, Alt+F4 o il pulsante X
- L'utente deve cliccare **"HO LETTO"** per confermare e chiudere
- Se arrivano più messaggi, vengono mostrati in sequenza

### Visualizzare lo storico

Clic destro sull'icona tray → **Storico**  
Tre tab: messaggi inviati, messaggi mostrati, messaggi confermati.

---

## Supporto RDP e Terminal Server

MustRead è compatibile con sessioni **Remote Desktop (RDP)** e **Terminal Server**:

- Funziona in ogni sessione RDP attiva purché MustRead sia avviato nella sessione
- Rileva automaticamente il tipo di sessione (`Local` / `RDP`)
- In ambienti Terminal Server, usa Group Policy o logon script per avviare MustRead per ogni utente
- Il `clientId` include il `sessionId` per distinguere sessioni multiple dello stesso utente

Vedere [docs/rdp-deployment.md](docs/rdp-deployment.md) per le istruzioni dettagliate.

---

## Domande frequenti (FAQ)

**Q: Serve un server dedicato?**  
A: No. Basta una cartella condivisa Windows su qualsiasi PC o NAS della rete.

**Q: Funziona senza Internet?**  
A: Sì, MustRead funziona interamente sulla rete locale (LAN). Non richiede connessione Internet.

**Q: Un utente può ignorare il messaggio?**  
A: La finestra è fullscreen, sempre in primo piano, senza pulsanti di chiusura. Un utente tecnico con accesso al Task Manager può terminare il processo, ma questo viene documentato nelle limitazioni.

**Q: Funziona con Active Directory?**  
A: Sì. MustRead usa `Environment.UserDomainName` e `Environment.UserName` — compatibile con utenti di dominio AD.

**Q: Quanti PC supporta?**  
A: Non c'è un limite tecnico. Le prestazioni dipendono dalla velocità della rete e del file server.

**Q: I messaggi sono cifrati?**  
A: No. I messaggi sono file JSON in chiaro. La sicurezza è delegata ai permessi NTFS della cartella condivisa. Non usare MustRead per informazioni riservate.

**Q: Funziona su Windows 7?**  
A: No. Richiede Windows 10 o Windows 11 con .NET 8. Per Windows 7 sarebbe necessario un porting a .NET Framework 4.8.

---

## Limitazioni note

- ❌ Non funziona se il PC è **spento o ibernato**
- ❌ Non mostra messaggi sulla **schermata di login** Windows
- ❌ Non bypassa la **lock screen** (schermo bloccato)
- ❌ Un amministratore può **terminare il processo** MustRead
- ❌ Non è un sistema di **emergenza certificato**
- ❌ I messaggi sono **in chiaro** (file JSON non cifrati)
- ⚠️ Per RDP: l'agent deve girare **dentro la sessione** dell'utente

Vedere [docs/limitations.md](docs/limitations.md) per i dettagli.

---

## Struttura del progetto

```
mustread/
├── src/
│   ├── MustRead.App/           ← Applicazione principale (WinForms .NET 8)
│   │   ├── Forms/              ← UI: SenderForm, AlertForm, HistoryForm, SettingsForm
│   │   ├── Services/           ← Business logic: invio, ricezione, storico, registro
│   │   ├── Models/             ← Modelli dati: AlertMessage, ClientInfo, AppSettings
│   │   └── Utils/              ← FileLockHelper, JsonHelper, UiHelper
│   └── MustRead.Tests/         ← Suite di test xUnit
├── .github/workflows/          ← CI: build, test, coverage, scansione vulnerabilità
├── examples/                   ← File di configurazione di esempio
├── docs/                       ← Documentazione setup, RDP, limitazioni
├── MustRead.sln
├── CHANGELOG.md
├── README.md
└── LICENSE
```

---

## Roadmap

Il piano tecnico completo, con priorità, dipendenze, criteri di accettazione e gate di rilascio, è disponibile in [docs/production-roadmap.md](docs/production-roadmap.md).

### v1.1 — Affidabilità
- [x] Test versionati e CI su GitHub Actions
- [x] Stato dei messaggi letti per utente (PC condivisi e RDP)
- [x] Scritture con lock obbligatorio e atomiche
- [x] Autorizzazione mittenti fail-closed e validazione destinatari
- [x] Log diagnostico locale
- [ ] Coda persistente per le conferme da sincronizzare sulla share
- [ ] Test end-to-end multi-processo e multi-sessione RDP
- [ ] Installer MSI / MSIX ed eseguibile firmato

### v1.2 — Operatività e UX
- [ ] Ricevute di lettura per destinatario visibili al mittente
- [ ] Storico con filtri, ricerca ed export CSV
- [ ] Livelli info / warning / critical e suono configurabile
- [ ] Interfaccia in italiano e inglese, accessibilità da tastiera

### v1.3 e oltre
- [ ] Gruppi / reparti e integrazione opzionale con Active Directory
- [ ] Backend HTTP opzionale (alternativa alla cartella SMB)

---

## Contribuire

Pull request benvenute! Per modifiche significative, apri prima una issue per discutere l'approccio.

```bash
git clone https://github.com/pikappa13/mustread.git
cd mustread
dotnet test MustRead.sln   # tutti i test devono passare (la CI li esegue a ogni push)
```

---

## Licenza

Distribuito sotto licenza **MIT**. Vedere [LICENSE](LICENSE) per i dettagli.

---

## Autore

Sviluppato da **Patrick Battistini**

---

*MustRead — Perché ogni messaggio importante merita di essere letto.*
