using System.Collections.Concurrent;
using System.Globalization;

namespace Atlas.Api.Domain;

/// <summary>The sample's data, in memory, seeded with a few countries and cities.</summary>
public sealed class AtlasStore
{
    private readonly ConcurrentDictionary<CountryId, CountryDto> _countries = new();

    private readonly ConcurrentDictionary<CityId, CityDto> _cities = new();

    private readonly ConcurrentQueue<SuggestCityRequest> _suggestions = new();

    private int _lastCityId;

    public AtlasStore()
    {
        // Fixed ids, so Atlas.Api.http can name them.
        CountryDto ukraine = Add(new CountryDto(Seed(1), "UA", "Ukraine", Continent.Europe));
        CountryDto japan = Add(new CountryDto(Seed(2), "JP", "Japan", Continent.Asia));
        CountryDto chile = Add(new CountryDto(Seed(3), "CL", "Chile", Continent.SouthAmerica));

        AddCity(ukraine.Id, "Kyiv", 2_952_301, isCapital: true);
        AddCity(ukraine.Id, "Lviv", 717_273, isCapital: false);
        AddCity(japan.Id, "Tokyo", 14_094_034, isCapital: true);
        AddCity(japan.Id, "Kyoto", 1_463_723, isCapital: false);
        AddCity(chile.Id, "Santiago", 6_310_000, isCapital: true);
        AddCity(chile.Id, "Valparaíso", null, isCapital: false);
    }

    public IReadOnlyList<CountryDto> Countries =>
        [.. _countries.Values.OrderBy(country => country.Name, StringComparer.Ordinal)];

    public IReadOnlyList<CityDto> Cities =>
        [.. _cities.Values.OrderBy(city => city.Id.Value)];

    public int PendingSuggestions => _suggestions.Count;

    public CountryDto? FindCountry(CountryId id) => _countries.GetValueOrDefault(id);

    public CityDto? FindCity(CityId id) => _cities.GetValueOrDefault(id);

    public CountryDto AddCountry(string code, string name, Continent continent) =>
        Add(new CountryDto(CountryId.New(), code, name, continent));

    public CityDto AddCity(CountryId countryId, string name, int? population, bool isCapital)
    {
        CityDto city = new(
            new CityId(Interlocked.Increment(ref _lastCityId)),
            countryId,
            name,
            population,
            isCapital);
        _cities[city.Id] = city;
        return city;
    }

    public void Suggest(SuggestCityRequest suggestion) => _suggestions.Enqueue(suggestion);

    private static CountryId Seed(int number) =>
        new(Guid.Parse($"00000000-0000-0000-0000-{number:D12}", CultureInfo.InvariantCulture));

    private CountryDto Add(CountryDto country)
    {
        _countries[country.Id] = country;
        return country;
    }
}
