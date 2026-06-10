# Deployment su server RDP / Terminal Server

## Come funziona con RDP

MustRead deve girare **dentro la sessione RDP dell'utente**, non come servizio di sistema.

Ogni utente che si collega via RDP deve avere MustRead avviato nella propria sessione.
Il programma rileva automaticamente il tipo di sessione leggendo la variabile `SESSIONNAME`:

- `Console` → sessione locale
- `RDP-Tcp#N` → sessione RDP

Queste informazioni vengono registrate nell'heartbeat client e nello storico.

## Avvio automatico su Terminal Server

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

## Nota su sessioni multiple

Se lo stesso utente apre più sessioni RDP sullo stesso server, ogni sessione
avrà il proprio agent. Il `clientId` include il `sessionId` per distinguerle.

Un messaggio inviato a `DOMAIN\user1` verrà mostrato in **tutte** le sessioni
attive di quell'utente, perché tutte leggono dalla stessa inbox utente.
