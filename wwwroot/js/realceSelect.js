// Troca a aparência nativa de <select class="form-select"> (ou .form-control)
// por um botão + painel estilizados, com campo de busca quando o select tem
// mais de LIMITE_PARA_BUSCA opções — pedido explícito: "dropdown sem efeito
// nenhum" incomodava, e um <select> com muita opção (UF, categoria...) é
// difícil de usar sem busca.
//
// O <select> original continua no DOM (só escondido) e continua sendo a
// fonte de verdade: troca de opção aqui dispara um evento "change" nele, então
// qualquer addEventListener/onchange que já existia na página continua
// funcionando sem precisar saber que isso existe. Formulário comum (GET/POST)
// também continua funcionando igual, porque o <select> escondido ainda entra
// no submit.
(function () {
    "use strict";

    var LIMITE_PARA_BUSCA = 10;

    function realcar(select) {
        if (select.dataset.realceAplicado) return;
        if (select.multiple) return;
        if (select.closest("[data-sem-realce]")) return;

        var opcoes = Array.prototype.slice.call(select.options);
        if (opcoes.length === 0) return;

        select.dataset.realceAplicado = "1";
        select.classList.add("realce-select-original");

        var wrap = document.createElement("div");
        wrap.className = "realce-select-wrap";

        var btn = document.createElement("button");
        btn.type = "button";
        btn.className = "realce-select-btn";
        btn.disabled = select.disabled;

        var btnLabel = document.createElement("span");
        btnLabel.className = "realce-select-label";

        var btnIcone = document.createElement("i");
        btnIcone.className = "bi bi-chevron-down realce-select-seta";

        btn.appendChild(btnLabel);
        btn.appendChild(btnIcone);

        var painel = document.createElement("div");
        painel.className = "realce-select-painel";

        var temBusca = opcoes.length > LIMITE_PARA_BUSCA;
        var campoBusca = null;

        if (temBusca) {
            campoBusca = document.createElement("input");
            campoBusca.type = "text";
            campoBusca.className = "realce-select-busca";
            campoBusca.placeholder = "Pesquisar...";
            campoBusca.setAttribute("autocomplete", "off");
            painel.appendChild(campoBusca);
        }

        var lista = document.createElement("div");
        lista.className = "realce-select-lista";
        painel.appendChild(lista);

        function textoOpcao(opcao) {
            return (opcao.text || "").trim();
        }

        function atualizarLabel() {
            var opcaoAtual = select.options[select.selectedIndex];
            var texto = opcaoAtual ? textoOpcao(opcaoAtual) : "";
            btnLabel.textContent = texto;
            wrap.classList.toggle("realce-select-vazio-selecionado", texto === "");
        }

        function renderLista(filtro) {
            lista.innerHTML = "";
            var termo = (filtro || "").trim().toLowerCase();
            var nenhumVisivel = true;

            opcoes.forEach(function (opcao, indice) {
                if (termo && textoOpcao(opcao).toLowerCase().indexOf(termo) === -1) return;

                nenhumVisivel = false;

                var item = document.createElement("div");
                item.className = "realce-select-item" + (opcao.selected ? " selecionado" : "");
                item.textContent = textoOpcao(opcao) || "—";
                item.addEventListener("click", function () {
                    if (select.selectedIndex !== indice) {
                        select.selectedIndex = indice;
                        select.dispatchEvent(new Event("change", { bubbles: true }));
                    }
                    atualizarLabel();
                    fechar();
                });

                lista.appendChild(item);
            });

            if (nenhumVisivel) {
                var vazio = document.createElement("div");
                vazio.className = "realce-select-item-vazio";
                vazio.textContent = "Nada encontrado";
                lista.appendChild(vazio);
            }
        }

        function abrir() {
            document.querySelectorAll(".realce-select-wrap.aberto").forEach(function (outro) {
                if (outro !== wrap) outro.classList.remove("aberto");
            });

            wrap.classList.add("aberto");
            renderLista("");

            if (campoBusca) {
                campoBusca.value = "";
                setTimeout(function () { campoBusca.focus(); }, 0);
            }
        }

        function fechar() {
            wrap.classList.remove("aberto");
        }

        btn.addEventListener("click", function () {
            if (btn.disabled) return;
            if (wrap.classList.contains("aberto")) fechar();
            else abrir();
        });

        if (campoBusca) {
            campoBusca.addEventListener("input", function () { renderLista(campoBusca.value); });
            campoBusca.addEventListener("click", function (e) { e.stopPropagation(); });
            campoBusca.addEventListener("keydown", function (e) {
                if (e.key === "Escape") { fechar(); btn.focus(); }
            });
        }

        document.addEventListener("click", function (e) {
            if (!wrap.contains(e.target)) fechar();
        });

        select.addEventListener("change", atualizarLabel);

        wrap.appendChild(btn);
        wrap.appendChild(painel);
        select.insertAdjacentElement("afterend", wrap);

        atualizarLabel();
    }

    function realcarTodos(escopo) {
        var raiz = escopo || document;
        raiz.querySelectorAll("select.form-select, select.form-control").forEach(realcar);
    }

    document.addEventListener("DOMContentLoaded", function () { realcarTodos(document); });

    // Exposto pra reaplicar depois de conteúdo trocado via JS (ex.: modal que
    // recarrega um <select> por fetch) — chamar window.realceSelect(container).
    window.realceSelect = realcarTodos;
})();
