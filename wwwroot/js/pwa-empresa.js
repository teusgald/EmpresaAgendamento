// PWA do portal da empresa/funcionário: registra o service worker e cuida
// do botão "Instalar app" na barra lateral (escondido por padrão no HTML —
// só aparece se der pra instalar de algum jeito).
//
// É a mesma lógica de wwwroot/js/pwa-cliente.js, só que apontando pro
// "instalarAppLink" de Views/Shared/_Layout.cshtml. Duplicado em vez de
// compartilhado de propósito: os dois layouts (_Layout e _LayoutCliente)
// já duplicam esse mesmo padrão (toast, sidebar, etc.) em vez de extrair
// algo comum — manter os dois scripts espelhados e independentes evita
// arriscar o portal do Cliente numa mudança pensada pro portal da Empresa.
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
