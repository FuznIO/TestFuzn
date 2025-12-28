namespace Fuzn.TestFuzn.Tests.ConsoleOutput
{
    [TestClass]
    public class ConsoleOutputTests : BaseFeatureTest
    {
        [Test]
        public async Task FeatureTest()
        {
            await Scenario()
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                .Step("Step 5", (context) => { })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Run();
        }

        [Test]
        public async Task FeatureTestWithInputData()
        {
            await Scenario()
                .InputData(new User("User1", "user1@foo.com"),
                    new User("User2", "user2@foo.com"),
                    new User("User3", "user3@foo.com"))
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                .Step("Step 5", (context) => { })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Run();
        }

        [Test]
        public async Task LoadTest()
        {
            await Scenario()
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                .Step("Step 5", (context) => { })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Load().Simulations((context, simulations) =>
                {
                    simulations.OneTimeLoad(5);
                    simulations.FixedLoad(2, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
                })
                .Run();
        }

        [Test]
        public async Task LoadTest_MutipleScenarios()
        {
            var scenario2 = Scenario("Scenario2")
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                .Step("Step 5", (context) => { })
                .Load().Simulations((context, simulations) => simulations.FixedLoad(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));

            await Scenario("Scenario1")
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                .Step("Step 5", (context) => { })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Load().Simulations((context, simulations) => simulations.FixedLoad(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20)))
                .Load().IncludeScenario(scenario2)
                .Run();
        }

        [Test]
        public async Task LoadTest_WithError()
        {
            int i = 0;

            await Scenario()
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                .Step("Step 5", (context) => {
                    Interlocked.Increment(ref i);
                    if (i == 2)
                        Assert.Fail(); 
                })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(2))
                .Run();
        }

        [Test]
        public async Task LoadTest_LongRunning()
        {
            int i = 0;

            await Scenario()
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                //.Step("Step 4", async (context) => { await Task.Delay(TimeSpan.FromMilliseconds(100));  })
                .Step("Step 5", (context) =>
                {
                    Interlocked.Increment(ref i);
                    if (i % 3 == 0)
                        Assert.Fail();
                })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Load().Simulations((context, simulations) => simulations.FixedLoad(400, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60)))
                .Run();
        }


        [Test]
        public async Task LoadTest_LongRunning2()
        {
            await Scenario()
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                .Step("Step 5", (context) => { })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Load().Simulations((context, simulations) => simulations.FixedLoad(500, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60)))
                .Run();
        }

        public class User(string name, string email)
        {
            public string Name { get; set; } = name;
            public string Email { get; set; } = email;
        }
    }
}
