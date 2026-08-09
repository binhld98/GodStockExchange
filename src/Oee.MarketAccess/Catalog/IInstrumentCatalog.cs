namespace Oee.MarketAccess.Catalog;

public interface IInstrumentCatalog
{
    bool TryGetBySymbol(string symbol, out SimpleInstrumentProfile profile);
}