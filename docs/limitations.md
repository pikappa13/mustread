# Limitazioni di MustRead

MustRead è uno strumento semplice per notifiche interne con conferma di lettura.
Non è un sistema di emergenza certificato.

## Limitazioni note

### PC spento o ibernato
MustRead non può recapitare messaggi a un PC spento. Il messaggio resta
nell'inbox e viene mostrato la prossima volta che il destinatario avvia MustRead.

### Schermata di login Windows
MustRead gira nella sessione utente loggata. Non compare sulla schermata di
login (GINA/Logon UI) né prima che l'utente abbia effettuato l'accesso.

### Lock screen
Se il PC è bloccato (Win+L), MustRead non bypassa la lock screen. Il messaggio
viene mostrato dopo che l'utente sblocca la sessione.

### Sessioni RDP
MustRead funziona correttamente dentro una sessione RDP, purché l'eseguibile
sia avviato dall'utente nella propria sessione RDP. Non avvia notifiche in
sessioni di altri utenti sullo stesso server.

### Terminazione forzata
Un amministratore locale può terminare il processo MustRead tramite Task Manager
o altri strumenti di sistema. In quel caso il messaggio rimane non confermato.

### Connettività di rete
MustRead richiede accesso continuo alla cartella condivisa. In caso di
disconnessione di rete, i messaggi non vengono ricevuti finché la connessione
non è ripristinata.

### Sicurezza file
I messaggi sono file JSON in chiaro. Chiunque abbia accesso alla cartella
condivisa può leggerli. Non usare MustRead per informazioni riservate.

### Nessuna garanzia di consegna certificata
MustRead non è equiparabile a sistemi di notifica certificati (PEC, ecc.).
È uno strumento operativo interno semplice.
