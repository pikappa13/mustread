# Setup MustRead

## Prerequisiti

- Windows 10 / Windows 11 x64 (anche Windows Server con sessioni RDP)
- Nessun runtime da installare: exe e MSI includono .NET 8
- Una cartella condivisa SMB con i permessi descritti sotto

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

I permessi NTFS sono il vero confine di sicurezza di MustRead. Il modello consigliato usa tre gruppi di dominio
(i nomi sono esempi):

| Gruppo | Chi | Serve per |
|---|---|---|
| `MustRead-Users` | tutti gli utenti che ricevono | leggere inbox e config, scrivere heartbeat e storico |
| `MustRead-Senders` | chi invia messaggi | in più: scrivere nell'inbox |
| `MustRead-Admins` | amministratori | controllo completo, unici a modificare `config\` |

| Cartella | MustRead-Users | MustRead-Senders | MustRead-Admins |
|---|:---:|:---:|:---:|
| `MustRead\` (radice) | Lettura | Lettura | Completo |
| `config\` | Lettura | Lettura | Completo |
| `inbox\` | Lettura | **Modifica** | Completo |
| `clients\` | Modifica | Modifica | Completo |
| `history\` | Modifica | Modifica | Completo |

Comandi da eseguire sul file server come amministratore (sostituire `D:\MustRead` e `DOMINIO`):

```bat
:: Share: l'accesso effettivo è deciso da NTFS
net share MustRead=D:\MustRead /GRANT:"Authenticated Users",CHANGE

mkdir D:\MustRead\config D:\MustRead\inbox D:\MustRead\clients D:\MustRead\history

:: Radice: niente permessi ereditati, solo quelli espliciti
icacls D:\MustRead /inheritance:r
icacls D:\MustRead /grant "SYSTEM:(OI)(CI)F" "BUILTIN\Administrators:(OI)(CI)F" "DOMINIO\MustRead-Admins:(OI)(CI)F"
icacls D:\MustRead /grant "DOMINIO\MustRead-Users:(OI)(CI)RX" "DOMINIO\MustRead-Senders:(OI)(CI)RX"

:: Scritture necessarie a tutti i client
icacls D:\MustRead\clients /grant "DOMINIO\MustRead-Users:(OI)(CI)M" "DOMINIO\MustRead-Senders:(OI)(CI)M"
icacls D:\MustRead\history /grant "DOMINIO\MustRead-Users:(OI)(CI)M" "DOMINIO\MustRead-Senders:(OI)(CI)M"

:: Solo i mittenti scrivono nell'inbox
icacls D:\MustRead\inbox /grant "DOMINIO\MustRead-Senders:(OI)(CI)M"
```

Per verificare: dal client di un destinatario e di un mittente usare **tray → Verifica connessione...**.
Il destinatario deve risultare in lettura sulle inbox e in scrittura su `clients` e `history`;
solo il mittente deve riuscire a inviare.

Limiti noti di questo modello (dovuti al backend su file, senza server):

- chi può scrivere in `history\` può anche modificare o cancellare righe dello storico;
- chi può scrivere in `clients\` può alterare l'heartbeat di altri client;
- chi può scrivere in `inbox\` può creare messaggi senza passare da MustRead, quindi
  `authorized_senders.json` è un controllo applicativo aggiuntivo, non un confine di sicurezza.

Per un audit non modificabile serve un backend con server (vedi roadmap).

## 2. Installare MustRead sui client

### Opzione A — MSI (consigliata per più PC)

Scaricare `MustRead-vX.Y.Z-win-x64.msi` dalla pagina Releases e installarlo come
amministratore, anche in modo silenzioso:

```bat
msiexec /i MustRead-v1.1.0-win-x64.msi /qn SERVERPATH=\\SERVER\MustRead AUTOSTART=1
```

| Proprietà | Effetto |
|---|---|
| `SERVERPATH` | percorso della share, salvato in `HKLM\SOFTWARE\MustRead\ServerPath`; usato se `settings.json` non ne indica uno |
| `AUTOSTART=1` | avvia MustRead a ogni accesso di qualunque utente (`HKLM\...\Run`) |

L'MSI installa in `C:\Program Files\MustRead`, crea il collegamento nel menu Start
e mantiene le proprietà negli aggiornamenti. Si distribuisce con GPO (Installazione
software), Intune (app line-of-business) o script.

Con l'avvio automatico dell'MSI non serve attivare quello per utente: se entrambi
sono presenti, la seconda istanza si chiude da sola senza messaggi.

### Opzione B — eseguibile singolo

Copiare `MustRead.exe` in una cartella locale di ogni PC ed eseguirlo.

### Imporre il percorso con i criteri di gruppo

Il valore stringa `ServerPath` in `HKLM\SOFTWARE\Policies\MustRead` (impostabile
con GPO → Preferenze → Registro) ha la precedenza su tutto e rende il campo non
modificabile nelle impostazioni.

## 3. Configurare ogni client

Se il percorso non arriva da MSI o criteri, al primo avvio si apre la schermata di
configurazione. In alternativa creare il file:

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

## 4. (Opzionale) Configurare i mittenti autorizzati

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

## 5. Avviare MustRead

Eseguire `MustRead.exe`. L'applicazione si minimizza nella tray icon.

- **Doppio clic** sull'icona → apre la schermata di invio
- **Clic destro** → Invia messaggio, Storico, Verifica connessione, Avvia con Windows, Impostazioni, Esci
- **Passando sopra l'icona** si vede lo stato: connesso, cartella non raggiungibile,
  conferme ancora da sincronizzare

## 6. Avvio automatico con Windows

- **Per tutti gli utenti:** installare l'MSI con `AUTOSTART=1`.
- **Per l'utente corrente:** tray → **Avvia con Windows**. Scrive in
  `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, senza privilegi di amministratore.
  Nella prima configurazione è disponibile anche come casella nelle impostazioni.

## 7. Verifica e log diagnostici

**Tray → Verifica connessione...** controlla configurazione, raggiungibilità e latenza
della share, permessi di lettura delle inbox, permessi di scrittura su `clients` e
`history`, autorizzazione all'invio e conferme in coda. Il report si può copiare: prima
di incollarlo in una issue pubblica, togliere nomi di server e utenti.

Ogni utente ha un log locale in `%LOCALAPPDATA%\MustRead\logs\mustread-AAAAMMGG.log`
(conservato 14 giorni). Contiene errori di rete, lock e permessi, ma mai il
testo dei messaggi. È il primo posto da controllare se un messaggio non arriva
o una conferma non compare nello storico.
