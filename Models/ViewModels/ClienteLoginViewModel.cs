using System.ComponentModel.DataAnnotations;

public class ClienteLoginViewModel
{
    [Required]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }

    public int? EmpresaId { get; set; }
}