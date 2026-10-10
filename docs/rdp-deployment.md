# Deployment su server RDP / Terminal Server

## Come funziona con RDP

MustRead deve girare **dentro la sessione RDP dell'utente**, non come servizio di sistema.

Ogni utente che si collega via RDP deve avere MustRead avviato nella propria sessione.
Il programma rileva automaticamente il tipo di sessione leggendo la variabile `SESSIONNAME`:

- `Console` → sessione locale
- `RDP-Tcp#N` → sessione RDP

Queste informazioni vengono registrate nell'heartbeat client e nello storico.

## Avvio automatico su Terminal Server

### Metodo consigliato: MSI con AUTOSTART

```bat
msiexec /i MustRead-v1.1.0-win-x64.msi /qn SERVERPATH=\\SERVER\MustRead AUTOSTART=1
```

MustRead parte in ogni sessione di ogni utente, comprese quelle RDP.
Un solo processo per sessione: un secondo avvio nella stessa sessione si chiude da solo.

### Metodo 1: Startup folder per utente

Aggiungere `MustRead.exe` (o un collegamento) alla cartella:

```
C:\Users\<utente>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup
```

### Metodo 2: Group Policy (consigliato in dominio)

Tramite GPO (User Configuration → Windows Settings → Scripts → Logon):

```
MustRead.exe
```

Questo garantisce che MustRead parta ad ogni logon, incluso quello RDP.

### Metodo 3: Logon script via Active Directory

Aggiungere `MustRead.exe` come script di logon nell'account utente AD.

## Più utenti e più sessioni sullo stesso server

Lo stato dei messaggi letti è **per utente** (`%LOCALAPPDATA%\MustRead\read_messages.json`):
la conferma di un utente non nasconde il messaggio agli altri utenti dello stesso server.

Se lo stesso utente apre più sessioni RDP sullo stesso server, ogni sessione ha il
proprio agent (il `clientId` include il `sessionId`), ma le sessioni condividono lo
stato letto:

- un messaggio già aperto in più sessioni contemporaneamente va confermato in ognuna;
- una volta confermato in una sessione, non viene più aperto nelle sessioni dove non
  era ancora comparso (entro un ciclo di polling).

Vedi [delivery-semantics.md](delivery-semantics.md) per le regole complete.
