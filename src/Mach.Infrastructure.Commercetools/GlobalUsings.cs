// Disambiguate types whose simple names collide between the commercetools SDK and the domain model.
// Domain Money/Address win the unqualified name; the SDK equivalents get explicit aliases.
global using Money = Mach.Domain.ValueObjects.Money;
global using Address = Mach.Domain.ValueObjects.Address;
global using CtMoney = commercetools.Sdk.Api.Models.Common.Money;
global using CtBaseAddress = commercetools.Sdk.Api.Models.Common.BaseAddress;
