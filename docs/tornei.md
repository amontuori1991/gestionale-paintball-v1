# Tornei

## Accesso e organizzazione

- Dashboard > Torneo: Admin e Staff possono creare, modificare ed eliminare eventi.
- Il pulsante partecipa alle preferenze di visibilita e ordinamento della dashboard.
- L'evento non crea una Partita, non occupa disponibilita e non assegna staff. Inserire la chiusura campo separatamente. Nelle prenotazioni compare una banda a tutta larghezza nel giorno e nella settimana dell'evento, anche senza altre partite, con nome, data, ora e tasto Gestisci. Gli eventi passati seguono la finestra storica delle prenotazioni; i filtri data si applicano anche alle bande.
- Da 2 a 64 squadre; almeno due per girone. Le squadre sono distribuite automaticamente e possono essere spostate prima di generare gli incontri.
- Dopo la creazione, il numero di squadre resta fisso per conservare i link assegnati. Dopo la generazione del calendario, regole e gironi sono bloccati; nomi e contatti restano modificabili.

## Incontri

- Gironi di sola andata o andata/ritorno con turni e riposi automatici.
- Punti configurabili per vittoria, pareggio e sconfitta, anche frazionari.
- Punteggio del match facoltativo. Se attivo, deve corrispondere all'esito. Nelle eliminatorie un punteggio pari richiede comunque la scelta della vincitrice dello spareggio.
- Classifica: punti, poi differenza punti e punti fatti solo quando il punteggio e attivo. Per le parita residue lo staff assegna priorita di spareggio positive e distinte; il numero minore precede gli altri.
- Le finali vengono generate dopo tutti i risultati dei gironi. Il tabellone gestisce turni liberi per numeri non potenze di due; i primi abbinamenti incrociano gironi diversi quando presenti.
- Il turno seguente viene creato automaticamente alla conclusione del precedente. Da quel momento i risultati del turno precedente non si possono modificare. Terzo posto opzionale, con almeno quattro qualificate.
- Ogni aggiornamento controlla una versione del torneo per evitare sovrascritture fra operatori.

## Iscrizioni e foto

- Ogni squadra ha un link con token casuale, copiabile e condivisibile via WhatsApp al referente.
- Il form riusa firma, validazioni e notifica email del tesseramento pubblico. Un tesseramento valido viene riutilizzato senza duplicare l'anagrafica; la partecipazione resta associata alla squadra.
- Un unico album per evento riusa il sistema foto esistente: logo, formati immagine inclusi HEIC/HEIF, esclusione video, avanzamento, cancellazione, download singolo/ZIP e scadenza a sette giorni.
- I pulsanti WhatsApp aprono una bozza; non inviano messaggi automaticamente.

## Eliminazione

- Conferma esplicita: elimina evento, squadre, risultati e associazioni.
- Rimuove soltanto tesseramenti nuovi esclusivi dell'evento non esportati e non utilizzati altrove.
- Conserva tesseramenti preesistenti, esportati ACSI, con tessera gia assegnata o riutilizzati. Una tessera assegnata e una protezione prudenziale anche per export storici non tracciati.
- Le firme non piu referenziate vengono ripulite dopo il commit, limitatamente ai file generati dall'applicazione nella cartella Firme. I link foto pubblici cessano di funzionare; gli oggetti foto seguono la scadenza dell'archivio.
- La chiusura campo inserita manualmente resta indipendente e va verificata separatamente.

## Distribuzione e verifiche

Lo schema viene aggiornato in modo idempotente da TorneoSchema all'avvio, senza creare prenotazioni o alterare le disponibilita esistenti.

Suite dedicate: tests/Tornei.Checks, tests/TorneoRegistrations.Checks, tests/PhotoAlbums.Checks e tests/BookingViews.Checks/torneo-browser.cjs. I test database usano database locali usa e getta; nessun test richiede credenziali di produzione.
