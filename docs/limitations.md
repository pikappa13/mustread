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

### Terminazione forzata e crash
Un amministratore locale può terminare il processo MustRead tramite Task Manager
o altri strumenti di sistema. In quel caso il messaggio rimane non confermato.
Se invece MustRead va in crash o smette di rispondere dopo almeno 60 secondi di
esecuzione, Windows lo riavvia (Windows Error Reporting, `RegisterApplicationRestart`).
Una terminazione da Task Manager non provoca il riavvio: il messaggio ricompare al
successivo avvio di MustRead.

### Più monitor
L'alert occupa il monitor principale; gli altri monitor vengono oscurati con un
avviso finché il messaggio non è confermato. L'alert torna in primo piano ogni due
secondi senza prendere il focus della tastiera, così chi sta scrivendo altrove non
lo conferma per errore.

### Mittente dichiarato
Il mittente mostrato nell'alert è quello scritto nel file del messaggio. È
attendibile solo se il permesso di scrittura su `inbox\` è riservato ai mittenti,
come indicato nella matrice ACL di [setup.md](setup.md).

### Connettività di rete
MustRead richiede accesso alla cartella condivisa per ricevere messaggi. In caso di
disconnessione i nuovi messaggi arrivano solo quando la connessione torna; l'icona
nella tray lo segnala. Le conferme di lettura date mentre la share non è
raggiungibile restano in una coda locale e vengono scritte nello storico al ritorno
della connessione (possono comparire una seconda volta in caso di crash a metà
scrittura, mai andare perse).

### Storico modificabile
Lo storico è un insieme di file sulla share: chi ha permesso di scrittura su
`history\` può alterarlo. Non è un registro di audit a prova di manomissione.

### Sicurezza file
I messaggi sono file JSON in chiaro. Chiunque abbia accesso alla cartella
condivisa può leggerli. Non usare MustRead per informazioni riservate.

### Nessuna garanzia di consegna certificata
MustRead non è equiparabile a sistemi di notifica certificati (PEC, ecc.).
È uno strumento operativo interno semplice.
