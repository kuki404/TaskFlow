namespace TaskFlow.Domain.Exceptions;

/// <summary>Thrown when an operation would violate a domain invariant (e.g. moving a card to a negative position).</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
