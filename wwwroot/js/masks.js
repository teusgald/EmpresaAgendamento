// Máscara de dinheiro (R$) — aplicada a qualquer <input data-mask="dinheiro">.
//
// Como funciona: o input com data-mask vira um campo só de EXIBIÇÃO (sem
// "name" — não é enviado no POST) e o script cria, logo depois dele, um
// <input type="hidden"> com o "name"/valor de verdade, sempre em formato
// decimal simples com ponto ("1234.56") — o mesmo formato que os campos
// type="number" já mandavam pro servidor antes, então o model binding do
// ASP.NET Core continua funcionando exatamente igual, sem depender da
// cultura/locale configurada no servidor.
//
// Data mask: não existe — todo campo de data do sistema já usa
// <input type="date"> (calendário nativo do navegador), que não precisa e
// não aceita máscara de texto digitado.
(function () {
    "use strict";

    function apenasDigitos(valor) {
        return (valor || "").replace(/\D/g, "");
    }

    function formatarExibicao(centavos) {
        var numero = (parseInt(centavos || "0", 10) / 100).toFixed(2);
        var partes = numero.split(".");
        var inteiro = partes[0].replace(/\B(?=(\d{3})+(?!\d))/g, ".");
        return "R$ " + inteiro + "," + partes[1];
    }

    // Aceita tanto o que já vinha do servidor ("39.9", "39.90") quanto um
    // valor já em formato brasileiro ("39,90" ou "1.234,56"), pra
    // inicializar corretamente tanto em telas de Create (vazio) quanto de
    // Edit (valor existente vindo do banco).
    function centavosDeTexto(texto) {
        if (texto == null || texto === "") return "";

        var limpo = String(texto).trim();
        var temVirgula = limpo.indexOf(",") !== -1;

        if (temVirgula) {
            limpo = limpo.replace(/\./g, "").replace(",", ".");
        }

        var numero = parseFloat(limpo);
        if (isNaN(numero)) return "";

        return Math.round(numero * 100).toString();
    }

    function iniciar(visivel) {
        if (visivel.dataset.maskAplicada === "1") return;
        visivel.dataset.maskAplicada = "1";

        var nomeReal = visivel.getAttribute("name");
        var eraObrigatorio = visivel.hasAttribute("required");

        var hidden = document.createElement("input");
        hidden.type = "hidden";
        hidden.name = nomeReal;
        visivel.insertAdjacentElement("afterend", hidden);

        visivel.removeAttribute("name");
        visivel.removeAttribute("type");
        visivel.setAttribute("type", "text");
        visivel.setAttribute("inputmode", "decimal");
        visivel.removeAttribute("step");
        visivel.removeAttribute("min");
        visivel.removeAttribute("max");
        visivel.removeAttribute("required");

        if (eraObrigatorio) {
            // HTML5 nativo (funciona mesmo sem jQuery validate na tela) —
            // valida o campo visível; o hidden é só quem carrega o valor.
            visivel.setAttribute("required", "required");
        }

        function aplicar(centavos) {
            if (!centavos || centavos === "0") {
                if (!centavos) {
                    visivel.value = "";
                    hidden.value = "";
                    return;
                }
            }

            visivel.value = formatarExibicao(centavos);
            hidden.value = (parseInt(centavos, 10) / 100).toFixed(2);
        }

        aplicar(centavosDeTexto(visivel.value));

        visivel.addEventListener("input", function () {
            aplicar(apenasDigitos(visivel.value));
        });

        // Telas que preenchem o campo via JS (ex.: botão "usar valor total
        // pendente") setam .value direto — não passam pelo evento "input".
        // Reaplica a máscara nesse caso.
        visivel.addEventListener("change", function () {
            aplicar(apenasDigitos(visivel.value));
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll('[data-mask="dinheiro"]').forEach(iniciar);
    });

    // Exposto pra telas que adicionam campo de valor dinamicamente (ex.:
    // linha nova numa lista) sem precisar duplicar a lógica.
    window.MascaraDinheiro = { iniciar: iniciar };
})();
