using System.Globalization;
using Xunit;

[CollectionDefinition("InvariantCultureCollection")]
public class InvariantCultureCollection : ICollectionFixture<InvariantCultureFixture> { }

public class InvariantCultureFixture
{
    private readonly CultureInfo _savedCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _savedUICulture = CultureInfo.CurrentUICulture;

    public InvariantCultureFixture()
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
    }

    ~InvariantCultureFixture()
    {
        CultureInfo.DefaultThreadCurrentCulture = _savedCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _savedUICulture;
    }
}