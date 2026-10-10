# Guida per contributori e agenti

MustRead è un progetto open source pubblico. Tutto ciò che entra nel repository è visibile a chiunque.

## Comandi

```bash
dotnet build MustRead.sln -c Release -warnaserror   # deve restare senza warning
dotnet test MustRead.sln -c Release                 # tutti i test devono passare
```

Installer (richiede `out/MustRead.exe` pubblicato, vedi `installer/MustRead.Installer.wixproj`):

```bash
dotnet build installer/MustRead.Installer.wixproj -c Release -p:ProductVersion=1.1.0
```

`installer/Test-Installer.ps1` installa davvero l'MSI: eseguirlo solo su macchine di test (la CI lo fa).

## Invarianti da non rompere

1. I file dei messaggi nell'inbox non vengono mai cancellati dai destinatari: servono a più destinatari.
2. Un messaggio confermato non deve riaprirsi. In `MessageReceiverService`, `MarkAsRead` aggiorna
   gli insiemi in memoria e salva `read_messages.json` dentro lo stesso lock `_sync`.
3. Nessuna scrittura su file condivisi senza lock: `FileLockHelper` solleva `TimeoutException`, mai fallback.
4. Le scritture di file JSON completi passano da file temporaneo + rename (`FileLockHelper.WriteAtomic`).
5. Lo stato locale (letti, coda, log) è per utente in `%LOCALAPPDATA%\MustRead`.
6. L'I/O sulla share non deve bloccare il thread UI alla chiusura dell'alert.
7. L'autorizzazione all'invio è fail-closed e ricontrollata in `MessageSenderService.Send()`.
8. Nessun errore ignorato in silenzio: almeno una riga in `Log`, mai il testo dei messaggi.

Le regole di consegna sono in [docs/delivery-semantics.md](docs/delivery-semantics.md).

## Test

- I test non devono toccare lo stato reale della macchina: usare `TestableSettingsService`
  (percorsi in una cartella temporanea, registro HKLM finto). `TestSetup` reindirizza log e
  chiave di avvio automatico.
- Concorrenza tra processi: `MultiProcessTests` avvia più istanze di `MustRead.TestHost`.
- Ogni bug corretto riceve un test di regressione.

## Dati pubblici

Mai committare: percorsi di share reali, nomi di server, utenti o domini reali, credenziali,
file di configurazione locali, output di build (`out/`, `publish/`, `*.msi`), note interne.
Nei test usare nomi fittizi (`DOM`, `mario`, `PC01`).

## Release

1. Aggiornare `<Version>` in `src/MustRead.App/MustRead.App.csproj` e `CHANGELOG.md`.
2. Creare il tag `vX.Y.Z` (o `vX.Y.Z-rc.N` per una pre-release) sul commit di master.
3. Il workflow `release.yml` crea una release in bozza con exe, MSI, zip e `SHA256SUMS.txt`.
4. Controllare la bozza e pubblicarla a mano.
