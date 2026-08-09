using Oee.MarketAccess.Catalog;

namespace Oee.MarketAccess.Tests.Catalog;

internal sealed class IgnoreCaseInstrumentCatalog : IInstrumentCatalog
{
    private readonly Dictionary<string, SimpleInstrumentProfile> _profiles;

    public IgnoreCaseInstrumentCatalog(params SimpleInstrumentProfile[] profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles.ToDictionary(p => p.Symbol, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetBySymbol(string symbol, out SimpleInstrumentProfile profile)
    {
        return _profiles.TryGetValue(symbol, out profile);
    }

    public static IgnoreCaseInstrumentCatalog New_With_Tradeable_AAPL_And_Untradeable_NVDA()
    {
        var aapl = new SimpleInstrumentProfile(1, "AAPL", true, 0.01m, 100, 1000, 30000, 36000);
        var nvda = new SimpleInstrumentProfile(2, "NVDA", false, 0.01m, 100, 1000, 10000, 20000);

        return new IgnoreCaseInstrumentCatalog(aapl, nvda);
    }
}