namespace Api.Exceptions;

public class OrganizationNotPayoutReadyException : Exception
{
    public OrganizationNotPayoutReadyException(string message) : base(message) { }
}
