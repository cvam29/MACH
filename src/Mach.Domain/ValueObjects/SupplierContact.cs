namespace Mach.Domain.ValueObjects;

/// <summary>A supplier's notification contact, resolved from a product SKU.</summary>
public sealed record SupplierContact(Guid Id, string Name, string Email);
