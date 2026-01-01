namespace Fuzn.TestFuzn.Tests.ExecutionType.Standard.InputData;

[TestClass]
public class InputDataTests :  Test
{
    [Test]
    public async Task Feature_Verify_List_based_InputData_Sync()
    {
        var userExecuted = new Dictionary<string, bool>();
        userExecuted["user1"] = false;
        userExecuted["user2"] = false;
        userExecuted["user3"] = false;

        await Scenario()
            .InputDataFromList((context) =>
            {
                Assert.IsNotNull(context);
                var inputData = new List<object>();
                inputData.Add(new User("user1"));
                inputData.Add(new User("user2"));
                inputData.Add(new User("user3"));
                return inputData;
            })
            .Step("Verify", context =>
            {
                userExecuted[context.InputData<User>().Name] = true;
            })
            .Run();

        foreach (var user in userExecuted)
        {
            Assert.IsTrue(user.Value, $"User {user.Key} was not executed");
        }
    }

    [Test]
    public async Task Feature_Verify_List_based_InputData_Async()
    {
        var userExecuted = new Dictionary<string, bool>();
        userExecuted["user1"] = false;
        userExecuted["user2"] = false;
        userExecuted["user3"] = false;

        await Scenario()
            .InputDataFromList(async (context) =>
            {
                Assert.IsNotNull(context);
                var testCases = new List<object>();
                testCases.Add(new User("user1"));
                testCases.Add(new User("user2"));
                testCases.Add(new User("user3"));

                return await Task.FromResult(testCases);
            })
            .Step("Verify", context =>
            {
                userExecuted[context.InputData<User>().Name] = true;
            })
            .Run();

        foreach (var user in userExecuted)
        {
            Assert.IsTrue(user.Value, $"User {user.Key} was not executed");
        }
    }

    [Test]
    public async Task Feature_Verify_Params_based_InputData()
    {
        var userExecuted = new Dictionary<string, bool>();
        userExecuted["user1"] = false;
        userExecuted["user2"] = false;
        userExecuted["user3"] = false;

        await Scenario()
            .InputData(new User("user1"), new User("user2"), new User("user3"))
            .Step("Verify", context =>
            {
                userExecuted[context.InputData<User>().Name] = true;
            })
            .Run();

        foreach (var user in userExecuted)
        {
            Assert.IsTrue(user.Value, $"User {user.Key} was not executed");
        }
    }

    [Test]
    public async Task Feature_Verify_String_Params_based_InputData()
    {
        var executed = new Dictionary<string, bool>();
        executed["user1"] = false;
        executed["user2"] = false;
        executed["user3"] = false;

        await Scenario()
            .InputData("user1", "user2", "user3")
            .Step("Verify", context =>
            {
                executed[context.InputData<string>()] = true;
            })
            .Run();

        foreach (var user in executed)
        {
            Assert.IsTrue(user.Value, $"User {user.Key} was not executed");
        }
    }

    [Test]
    public async Task Feature_Verify_Int_Params_based_InputData()
    {
        var executed = new Dictionary<string, bool>();
        executed["1"] = false;
        executed["2"] = false;
        executed["3"] = false;

        await Scenario()
            .InputData(1, 2, 3)
            .Step("Verify", context =>
            {
                executed[context.InputData<int>().ToString()] = true;
            })
            .Run();

        foreach (var user in executed)
        {
            Assert.IsTrue(user.Value, $"User {user.Key} was not executed");
        }
    }

    [Test]
    public async Task Should_Fail_Feature_Verify_Scenario_Fails_When_InputData_Iteration_Fails()
    {
        var catchWasCalled = false;

        try
        {
            await Scenario()
                 .InputData("Test1", "Test2")
                 .Step("Step should fail", (context) =>
                 {
                     Assert.Fail("This step should fail");
                 })
                 .AssertInternalState((state) =>
                 {
                     Assert.AreEqual(TestStatus.Failed, state.SharedExecutionState.ScenarioResultState.StandardCollectors.First().Value.Status);
                 })
                 .Run();
        }
        catch (AssertFailedException)
        {
            catchWasCalled = true;
        }

        Assert.IsTrue(catchWasCalled);
    }
}
