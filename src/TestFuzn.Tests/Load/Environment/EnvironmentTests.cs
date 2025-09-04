namespace Fuzn.TestFuzn.Tests.Load.Environment;

[FeatureTest]
public class EnvironmentTests : BaseFeatureTest
{

    [ScenarioTest]
    public async Task Test2()
    {
        var executionCount = 0;

        await Scenario()
            .InputDataFromList(async (context) =>
            {
                return context.Info.EnvironmentName switch
                {
                    "development" => new List<object> { new User("userId_9"), new User("user_19") },
                    "qa" => await InputDataFileHelper.LoadFromCsv<User>($"users_{context.Info.EnvironmentName}"),
                    _ => throw new NotImplementedException()
                };
            })
            .Step("Step 1", async (context) =>
            {
                Interlocked.Increment(ref executionCount);
                var currentUser = context.InputData<User>();

                // Do something with currentUser.UserId
            })
            .Load().Simulations((context, simulations) =>
            {
                if (context.Info.EnvironmentName == "development")
                    simulations.OneTimeLoad(10);
                else if (context.Info.EnvironmentName == "test")
                    simulations.GradualLoadIncrease(10, 100, TimeSpan.FromSeconds(20));
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                if (context.Info.EnvironmentName == "development")
                    Assert.AreEqual(10, stats.RequestCount);
                else if (context.Info.EnvironmentName == "test")
                    Assert.AreEqual(20, stats.RequestCount);
                else
                    Assert.Fail();
            })
            .Run();
    }

    public class User(string userId)
    {
        public string UserId { get; set; } = userId;
    }
}
