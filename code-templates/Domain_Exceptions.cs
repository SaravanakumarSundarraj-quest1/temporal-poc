namespace Oms.Domain.Exceptions;

/// <summary>Thrown when an order state transition is invalid</summary>
public class InvalidOrderStateException : DomainException
{
    public InvalidOrderStateException(string message) : base(message) { }
}

/// <summary>Base class for all domain exceptions</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
