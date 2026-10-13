# Pianificatore eventi

Pagina dedicata, raggiungibile da un pulsante nelle statistiche e accessibile con la stessa policy Statistiche. Solo lettura:
nessuna prenotazione, chiusura, assegnazione staff o modifica al simulatore Campo 2.

- Durata 60-600 minuti a passi di 30, default 4 ore; date future entro due anni,
  intervallo massimo 366 giorni; sabato, domenica o entrambi.
- Ricalcolo automatico con debounce e cancellazione delle richieste superate.
- Disponibilita tramite BuildGiorno: apertura, chiusure, tutte le partite attive
  anche non pagate, margini di 30 minuti, tramonto meno 15 minuti arrotondato in difetto.
- Tornei e amichevoli attive senza durata/orario completo: intera giornata esclusa,
  solo nel pianificatore. Non viene alterata la disponibilita operativa.
- Confronto con un solo stesso giorno della settimana entro +/-3 giorni dalla data
  equivalente dell'anno precedente, solo nello stesso anno e prima di oggi.
- Esclusi intervalli storici chiusi, oltre la luce disponibile, con eventi senza
  orari, o precedenti alla prima prenotazione del periodo caricato.
- Occupazione = minuti coperti da partite confermate / minuti osservati.
  Sovrapposizioni conteggiate una volta; nessun dato personale restituito.
- Una proposta per giorno, poi classifica: storico disponibile, minore
  occupazione, campione maggiore, prima data. A parita, primo orario.
- Dati mancanti NON classificati come zero; conteggi mensili manuali non usati.
  La presenza di una prima prenotazione non certifica la completezza dello storico:
  UI espone limite e data confrontata. Non e una previsione di domanda.
- Nessun ripiego su altri weekend se la data equivalente non e utilizzabile.
- Tre risultati iniziali, poi altre tre proposte per click. Percentuali non esposte:
  viene mostrato il numero di partite confermate nella stessa fascia della data storica.

Test: `dotnet run --project tests/EventPlanner.Checks`.
