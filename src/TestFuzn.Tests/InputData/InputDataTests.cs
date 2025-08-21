namespace FuznLabs.TestFuzn.Tests.InputData;

[FeatureTest]
public class InputDataTests : BaseFeatureTest
{
    public override string FeatureName => "";

    [ScenarioTest]
    public async Task Verify_Params_based_InputData()
    {
        var userExecuted = new Dictionary<string, bool>();
        userExecuted["user1"] = false;
        userExecuted["user2"] = false;
        userExecuted["user3"] = false;

        await Scenario()
            .InputData(
                new User("user1"),
                new User("user2"),
                new User("user3")
            )
            .InputDataBehavior(InputDataBehavior.LoopThenRepeatLast)
            .Step("Verify", context =>
            {
                var user = context.InputData<User>();
                userExecuted[user.Name] = true;
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(6))
            .Run();

        foreach (var user in userExecuted)
        {
            Assert.IsTrue(user.Value);
        }
    }

    [ScenarioTest]
    public async Task Verify_List_based_InputData()
    {
        var userExecuted = new Dictionary<string, bool>();
        userExecuted["user1"] = false;
        userExecuted["user2"] = false;
        userExecuted["user3"] = false;

        await Scenario()
            .InputDataFromList((context) =>
            {
                Assert.IsNotNull(context);
                return new List<object>()
                {
                    new User("user1"),
                    new User("user2"),
                    new User("user3")
                };
            })
            .Step("Verify", context =>
            {
                var user = context.InputData<User>();
                userExecuted[user.Name] = true;
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(6))
            .Run();

        foreach (var user in userExecuted)
        {
            Assert.IsTrue(user.Value);
        }
    }

    [ScenarioTest]
    public async Task Verify_List_based_InputData_Async()
    {
        var userExecuted = new Dictionary<string, bool>();
        userExecuted["user1"] = false;
        userExecuted["user2"] = false;
        userExecuted["user3"] = false;

        await Scenario()
            .InputDataFromList(async (context) =>
            {
                Assert.IsNotNull(context);
                var dataTable = new List<object>();
                dataTable.Add(new User("user1"));
                dataTable.Add(new User("user2"));
                dataTable.Add(new User("user3"));
                return await Task.FromResult(dataTable);
            })
            .Step("Verify", context =>
            {
                var user = context.InputData<User>();
                userExecuted[user.Name] = true;
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(6))
            .Run();

        foreach (var user in userExecuted)
        {
            Assert.IsTrue(user.Value);
        }
    }

    [ScenarioTest]
    public async Task Verify_Params_based_InputData_Feature()
    {
        var userExecuted = new Dictionary<string, bool>();
        userExecuted["user1"] = false;
        userExecuted["user2"] = false;
        userExecuted["user3"] = false;

        await Scenario()
            .InputData(
                new User("user1"),
                new User("user2"),
                new User("user3")
            )
            .InputDataBehavior(InputDataBehavior.LoopThenRepeatLast)
            .Step("Verify", context =>
            {
                var user = context.InputData<User>();
                userExecuted[user.Name] = true;
            })
            .Run();
    }
}
