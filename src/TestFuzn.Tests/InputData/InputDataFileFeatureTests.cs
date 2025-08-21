namespace FuznLabs.TestFuzn.Tests.InputData;

[FeatureTest]
public class InputDataFileFeatureTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Verify_Csv_InputData()
    {
        await Scenario()
            .InputDataFromList(async context =>
            {
                Assert.IsNotNull(context);
                return await InputDataFileHelper.LoadFromCsv<SimpleUser>("InputData/users.csv");
            })
            .InputDataBehavior(InputDataBehavior.LoopThenRepeatLast)
            .Step("Arrange", async context => { await Task.Delay(100);})
            .Step("Act", async context =>
            {
                await Task.Delay(100);
                var user = context.InputData<SimpleUser>();
                Assert.IsNotNull(user);
                // Assert on each row
                if (user.Username == "jdoe")
                {
                    Assert.AreEqual("John Doe", user.Name);
                    Assert.AreEqual("Main St", user.Street);
                    Assert.AreEqual("Oslo", user.City);
                    Assert.AreEqual("0010", user.ZipCode);
                    Assert.AreEqual(30, user.Age);
                    Assert.AreEqual(55000.50m, user.Income);
                    Assert.IsTrue(user.IsActive);
                }
                else if (user.Username == "jsmith")
                {
                    Assert.AreEqual("Jane Smith", user.Name);
                    Assert.AreEqual("Second St", user.Street);
                    Assert.AreEqual("Bergen", user.City);
                    Assert.AreEqual("5003", user.ZipCode);
                    Assert.AreEqual(28, user.Age);
                    Assert.AreEqual(62000.00m, user.Income);
                    Assert.IsFalse(user.IsActive);
                }
                else if (user.Username == "ajohnson")
                {
                    Assert.AreEqual("Alice Johnson", user.Name);
                    Assert.AreEqual("Third St", user.Street);
                    Assert.AreEqual("Trondheim", user.City);
                    Assert.AreEqual("7011", user.ZipCode);
                    Assert.AreEqual(35, user.Age);
                    Assert.AreEqual(71000.75m, user.Income);
                    Assert.IsTrue(user.IsActive);
                }
                else
                {
                    Assert.Fail($"Unexpected user: {user.Username}");
                }
            })
            .Step("Assert", async context => { await Task.Delay(100); })
            .Run();
    }

    [ScenarioTest]
    public async Task Verify_Json_InputData()
    {
        await Scenario()
            .InputDataFromList(async context =>
            {
                Assert.IsNotNull(context);
                return await InputDataFileHelper.LoadFromJson<ComplexUser>("InputData/users.json");
            })
            .Step("Verify", context =>
            {
                var user = context.InputData<ComplexUser>();
                Assert.IsNotNull(user);
                if (user.Username == "jdoe")
                {
                    Assert.AreEqual("John Doe", user.Name);
                    Assert.AreEqual(1, user.Address.Length);
                    Assert.AreEqual("Main St", user.Address[0].Street);
                    Assert.AreEqual("Oslo", user.Address[0].City);
                    Assert.AreEqual(1, user.PhoneNumbers.Count);
                    Assert.AreEqual("+47", user.PhoneNumbers[0].CountyCode);
                    Assert.AreEqual("12345678", user.PhoneNumbers[0].Number);
                    Assert.AreEqual(0, user.Counter);
                }
                else if (user.Username == "jsmith")
                {
                    Assert.AreEqual("Jane Smith", user.Name);
                    Assert.AreEqual(1, user.Address.Length);
                    Assert.AreEqual("Second St", user.Address[0].Street);
                    Assert.AreEqual("Bergen", user.Address[0].City);
                    Assert.AreEqual(2, user.PhoneNumbers.Count);
                    Assert.AreEqual("+47", user.PhoneNumbers[0].CountyCode);
                    Assert.AreEqual("87654321", user.PhoneNumbers[0].Number);
                    Assert.AreEqual("+46", user.PhoneNumbers[1].CountyCode);
                    Assert.AreEqual("11223344", user.PhoneNumbers[1].Number);
                    Assert.AreEqual(0, user.Counter);
                }
                else if (user.Username == "ajohnson")
                {
                    Assert.AreEqual("Alice Johnson", user.Name);
                    Assert.AreEqual(1, user.Address.Length);
                    Assert.AreEqual("Third St", user.Address[0].Street);
                    Assert.AreEqual("Trondheim", user.Address[0].City);
                    Assert.AreEqual(1, user.PhoneNumbers.Count);
                    Assert.AreEqual("+45", user.PhoneNumbers[0].CountyCode);
                    Assert.AreEqual("99887766", user.PhoneNumbers[0].Number);
                    Assert.AreEqual(0, user.Counter);
                }
                else
                {
                    Assert.Fail($"Unexpected user: {user.Username}");
                }
            })
            .Run();
    }
}
