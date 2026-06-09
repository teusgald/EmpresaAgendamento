using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace EmpresaAgendamento.Helpers
{
    public static class ToastHelper
    {
        public static void Success(ITempDataDictionary tempData, string mensagem)
        {
            tempData["ToastMessage"] = mensagem;
            tempData["ToastType"] = "success";
        }

        public static void Warning(ITempDataDictionary tempData, string mensagem)
        {
            tempData["ToastMessage"] = mensagem;
            tempData["ToastType"] = "warning";
        }

        public static void Error(ITempDataDictionary tempData, string mensagem)
        {
            tempData["ToastMessage"] = mensagem;
            tempData["ToastType"] = "error";
        }

        public static void Info(ITempDataDictionary tempData, string mensagem)
        {
            tempData["ToastMessage"] = mensagem;
            tempData["ToastType"] = "info";
        }
    }
}