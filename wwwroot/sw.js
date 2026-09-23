// Service worker compartilhado pelos portais do Cliente e da Empresa/
// Funcionário (cada um com seu próprio manifest, registrando o mesmo
// /sw.js). Estratégia deliberadamente conservadora: só assets estáticos
// (css/js/fonte/ícone) são cacheados — NUNCA o HTML de uma página
// navegada. Isso evita o risco de alguém abrir o app e ver dados em cache
// de outra sessão/pessoa no mesmo aparelho. Navegação sempre busca rede
// primeiro; só cai pro offline.html (que não tem dado nenhum de usuário)
// se a rede falhar de verdade.

const CACHE_NAME = "simplitime-static-v3";

const ASSETS_ESSENCIAIS = [
    "/offline.html",
    "/manifest.json",
    "/manifest-empresa.json",
    "/img/icon-192.png",
    "/img/icon-512.png",
    "/css/site.css",
    "/lib/bootstrap/dist/css/bootstrap.min.css",
    "/lib/bootstrap/dist/js/bootstrap.bundle.min.js"
];

self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => cache.addAll(ASSETS_ESSENCIAIS))
            .catch(() => {
                // Um asset faltando não deve impedir a instalação do worker.
            })
    );
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys().then((nomes) =>
            Promise.all(
                nomes
                    .filter((nome) => nome !== CACHE_NAME)
                    .map((nome) => caches.delete(nome))
            )
        )
    );
    self.clients.claim();
});

function ehAssetEstatico(url) {
    return /\.(css|js|png|jpg|jpeg|svg|webp|gif|ico|woff2?|ttf)$/i.test(url.pathname);
}

self.addEventListener("fetch", (event) => {
    const { request } = event;

    if (request.method !== "GET") return;

    const url = new URL(request.url);

    // Só same-origin — nunca intercepta chamada pra CDN/fonte externa.
    if (url.origin !== self.location.origin) return;

    // Navegação (a própria página) — sempre tenta rede; nunca serve HTML do
    // cache (dado por sessão), só o offline.html genérico se a rede cair.
    if (request.mode === "navigate") {
        event.respondWith(
            fetch(request).catch(() => caches.match("/offline.html"))
        );
        return;
    }

    // Asset estático — cache-first, atualiza o cache em segundo plano.
    if (ehAssetEstatico(url)) {
        event.respondWith(
            caches.match(request).then((cached) => {
                const buscaRede = fetch(request)
                    .then((resposta) => {
                        if (resposta.ok) {
                            caches.open(CACHE_NAME).then((cache) => cache.put(request, resposta.clone()));
                        }
                        return resposta;
                    })
                    .catch(() => cached);

                return cached || buscaRede;
            })
        );
    }
});
