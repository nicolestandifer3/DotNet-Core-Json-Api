using JetBrains.Annotations;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreTests.IntegrationTests.CompositeKeys;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
[Resource(ControllerNamespace = "JsonApiDotNetCoreTests.IntegrationTests.CompositeKeys")]
public sealed class Dealership : Identifiable<int>
{
    [Attr]
    public string Address { get; set; } = null!;

    [HasMany]
    public ISet<Car> Inventory { get; set; } = new HashSet<Car>();

    [HasMany]
    public ISet<Car> SoldCars { get; set; } = new HashSet<Car>();
}
