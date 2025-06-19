using Microsoft.Extensions.Logging;

namespace TestFusion.Tests;

[FeatureTest]
public class SyntaxTests : BaseFeatureTest
{
    public override string FeatureName => "TestFusion Syntax";

    public override Task InitScenarioTest(Context context)
    {
        return Task.CompletedTask;
    }

    public override Task CleanupScenarioTest(Context context)
    {
        return Task.CompletedTask;
    }

    [Ignore]
    [ScenarioTest]
    public async Task DefaultContext()
    {
        await Scenario("Default Context Showcase")
            .Init((context) =>
            {
                // Initialization code goes here.
                // This will be executed before any steps.
            })
            .Init(async (context) =>
            {
            })
            .InputData("user1", "user2", "user3")
            .InputDataFromList((context) =>
            {
                var inputData = new List<object>()
                {
                    "user1",
                    "user2",
                    "user3"
                };
                return inputData;
            })
            .InputDataFromList(async (context) =>
            {
                // Override with context-object.
                var inputData = new List<object>();
                // Some async code goes here, read from database / api etc.
                return await Task.FromResult(inputData);
            })
            .Step("Step 1 - Sync with context", context =>
            {
                // Get current input data.
                var user = context.InputData<string>();
                // Set data in context which is shared between steps.
                context.SetSharedData("item1", "value1"); 
                // Get data.
                var value1 = context.GetSharedData<string>("item1");

                context.Logger.LogInformation($"User: {user}"); // Log information.

                // Some code goes here.
            })
            .Step("Step 2 - Async with context", async (context) =>
            {
                // Some code goes here.
                // Attach file to the current step.
                await context.Step.Attach("file1.txt", "Some content");
                await context.Step.Attach($"screenshot.png", new byte[0]);
                await context.Step.Attach($"screenshot.png", new MemoryStream());

                await Task.CompletedTask;
            })
            .Step("Step 3 - Shared step", SharedStep)
            .CleanupAfterEachIteration((context) =>
            {
            })
            .CleanupAfterEachIteration(async (context) =>
            {
            })
            .CleanupAfterScenario((context) =>
            {
            })
            .CleanupAfterScenario(async (context) =>
            {
            })
            .Run();
    }

    [Ignore]
    [ScenarioTest]
    public async Task DefaultContext_Load()
    {
        await Scenario("Default Context Showcase")
            .Init((context) =>
            {
            })
            .Init(async (context) =>
            {
            })
            .InputData("user1", "user2", "user3")
            .InputDataFromList((context) =>
            {
                var dataTable = new List<object>()
                {
                    "user1",
                    "user2",
                    "user3"
                };
                return dataTable;
            })
            .InputDataFromList(async (context) =>
            {
                // Override with context-object.
                var inputData = new List<object>();
                // Some async code goes here, read from database / api etc.
                return await Task.FromResult(inputData);
            })
            .Step("Step 1 - Sync with context", context =>
            {
                // Get current row.
                var user = context.InputData<string>();

                // Set data in context which is shared between steps.
                context.SetSharedData("item1", "value1"); 
                // Get data.
                var value1 = context.GetSharedData<string>("item1");

                context.Logger.LogInformation($"User: {user}"); // Log information.

                // Some code goes here.
            })
            .Pause(TimeSpan.FromSeconds(2)) // Pause for 2 seconds.
            .Step("Step 2 - Async with context", async (context) =>
            {
                // Some code goes here.
                await Task.CompletedTask;
            })
            .Step("Step 3 - Shared step", SharedStep)
            .Load().GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5))
            .Load().FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100))
            .Load().OneTimeLoad(100)
            .Load().Pause(TimeSpan.FromSeconds(5))
            .Load().FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100))
            .Load().RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100))
            .Load().AssertWhileRunning(stats =>
            {
                Assert.IsTrue(stats.RequestCount < 1000);
                Assert.AreEqual(0, stats.Ok.RequestCount);
                Assert.AreEqual(0, stats.Failed.RequestCount);
                Assert.IsTrue(stats.Ok.ResponseTimeMean > TimeSpan.FromSeconds(0.1));
                Assert.IsTrue(stats.Failed.ResponseTimeMean > TimeSpan.FromSeconds(0.1));
                Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.8));
                Assert.IsTrue(stats.Failed.ResponseTimeMean < TimeSpan.FromSeconds(0.8));
            })
            .Load().AssertWhenDone(stats =>
            {
                Assert.IsTrue(stats.RequestCount == 100);
                Assert.IsTrue(stats.Ok.RequestCount == 100);
                Assert.IsTrue(stats.Failed.RequestCount == 0);
                Assert.IsTrue(stats.Ok.ResponseTimeStandardDeviation < TimeSpan.FromSeconds(0.5));
                Assert.IsTrue(stats.Ok.ResponseTimeMean > TimeSpan.FromSeconds(0.5));
                Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.8));
            })
            .CleanupAfterEachIteration((context) =>
            {
            })
            .CleanupAfterEachIteration(async (context) =>
            {
            })
            .CleanupAfterScenario((context) =>
            {
            })
            .CleanupAfterScenario(async (context) =>
            {
            })
            .Run();
    }

    [Ignore]
    [ScenarioTest]
    public async Task CustomContext_Load()
    {
        await Scenario<CustomContext>("Syntax Showcase with custom context")
            .Step("Step 1 - Set property on context", context =>
            {
                context.CustomProperty = "value1"; // Set data in context which is shared between steps.
            })
            .Step("Step 2 - Read property from context", context =>
            {
                Assert.AreEqual("value1", context.CustomProperty);
            })
            .Run();
    }

    [Ignore]
    [ScenarioTest]
    public async Task CustomContext()
    {
        await Scenario<CustomContext>("Syntax Showcase with custom context")
            .Step("Step 1 - Set property on context", context =>
            {
                context.CustomProperty = "value1"; // Set data in context which is shared between steps.
            })
            .Step("Step 2 - Read property from context", context =>
            {
                Assert.AreEqual("value1", context.CustomProperty);
            })
            .Run();
    }

    public void SharedStep(StepContext context)
    {
        // Some code goes here.
    }
}

public class CustomContext : StepContext
{
    public string CustomProperty { get; set; }
}
