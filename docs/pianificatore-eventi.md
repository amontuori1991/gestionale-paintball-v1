# Pianificatore eventi

Sezione nelle statistiche, accessibile con la stessa policy Statistiche. Solo lettura:
nessuna prenotazione, chiusura, assegnazione staff o modifica al simulatore Campo 2.

- Durata 60-600 minuti a passi di 30, default 4 ore; date future entro due anni,
  intervallo massimo 366 giorni; sabato, domenica o entrambi.
- Ricalcolo automatico con debounce e cancellazione delle richieste superate.
- Disponibilita tramite BuildGiorno: apertura, chiusure, tutte le partite attive
  anche non pagate, margini di 30 minuti, tramonto meno 15 minuti arrotondato in difetto.
- Tornei e amichevoli attive senza durata/orario completo: intera giornata esclusa,
  solo nel pianificatore. Non viene alterata la disponibilita operativa.
- Confronto con lo stesso giorno della settimana entro +/-21 giorni dalla data
  equivalente dell'anno precedente, solo nello stesso anno e prima di oggi.
- Esclusi intervalli storici chiusi, oltre la luce disponibile, con eventi senza
  orari, o precedenti alla prima prenotazione del periodo caricato.
- Occupazione = minuti coperti da partite confermate / minuti osservati.
  Sovrapposizioni conteggiate una volta; nessun dato personale restituito.
- Una proposta per giorno, poi classifica: campione almeno 3 giorni, minore
  occupazione, campione maggiore, prima data. A parita, primo orario.
- Dati mancanti NON classificati come zero; conteggi mensili manuali non usati.
  La presenza di una prima prenotazione non certifica la completezza dello storico:
  UI espone limite, date confrontate e campione. Non e una previsione di domanda.

Test: `dotnet run --project tests/EventPlanner.Checks`.
