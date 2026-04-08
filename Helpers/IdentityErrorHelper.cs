using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace EmpresaAgendamento.Helpers
{
    public static class IdentityErrorHelper
    {
        /// <summary>
        /// Converte os erros do Identity em mensagens amigáveis
        /// </summary>
        public static IEnumerable<string> GetFriendlyErrors(IEnumerable<IdentityError> errors)
        {
            foreach (var error in errors)
            {
                string message = error.Description;

                if (error.Code.Contains("Password"))
                {
                    message = "A senha deve ter: mínimo 8 caracteres, " +
                              "1 letra maiúscula, 1 letra minúscula, " +
                              "1 número e 1 caractere especial.";
                }
                else if (error.Code.Contains("DuplicateUserName"))
                {
                    message = "O email informado já está em uso.";
                }
                else if (error.Code.Contains("InvalidEmail"))
                {
                    message = "O email informado não é válido.";
                }

                yield return message;
            }
        }
    }
}