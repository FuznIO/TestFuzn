namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Environment;

[TestClass]
public class EnvironmentTests : Test
{

    [Test]
    public async Task Test2()
    {
        var executionCount = 0;

        await Scenario()
            .InputDataFromList(async (context) =>
            {
                return context.Info.TargetEnvironment switch
                {
                    "test" => await InputDataFileHelper.LoadFromCsv<User>($"users_{context.Info.TargetEnvironment}"),
                    _ => new List<object> { new User("userId_9"), new User("user_19") },
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
                if (context.Info.TargetEnvironment == "")
                    simulations.OneTimeLoad(10);
                else if (context.Info.TargetEnvironment == "test")
                    simulations.GradualLoadIncrease(10, 100, TimeSpan.FromSeconds(20));
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                if (context.Info.TargetEnvironment == "")
                    Assert.AreEqual(10, stats.RequestCount);
                else if (context.Info.TargetEnvironment == "test")
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
