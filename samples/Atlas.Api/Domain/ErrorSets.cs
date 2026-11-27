namespace Atlas.Api.Domain;

// What each operation can fail with. A handler returning Result<T, TError> declares them, and
// AddResults() documents each one with its own body.
public union GetCountryError(CountryNotFound);

public union GetCityError(CityNotFound);

public union CreateCityError(CountryNotFound, CapitalWithoutPopulation, CityNameTaken, CapitalTaken);

public union DeleteCountryError(CountryNotFound, CountryHasCities);
