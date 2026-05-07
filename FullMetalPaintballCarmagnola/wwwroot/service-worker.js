self.addEventListener('install', function (event) {
    console.log('Service Worker installato');
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    console.log('Service Worker attivo');
});

self.addEventListener('fetch', function (event) {
    // Lasciamo passare tutte le richieste normalmente
});
