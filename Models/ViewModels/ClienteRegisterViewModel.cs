using System.ComponentModel.DataAnnotations;

public class ClienteRegisterViewModel
{
    [Required]
    public string Nome { get; set; }

    public string? Telefone { get; set; }

    [Required]
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}