using System.Collections.Concurrent;

namespace TestFusion.Tests.InputData;

[FeatureTest]
public class InputDataFileLoadTests : BaseFeatureTest
{
    public override string FeatureName => "";

    [ScenarioTest]
    public async Task Verify_Csv_InputData()
    {
        var dict = new ConcurrentDictionary<string, int>();
        await Scenario()
            .InputDataFromList(async context =>
            {
                Assert.IsNotNull(context);
                return await InputDataFileHelper.LoadFromCsv<SimpleUser>("InputData/users.csv");
            })
            .InputDataBehavior(InputDataBehavior.Loop)
            .Step("Verify", async context =>
            {
                var user = context.InputData<SimpleUser>();
                dict.AddOrUpdate(user.Name,
                    s => 1,
                    (s, i) => i + 1);
                await Task.Delay(35);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                foreach (var e in dict)
                    Assert.AreEqual(3, e.Value);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(9))
            .Run();
    }

    [ScenarioTest]
    public async Task Verify_Json_InputData()
    {
        var dict = new ConcurrentDictionary<string, int>();
        await Scenario()
            .InputDataFromList(async context =>
            {
                Assert.IsNotNull(context);
                return await InputDataFileHelper.LoadFromJson<ComplexUser>("InputData/users.json");
            })
            .Step("Verify", async context =>
            {
                var user = context.InputData<ComplexUser>();
                dict.AddOrUpdate(user.Name,
                    s => 1,
                    (s, i) => i + 1);
                await Task.Delay(55);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                foreach (var e in dict)
                    Assert.AreEqual(3, e.Value);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(9))
            .Run();
    }
}
