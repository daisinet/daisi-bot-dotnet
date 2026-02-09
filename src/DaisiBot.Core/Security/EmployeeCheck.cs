namespace DaisiBot.Core.Security;

public static class EmployeeCheck
{
    public static bool IsEmployee(string email)
    {
        return !string.IsNullOrWhiteSpace(email) &&
               email.EndsWith("@daisi.ai", StringComparison.OrdinalIgnoreCase);
    }
}
