using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.InputData;

[TestClass]
public class InputDataBehaviorTests : Test
{
    [Test]
    public async Task Verify_Loop()
    {
        var userExecuted = new Dictionary<string, User>();
        userExecuted["user1"] = new User("user1");
        userExecuted["user2"] = new User("user2");
        userExecuted["user3"] = new User("user3");

        await Scenario()
            .InputDataFromList((context) =>
            {
                var inputData = new List<object>();
                foreach (var user in userExecuted)
                    inputData.Add(user.Value.Name);
                return inputData;
            }
            )
            .InputDataBehavior(InputDataBehavior.Loop)
            .Step("Verify", context =>
            {
                var userName = context.InputData<string>();
                var user = userExecuted[userName];
                Interlocked.Increment(ref user.Counter);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(8, stats.Ok.RequestCount);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(8))
            .Run();

        Assert.AreEqual(3, userExecuted["user1"].Counter);
        Assert.AreEqual(3, userExecuted["user2"].Counter);
        Assert.AreEqual(2, userExecuted["user3"].Counter);
    }

    [Test]
    public async Task Verify_LoopThenRepeatLast()
    {
        var userExecuted = new Dictionary<string, User>();
        userExecuted["user1"] = new User("user1");
        userExecuted["user2"] = new User("user2");
        userExecuted["user3"] = new User("user3");

        await Scenario()
            .InputDataFromList((context) =>
            {
                var inputData = new List<object>();
                foreach (var user in userExecuted)
                    inputData.Add(user.Value.Name);
                return inputData;
            })
            .InputDataBehavior(InputDataBehavior.LoopThenRepeatLast)
            .Step("Verify", context =>
            {
                var userName = context.InputData<string>();
                var user = userExecuted[userName];
                Interlocked.Increment(ref user.Counter);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(100, stats.Ok.RequestCount);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(100))
            .Run();

        Assert.AreEqual(1, userExecuted["user1"].Counter);
        Assert.AreEqual(1, userExecuted["user2"].Counter);
        Assert.AreEqual(98, userExecuted["user3"].Counter);
    }

    [Test]
    public async Task Verify_Random()
    {
        var userExecuted = new Dictionary<string, User>();
        userExecuted["user1"] = new User("user1");
        userExecuted["user2"] = new User("user2");
        userExecuted["user3"] = new User("user3");

        await Scenario()
            .InputDataFromList((context) =>
            {
                var inputData = new List<object>();
                foreach (var user in userExecuted)
                    inputData.Add(user.Value.Name);
                return inputData;
            }
            )
            .InputDataBehavior(InputDataBehavior.Random)
            .Step("Verify", context =>
            {
                var userName = context.InputData<string>();
                var user = userExecuted[userName];
                Interlocked.Increment(ref user.Counter);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(30, stats.Ok.RequestCount);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(30))
            .Run();

        Assert.IsGreaterThan(1, userExecuted["user1"].Counter);
        Assert.IsGreaterThan(1, userExecuted["user2"].Counter);
        Assert.IsGreaterThan(1, userExecuted["user3"].Counter);
    }

 [Test]
    public async Task Verify_LoopThenRandom()
    {
        var userExecuted = new Dictionary<string, User>();
        userExecuted["user1"] = new User("user1");
        userExecuted["user2"] = new User("user2");
        userExecuted["user3"] = new User("user3");

        await Scenario()
            .InputDataFromList((context) =>
            {
                var inputData = new List<object>();
                foreach (var user in userExecuted)
                    inputData.Add(user.Value.Name);
                return inputData;
            }
            )
            .InputDataBehavior(InputDataBehavior.LoopThenRandom)
            .Step("Verify", context =>
            {
                var userName = context.InputData<string>();
                var user = userExecuted[userName];
                Interlocked.Increment(ref user.Counter);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(30, stats.Ok.RequestCount);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(30))
            .Run();

        Assert.IsGreaterThan(1, userExecuted["user1"].Counter);
        Assert.IsGreaterThan(1, userExecuted["user2"].Counter);
        Assert.IsGreaterThan(1, userExecuted["user3"].Counter);
    }
}
