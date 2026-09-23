// PWA do portal do cliente: registra o service worker e cuida do botão
// "Instalar app" na barra lateral (escondido por padrão no HTML — só
// aparece se der pra instalar de algum jeito).
(function () {
    "use strict";

    if ("serviceWorker" in navigator) {
        window.addEventListener("load", function () {
            navigator.serviceWorker.register("/sw.js").catch(function () {
                // Sem service worker o app continua funcionando normal,
                // só sem os benefícios de instalação/offline.
            });
        });
    }

    var linkInstalar = document.getElementById("instalarAppLink");
    if (!linkInstalar) return;

    var jaInstalado =
        window.matchMedia("(display-mode: standalone)").matches ||
        window.navigator.standalone === true;

    if (jaInstalado) return;

    var eventoInstalacao = null;

    window.addEventListener("beforeinstallprompt", function (e) {
        e.preventDefault();
        eventoInstalacao = e;
        linkInstalar.hidden = false;
    });

    var ehIOS = /iphone|ipad|ipod/i.test(window.navigator.userAgent);
    var ehSafari = /^((?!chrome|android).)*safari/i.test(window.navigator.userAgent);

    if (ehIOS && ehSafari) {
        linkInstalar.hidden = false;
    }

    linkInstalar.addEventListener("click", function (e) {
        e.preventDefault();

        if (eventoInstalacao) {
            eventoInstalacao.prompt();
            eventoInstalacao.userChoice.finally(function () {
                eventoInstalacao = null;
                linkInstalar.hidden = true;
            });
            return;
        }

        if (ehIOS) {
            if (typeof showToast === "function") {
                showToast(
                    "Toque no ícone de compartilhar do navegador e escolha \"Adicionar à Tela de Início\".",
                    "warning"
                );
            } else {
                alert("Toque no ícone de compartilhar do navegador e escolha \"Adicionar à Tela de Início\".");
            }
        }
    });
})();
