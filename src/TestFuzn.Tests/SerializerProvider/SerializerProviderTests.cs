using System.Runtime.Serialization;
using Fuzn.TestFuzn.Contracts.Providers;
using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests.SerializerProvider;

[FeatureTest]
public class SerializerProviderTests : BaseFeatureTest
{
    public override string FeatureName => "SerializerProvider";

    [ScenarioTest]
    public async Task Verify()
    {
        await Scenario("Verify that when System.Text.Json fails, Newtonsoft should pick up the slack")
            .Step("Verify NewtonsoftSerializerProvider is present", VerifyNewtonsoftSerializerProviderIsAdded)
            .Step("Verify that type known to fail with System.Text.Json still deserializes successfully", async (context) =>
            {
                var response = CreateHttpResponse(context, null);
                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Verify2()
    {
        await Scenario("Verify that System.Text.Json properties is detected, and that System.Text.Json is being used")
            .Step("Verify NewtonsoftSerializerProvider is present", VerifyNewtonsoftSerializerProviderIsAdded)
            .Step("Verify IsSerializerSpecific true is preferred", async (context) =>
            {
                var response = CreateHttpResponse(context, SetPriorityNewtonsoftFirst(context));

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<ProductSystemTextJson>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
                Assert.IsTrue(products.All(a => !string.IsNullOrEmpty(a.ProductName)), "");
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Verify3()
    {
        await Scenario("Verify that Newtonsoft.Json properties is detected, and that Newtonsoft.Json is being used")
            .Step("Verify NewtonsoftSerializerProvider is present", VerifyNewtonsoftSerializerProviderIsAdded)
            .Step("Verify IsSerializerSpecific true is preferred", async (context) =>
            {
                var response = CreateHttpResponse(context, SetPrioritySystemTextJsonFirst(context));

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<ProductNewtonsoft>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
                Assert.IsTrue(products.All(a => !string.IsNullOrEmpty(a.ProductName)), "");
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Verify4()
    {
        await Scenario("Verify that System.Text.Json doesnt honor newtonsoft.json attributes")
            .Step("Verify NewtonsoftSerializerProvider is present", VerifyNewtonsoftSerializerProviderIsAdded)
            .Step("Verify failed serialization when using System.Text.Json on type with Newtonsoft.Json attributes", async (context) =>
            {
                var response = CreateHttpResponse(context, RemoveNewtonsoftJsonSerializer(context));

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<ProductNewtonsoft>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
                Assert.IsTrue(products.All(a => string.IsNullOrEmpty(a.ProductName)), "");
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Verify5()
    {
        await Scenario("Verify that Newtonsoft.Json doesn't honor System.Text.Json attributes")
            .Step("Verify NewtonsoftSerializerProvider is present", VerifyNewtonsoftSerializerProviderIsAdded)
            .Step("Verify failed serialization when using Newtonsoft.Json on type with System.Text.Json attributes", async (context) =>
            {
                var response = CreateHttpResponse(context, RemoveSystemTextJsonSerializer(context));

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<ProductSystemTextJson>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
                Assert.IsTrue(products.All(a => string.IsNullOrEmpty(a.ProductName)), "");
            })
            .Run();
    }

    private Task VerifyNewtonsoftSerializerProviderIsAdded(StepContext stepContext)
    {
        Assert.IsTrue(stepContext.SerializerProvider.Any(a => a.GetType().FullName == "TestFusion.Plugins.Newtonsoft.Internals.NewtonsoftSerializerProvider"));
        return Task.CompletedTask;
    }

    private HttpResponse CreateHttpResponse(StepContext context, HashSet<ISerializerProvider>? serializers)
    {
        var type = typeof(HttpResponse);
        var propertyBindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var response = (HttpResponse)FormatterServices.GetUninitializedObject(typeof(HttpResponse));
        type.GetProperty("Body", propertyBindingFlags)!.SetValue(response, "[{\"id\":\"bb423518-a6a6-4914-b044-58d08a4d0508\",\"name\":\"string\",\"price\":1.0}]");
        type.GetProperty("InnerResponse", propertyBindingFlags)!.SetValue(response, new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var serializerProvidersField = typeof(HttpResponse).GetField("_serializerProviders", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        serializerProvidersField!.SetValue(response, serializers ?? context.SerializerProvider);

        return response;
    }

    private HashSet<ISerializerProvider> SetPriorityNewtonsoftFirst(StepContext context)
    {
        var newHashSet = context.SerializerProvider.ToHashSet();
        foreach (var provider in newHashSet)
        {
            var backingField = provider.GetType().GetField("<Priority>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            backingField!.SetValue(provider,
                provider.GetType().FullName is "TestFusion.Plugins.Newtonsoft.Internals.NewtonsoftSerializerProvider"
                    ? 0
                    : 1);
        }

        return newHashSet;
    }

    private HashSet<ISerializerProvider> SetPrioritySystemTextJsonFirst(StepContext context)
    {
        var newHashSet = context.SerializerProvider.ToHashSet();
        foreach (var provider in newHashSet)
        {
            var backingField = provider.GetType().GetField("<Priority>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            backingField!.SetValue(provider,
                provider.GetType().FullName is "TestFusion.Plugins.Newtonsoft.Internals.NewtonsoftSerializerProvider"
                    ? 1
                    : 0);
        }

        return newHashSet;
    }

    private HashSet<ISerializerProvider> RemoveSystemTextJsonSerializer(StepContext context)
    {
        var newHashSet = context.SerializerProvider.ToHashSet();
        newHashSet.RemoveWhere(provider => provider.GetType().FullName == "TestFusion.SystemTextJsonSerializerProvider");
        return newHashSet;
    }

    private HashSet<ISerializerProvider> RemoveNewtonsoftJsonSerializer(StepContext context)
    {
        var newHashSet = context.SerializerProvider.ToHashSet();
        newHashSet.RemoveWhere(provider => provider.GetType().FullName == "TestFusion.Plugins.Newtonsoft.Internals.NewtonsoftSerializerProvider");
        return newHashSet;
    }
}
