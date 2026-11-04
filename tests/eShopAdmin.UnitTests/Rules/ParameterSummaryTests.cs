using System.Text.Json;
using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class ParameterSummaryTests
{
    private static JsonElement J(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [TestMethod]
    [DataRow("ORD-001", """{"minutes":10}""", "10 minutes")]
    [DataRow("ORD-001", """{"minutes":1}""", "1 minute")]
    [DataRow("ORD-013", """{"policy":"AllOrNothing"}""", "All or nothing")]
    [DataRow("ORD-013", """{"policy":"PartialFulfilment"}""", "Partial fulfilment")]
    [DataRow("SHP-001", """{"fee":4.99,"freeShippingThreshold":50}""", "Fee $4.99, free from $50.00")]
    [DataRow("SHP-001", """{"fee":4.99,"freeShippingThreshold":null}""", "Fee $4.99")]
    [DataRow("SHP-001", """{"fee":0,"freeShippingThreshold":null}""", "Fee $0.00")]
    [DataRow("SHP-001", """{"fee":5,"freeShippingThreshold":0}""", "Fee $5.00, free from $0.00")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"All","id":null},"mode":"PercentOff","value":5},{"target":{"kind":"Type","id":1},"mode":"PercentOff","value":10},{"target":{"kind":"Item","id":9},"mode":"FixedPrice","value":3.5}]}""", "3 price adjustments")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Type","id":1},"mode":"PercentOff","value":10}]}""", "1 price adjustment")]
    [DataRow("PRC-001", """{"entries":[]}""", "No price adjustments")]
    [DataRow("PRM-001", """{"linePromotions":[{"target":{"kind":"Item","id":1},"kind":"PercentOff","value":1,"minUnits":null,"label":"a"},{"target":{"kind":"Item","id":2},"kind":"PercentOff","value":1,"minUnits":null,"label":"b"}],"orderPromotions":[{"kind":"AmountOff","value":5,"minSubtotal":50,"label":"c"}]}""", "2 line, 1 order promotions")]
    [DataRow("PRM-001", """{"linePromotions":[],"orderPromotions":[]}""", "0 line, 0 order promotions")]
    [DataRow("XYZ-001", """{"anything":1}""", "Custom value")]
    [DataRow("ORD-001", """{"minutes":5,"extra":1}""", "Custom value")]
    [DataRow("ORD-001", """{}""", "Custom value")]
    [DataRow("SHP-001", """{"fee":"x"}""", "Custom value")]
    [DataRow("PRC-001", """[]""", "Custom value")]
    public void ParameterSummaryDescribesEachType(string ruleTypeId, string json, string expected)
    {
        Assert.AreEqual(expected, ParameterSummary.Describe(ruleTypeId, J(json)));
    }

    [TestMethod]
    public void UndefinedParametersAreCustomValue()
    {
        Assert.AreEqual("Custom value", ParameterSummary.Describe("ORD-001", default));
    }
}
