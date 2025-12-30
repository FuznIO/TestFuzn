namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Environment;

[TestClass]
public class EnvironmentTests : TestBase
{

    [Test]
    public async Task Test2()
    {
        var executionCount = 0;

        await Scenario()
            .InputDataFromList(async (context) =>
            {
                return context.Info.EnvironmentName switch
                {
                    "" => new List<object> { new User("userId_9"), new User("user_19") },
                    "test" => await InputDataFileHelper.LoadFromCsv<User>($"users_{context.Info.EnvironmentName}"),
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
                if (context.Info.EnvironmentName == "")
                    simulations.OneTimeLoad(10);
                else if (context.Info.EnvironmentName == "test")
                    simulations.GradualLoadIncrease(10, 100, TimeSpan.FromSeconds(20));
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                if (context.Info.EnvironmentName == "")
                    Assert.AreEqual(10, stats.RequestCount);
                else if (context.Info.EnvironmentName == "test")
                    Assert.AreEqual(20, stats.RequestCount);
                //else
                //    Assert.Fail();
            })
            .Run();
    }

    public class User(string userId)
    {
        public string UserId { get; set; } = userId;
    }
}
