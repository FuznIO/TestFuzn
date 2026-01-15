#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
using Microsoft.Extensions.Logging;

namespace Fuzn.TestFuzn.Tests;

[TestClass]
[Group("TestFuzn Group Example")]
public class SyntaxTests : Test, IBeforeTest, IAfterTest
{
    public Task BeforeTest(Context context)
    {
        return Task.CompletedTask;
    }

    public Task AfterTest(Context context)
    {
        return Task.CompletedTask;
    }

    [Skip]
    [Test(Name = "Default context syntax showcase",
        Description = "Showcase of all syntax options with the default context.",
        Id = "Test-Id-1234")]
    [Metadata("Key1", "Value1")]
    [Metadata("Key2", "Value2")]
    [Tags("UT", "IT")]
    [TargetEnvironments("Dev", "Test")]
    public async Task DefaultContext()
    {
        var scenario2 = Scenario("Scenario2").Step("Step1", (context) => { })
            .Load().Simulations((context, builder) =>
            {
                builder.FixedLoad(5, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
            });

        // Definition of test types:
        // Standard Test = A test runs once with one set of input data, or run multiple times with different input data.
        // Load Test = A test runs multiple times with the same input data, simulating load on the system.
        await Scenario()
            .Id("Scenario-Id-1234") // Optional id for the scenario.
            // BeforeScenario is the first method that will be run.
            .BeforeScenario((context) =>
            {
            })
            .BeforeScenario(async (context) =>
            {
                // This will be executed before any steps.
            })
            // InputData will run before steps. Only one of the InputData* methods should be used.
            // For standard test: Defines the number of iterations the test will be run.
            // For load test: Load().Simulations() defines the number of iterations the test will run, input data provides test data for each iteration.
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
            .BeforeIteration((context) =>
            {
                // This will be executed once per iteration, before any steps.
            })
            .BeforeIteration(async (context) =>
            {
                // This will be execute once per iteration before any steps.
            })
            .Step("Step 1 - Sync with context", context =>
            {
                // Some code goes here.
            })
            .Step("Step 2 - Async with context", async (context) =>
            {
                // Some code goes here.
                await Task.CompletedTask;
            })
            .SharedStepExtension() // Extension method for shared steps.
            .Step("Step 3 - Shared step using action", SharedStepAction)
            .Step("Step 4 - Shared step type regular method", context => SharedMethod("value"))
            .Step("Step 5 - All functionality", "ID-1234", async context => 
            {
                // Get input data for the row row.
                var user = context.InputData<string>();

                // SharedData - data shared between steps within the same iteration.
                context.SetSharedData("item1", "value1"); 
                var value1 = context.GetSharedData<string>("item1");

                // Log information to log file.
                context.Logger.LogInformation($"User: {user}");

                // Some code goes here.
                // Support for sub-steps, sync/async.
                context.Step("Sub step 1.1", subcontext1 =>
                { 
                    subcontext1.Step("Sub step 1.1.1", subcontext2 =>
                    {
                    });
                });

                // Comments: Standard test: Outputted to console and reports. Load tests: Outputted to log file
                context.Comment("Opening"); 
                context.Comment("Closing");

                // Attach file to the current step.
                await context.Attach("file1.txt", "Some content");
                await context.Attach($"screenshot.png", new byte[0]);
                await context.Attach($"screenshot.png", new MemoryStream());
            })
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
                Assert.IsLessThan(1000, stats.RequestCount);
                Assert.AreEqual(0, stats.Ok.RequestCount);
                Assert.AreEqual(0, stats.Failed.RequestCount);
                Assert.IsLessThan(TimeSpan.FromSeconds(0.1), stats.Ok.ResponseTimeMean);
                Assert.IsLessThan(TimeSpan.FromSeconds(0.1), stats.Failed.ResponseTimeMean);
                Assert.IsLessThan(TimeSpan.FromSeconds(0.8), stats.Ok.ResponseTimeMean);
                Assert.IsLessThan(TimeSpan.FromSeconds(0.8), stats.Failed.ResponseTimeMean);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(100, stats.RequestCount);
                Assert.AreEqual(100, stats.Ok.RequestCount);
                Assert.AreEqual(0, stats.Failed.RequestCount);
                Assert.IsLessThan(TimeSpan.FromSeconds(0.5), stats.Ok.ResponseTimeStandardDeviation);
                Assert.IsLessThan(TimeSpan.FromSeconds(0.5), stats.Ok.ResponseTimeMean);
                Assert.IsLessThan(TimeSpan.FromSeconds(0.8), stats.Ok.ResponseTimeMean);

                Assert.AreEqual(3, stats.GetStep("Sub step 1.1").Ok.RequestCount);
            })
            .Load().IncludeScenario(scenario2)
            .AfterIteration((context) =>
            {
            })
            .AfterIteration(async (context) =>
            {
            })
            .AfterScenario((context) =>
            {
            })
            .AfterScenario(async (context) =>
            {
            })
            .Run();
    }

    [Test]
    public async Task CustomContext()
    {
        await Scenario<CustomModel>("Syntax Showcase with custom context")
            .Step("Step 1 - Set property on context", context =>
            {
                context.Model.CustomProperty = "value1"; // Set data in context which is shared between steps.
            })
            .Step("Step 2 - Read property from context", context =>
            {
                Assert.AreEqual("value1", context.Model.CustomProperty);
            })
            .Run();
    }

    public async Task SharedStepAction(IterationContext<EmptyModel> context)
    {
        // Some code goes here.
    }

    public void SharedMethod(string value)
    {
        // Some code goes here.
    }
}

public class CustomModel
{
    public string? CustomProperty { get; set; }
}

public static class SharedSteps
{ 
    public static ScenarioBuilder<T> SharedStepExtension<T>(this ScenarioBuilder<T> builder)
        where T: new()
    {
        builder.Step("Shared step", (context) =>
        {
            // Some code goes here.
            return Task.CompletedTask;
        });

        return builder;
    }
}