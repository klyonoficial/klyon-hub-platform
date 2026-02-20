namespace KlyonHubPlatform.Api.Dtos.Admin
{
    /// <summary>
    /// DTO para formulário de edição de usuário (admin). Usado com AutoMapper para mapear sobre User.
    /// </summary>
    public class EditUserFormDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsActive { get; set; }
    }
}
