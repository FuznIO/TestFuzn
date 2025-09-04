using Microsoft.Extensions.Logging;

namespace FuznLabs.TestFuzn.Tests;

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
    public async Task DefaultContext_Feature()
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

                // Define sub-steps.
                context.Step("Sub step 1.1", subcontext1 =>
                { 
                    context.Step("Sub step 1.1.1", subcontext2 =>
                    {
                    });
                });

                // Some code goes here.
            })
            .Step("Step 2 - Async with context", async (context) =>
            {
                // Some code goes here.
                // Attach file to the current step.
                await context.CurrentStep.Attach("file1.txt", "Some content");
                await context.CurrentStep.Attach($"screenshot.png", new byte[0]);
                await context.CurrentStep.Attach($"screenshot.png", new MemoryStream());

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
        // Definition of test types:
        // Feature Test = A test runs once with one set of input data, or run multiple times with different input data.
        // Load Test = A test runs multiple times with the same input data, simulating load on the system.
        await Scenario("Default Context Showcase")
            // Init is the first method that will be run.
            .Init((context) =>
            {
            })
            .Init(async (context) =>
            {
            })
            // InputData will run before steps. Only one of the InputData* methods should be used.
            // For feature test: Defines the number of iterations the test will be run.
            // For load test: Load().Simulations() defines the number of iterations the test will run, input data provides test data for each iteration.
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
            // Defines how the input data is provided to each execution
            // Runs through the input data sequentially.
            .InputDataBehavior(InputDataBehavior.Loop)
            // Runs through the input data randomly.
            .InputDataBehavior(InputDataBehavior.Random)
            // Load test specific: Runs through the input data sequentially, then randomly.
            .InputDataBehavior(InputDataBehavior.LoopThenRandom)
            // Load test specific: Runs through the input data sequentially, then repeats the last input data for the remaining iterations.
            .InputDataBehavior(InputDataBehavior.LoopThenRepeatLast)
            // Steps are executed in order. If one steps fails within an execution, the rest of the steps will be skipped (=not executed).
            .Step("Step 1 - Sync with context", context =>
            {
                // Get input data for the row row.
                var user = context.InputData<string>();

                // Set data in context which is shared between steps.
                context.SetSharedData("item1", "value1"); 
                // Get data.
                var value1 = context.GetSharedData<string>("item1");

                context.Logger.LogInformation($"User: {user}"); // Log information.

                // Some code goes here.

                context.Step("Step 1.1", (inlineContext) =>
                {
                    // Some code goes here.
                });
            })
            .Step("Step 2 - Async with context", async (context) =>
            {
                // Some code goes here.
                await Task.CompletedTask;

                await context.Step("Step 2.1", async (inlineContext) =>
                {
                    // Some code goes here.
                    await Task.CompletedTask;
                });
            })
            .Step("Step 3 - Shared step", SharedStep)
            // Warmup simulations run before .Load().Simulations(). 
            // For these simulations no stats will be recorded, AssertWhileRunning, AssertWhenDone and sinks will not be called.
            .Load().Warmup((context, simulations) =>
            {
                simulations.FixedConcurrentLoad(10, TimeSpan.FromSeconds(3));
            })
            // Supports both sync and async.
            .Load().Simulations((context, simulations) =>
            {
                simulations.GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5));
                simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
                simulations.OneTimeLoad(100);
                simulations.Pause(TimeSpan.FromSeconds(5));
                simulations.FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100));
                simulations.RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100));
            })
            .Load().Simulations(async (context, simulations) =>
            {
                simulations.GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5));
                simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
                simulations.OneTimeLoad(100);
                simulations.Pause(TimeSpan.FromSeconds(5));
                simulations.FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100));
                simulations.RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100));
                await Task.CompletedTask;
            })
            .Load().AssertWhileRunning((context, stats) =>
            {
                Assert.IsTrue(stats.RequestCount < 1000);
                Assert.AreEqual(0, stats.Ok.RequestCount);
                Assert.AreEqual(0, stats.Failed.RequestCount);
                Assert.IsTrue(stats.Ok.ResponseTimeMean > TimeSpan.FromSeconds(0.1));
                Assert.IsTrue(stats.Failed.ResponseTimeMean > TimeSpan.FromSeconds(0.1));
                Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.8));
                Assert.IsTrue(stats.Failed.ResponseTimeMean < TimeSpan.FromSeconds(0.8));
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.IsTrue(stats.RequestCount == 100);
                Assert.IsTrue(stats.Ok.RequestCount == 100);
                Assert.IsTrue(stats.Failed.RequestCount == 0);
                Assert.IsTrue(stats.Ok.ResponseTimeStandardDeviation < TimeSpan.FromSeconds(0.5));
                Assert.IsTrue(stats.Ok.ResponseTimeMean > TimeSpan.FromSeconds(0.5));
                Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.8));
            })
            .Load().IncludeScenario(Scenario("Scenario2").Step("Step1", (context) => { }))
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
                context.Custom.CustomProperty = "value1"; // Set data in context which is shared between steps.
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
                context.Custom.CustomProperty = "value1"; // Set data in context which is shared between steps.
            })
            .Step("Step 2 - Read property from context", context =>
            {
                Assert.AreEqual("value1", context.Custom.CustomProperty);
            })
            .Run();
    }

    public void SharedStep(StepContext context)
    {
        // Some code goes here.
    }
}

public class CustomContext : StepContext<CustomContext>
{
    public string CustomProperty { get; set; }
}
