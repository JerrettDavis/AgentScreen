self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(caches.open('agentdisplay-v1').then(cache => cache.addAll(self.assetsManifest.assets.filter(a => a.url.indexOf('.map') < 0).map(a => a.url)))));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;
  event.respondWith(fetch(event.request).catch(() => caches.match(event.request)));
});
