# Regole di consegna e conferma

Questo documento fissa cosa significa "consegnato" e "letto" in MustRead.
Le regole sono verificate dai test in `src/MustRead.Tests` (in particolare
`MessageReceiverServiceTests`, `HistoryOutboxTests` e `MultiProcessTests`).

## Chi deve confermare

Lo stato dei messaggi letti è salvato **per utente Windows e per computer**
(`%LOCALAPPDATA%\MustRead\read_messages.json`, profilo locale).

| Destinatario | Chi vede il messaggio | Conferme registrate |
|---|---|---|
| Utente (`DOMINIO\mario`) | ogni sessione di quell'utente, su ogni computer dove MustRead è avviato | una per computer su cui l'utente lo vede |
| Computer (`PC-01`) | ogni utente con MustRead avviato su quel computer | una per utente |
| Tutti | ogni utente su ogni computer | una per utente per computer |

Più sessioni dello stesso utente sullo stesso computer (RDP) condividono lo stato:

- un messaggio già aperto in più sessioni va confermato in ognuna;
- dopo la prima conferma, le sessioni in cui non era ancora comparso non lo aprono più.

## Destinatari offline

I messaggi restano nell'inbox sulla share e non vengono mai cancellati dai destinatari.
Un destinatario che si collega dopo l'invio vede tutti i messaggi a lui destinati che
non ha ancora confermato, anche se inviati prima della sua prima connessione.

## Cosa succede al clic su "HO LETTO"

1. Il pulsante si disattiva (nessuna doppia conferma).
2. L'ID viene aggiunto allo stato letto locale, con scrittura atomica sotto lock e
   unione con quanto scritto da altre sessioni dello stesso utente.
3. Il record di lettura viene accodato in `%LOCALAPPDATA%\MustRead\outbox`.
4. La finestra si chiude.
5. In background il record viene scritto in `history\read.jsonl` sulla share; se la
   share non è raggiungibile resta in coda e viene ritentato ogni 30 secondi e a ogni
   avvio.

Se il salvataggio locale del punto 2 fallisce (disco pieno, permessi), la finestra si
chiude comunque e l'errore va nel log: nella sessione in corso il messaggio non
riappare, ma dopo un riavvio di MustRead verrà mostrato di nuovo. È una scelta
voluta: rimostrare un messaggio è preferibile a perdere una conferma.

## Garanzie dello storico

- **Almeno una volta:** ogni record arriva sulla share; un crash tra scrittura e
  pulizia della coda può produrre una riga duplicata. Chi analizza lo storico deve
  deduplicare per `messageId` + utente + computer.
- **Ordine:** i record di uno stesso utente arrivano nell'ordine in cui sono stati creati.
- **Nessuna scrittura senza lock:** se il lock su un file della share non si ottiene
  entro circa 4,5 secondi l'operazione fallisce e viene ritentata, non forzata.

## Cosa MustRead non garantisce

- Che il messaggio sia stato effettivamente letto: la conferma attesta il clic.
- La consegna a PC spenti, bloccati o senza MustRead avviato.
- Uno storico non modificabile: vedi [limitations.md](limitations.md).
