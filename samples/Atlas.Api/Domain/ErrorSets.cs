namespace Atlas.Api.Domain;

// What each operation can fail with. A handler returning Result<T, TError> declares them, and
// AddResults() documents each one with its own body.
#pragma warning disable CA1815 // A union has no equality members of its own.
public union GetCountryError(CountryNotFound);

public union GetCityError(CityNotFound);

public union CreateCityError(CountryNotFound, CityNameTaken, CapitalTaken);

public union DeleteCountryError(CountryNotFound, CountryHasCities);
#pragma warning restore CA1815
