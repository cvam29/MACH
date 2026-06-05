using Mach.Domain.ValueObjects;

namespace Mach.Application.Dtos;

/// <summary>A customer profile as returned by commercetools.</summary>
public sealed record CustomerDto(
    CustomerId Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<Address> Addresses);
