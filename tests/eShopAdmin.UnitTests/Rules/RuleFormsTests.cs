using System.Text.Json;
using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class RuleFormsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static JsonElement J(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [TestMethod]
    [DataRow("ORD-001", true)]
    [DataRow("ORD-013", true)]
    [DataRow("SHP-001", true)]
    [DataRow("PRC-001", true)]
    [DataRow("PRM-001", true)]
    [DataRow("XYZ-001", false)]
    [DataRow("ord-001", false)]
    [DataRow("", false)]
    public void IsSupportedCoversTheFiveKnownTypes(string ruleTypeId, bool expected)
    {
        Assert.AreEqual(expected, RuleForms.IsSupported(ruleTypeId));
    }

    [TestMethod]
    [DataRow("ORD-001", """{"minutes":1}""")]
    [DataRow("ORD-013", """{"policy":"AllOrNothing"}""")]
    [DataRow("SHP-001", """{"fee":0,"freeShippingThreshold":null}""")]
    [DataRow("PRC-001", """{"entries":[]}""")]
    [DataRow("PRM-001", """{"linePromotions":[],"orderPromotions":[]}""")]
    [DataRow("ORD-001", """{"minutes":60}""")]
    [DataRow("ORD-013", """{"policy":"PartialFulfilment"}""")]
    [DataRow("SHP-001", """{"fee":4.99,"freeShippingThreshold":50}""")]
    [DataRow("SHP-001", """{"fee":10,"freeShippingThreshold":50.5}""")]
    [DataRow("PRM-001", """{"linePromotions":[],"orderPromotions":[{"kind":"AmountOff","value":5.25,"minSubtotal":0,"label":"Five off"}]}""")]
    public void EachSupportedTypeRoundTrips(string ruleTypeId, string json)
    {
        var parameters = J(json);

        var input = DraftInput.FromParameters(ruleTypeId, parameters, Now, null);

        Assert.IsNotNull(input);
        Assert.IsTrue(JsonElement.DeepEquals(parameters, input.ToParameters(ruleTypeId)), input.ToParameters(ruleTypeId).GetRawText());
    }

    [TestMethod]
    public void PriceAdjustmentExampleRoundTrips()
    {
        var json = J("""{"entries":[{"target":{"kind":"Type","id":1},"mode":"PercentOff","value":10}]}""");

        var input = DraftInput.FromParameters("PRC-001", json, Now, null)!;

        Assert.HasCount(1, input.PriceAdjustment.Entries);
        var entry = input.PriceAdjustment.Entries[0];
        Assert.AreEqual("Type", entry.Target.Kind);
        Assert.AreEqual(1, entry.Target.Id);
        Assert.AreEqual("PercentOff", entry.Mode);
        Assert.AreEqual(10m, entry.Value);
        Assert.IsTrue(JsonElement.DeepEquals(json, input.ToParameters("PRC-001")));
    }

    [TestMethod]
    public void PromotionExampleRoundTrips()
    {
        var json = J("""{"linePromotions":[{"target":{"kind":"Item","id":99},"kind":"PercentOff","value":10,"minUnits":null,"label":"10% off"}],"orderPromotions":[]}""");

        var input = DraftInput.FromParameters("PRM-001", json, Now, null)!;

        Assert.IsTrue(JsonElement.DeepEquals(json, input.ToParameters("PRM-001")));
    }

    [TestMethod]
    public void PromotionWithMinUnitsAndBothListsRoundTrips()
    {
        var json = J("""{"linePromotions":[{"target":{"kind":"Brand","id":2},"kind":"AmountOff","value":2.5,"minUnits":3,"label":"Bulk"},{"target":{"kind":"All","id":null},"kind":"PercentOff","value":5,"minUnits":null,"label":"All"}],"orderPromotions":[{"kind":"PercentOff","value":15,"minSubtotal":100,"label":"Big basket"}]}""");

        var input = DraftInput.FromParameters("PRM-001", json, Now, null)!;

        Assert.AreEqual(3, input.Promotion.LinePromotions[0].MinUnits);
        Assert.IsNull(input.Promotion.LinePromotions[1].MinUnits);
        Assert.AreEqual(100m, input.Promotion.OrderPromotions[0].MinSubtotal);
        Assert.IsTrue(JsonElement.DeepEquals(json, input.ToParameters("PRM-001")));
    }

    [TestMethod]
    public void NumbersWithOrWithoutADecimalPointRoundTrip()
    {
        var json = J("""{"entries":[{"target":{"kind":"Item","id":7},"mode":"FixedPrice","value":10.0}]}""");

        var input = DraftInput.FromParameters("PRC-001", json, Now, null)!;

        Assert.IsTrue(JsonElement.DeepEquals(json, input.ToParameters("PRC-001")));
        Assert.IsTrue(JsonElement.DeepEquals(J("""{"entries":[{"target":{"kind":"Item","id":7},"mode":"FixedPrice","value":10}]}"""), input.ToParameters("PRC-001")));
    }

    [TestMethod]
    [DataRow("ORD-001", """{"minutes":5,"extra":1}""")]
    [DataRow("ORD-001", """{}""")]
    [DataRow("ORD-001", """{"minutes":"5"}""")]
    [DataRow("ORD-001", """{"minutes":5.5}""")]
    [DataRow("ORD-001", """{"minutes":null}""")]
    [DataRow("ORD-001", """[5]""")]
    [DataRow("ORD-001", """5""")]
    [DataRow("ORD-001", """null""")]
    [DataRow("ORD-013", """{"policy":"Bogus"}""")]
    [DataRow("ORD-013", """{"policy":0}""")]
    [DataRow("ORD-013", """{"policy":"AllOrNothing","other":true}""")]
    [DataRow("SHP-001", """{"fee":"x"}""")]
    [DataRow("SHP-001", """{"fee":1,"freeShippingThreshold":"50"}""")]
    [DataRow("SHP-001", """{"fee":1,"freeShipping":50}""")]
    [DataRow("PRC-001", """{}""")]
    [DataRow("PRC-001", """{"entries":null}""")]
    [DataRow("PRC-001", """{"entries":{}}""")]
    [DataRow("PRC-001", """{"entries":[1]}""")]
    [DataRow("PRC-001", """{"entries":[],"extra":1}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Type","id":1},"mode":"PercentOff"}]}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Type","id":1},"mode":"Half","value":1}]}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Type","id":1},"mode":"PercentOff","value":1,"note":"x"}]}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Cat","id":1},"mode":"PercentOff","value":1}]}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Type","id":1,"extra":1},"mode":"PercentOff","value":1}]}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Type","id":"1"},"mode":"PercentOff","value":1}]}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"All","id":3},"mode":"PercentOff","value":1}]}""")]
    [DataRow("PRC-001", """{"entries":[{"target":{"kind":"Type","id":1},"mode":"PercentOff","value":"1"}]}""")]
    [DataRow("PRM-001", """{"linePromotions":[]}""")]
    [DataRow("PRM-001", """{"linePromotions":[],"orderPromotions":[{"kind":"PercentOff","value":1,"minSubtotal":0}]}""")]
    [DataRow("PRM-001", """{"linePromotions":[{"target":{"kind":"Item","id":1},"kind":"PercentOff","value":1,"minUnits":"2","label":"x"}],"orderPromotions":[]}""")]
    [DataRow("PRM-001", """{"linePromotions":[{"target":{"kind":"Item","id":1},"kind":"PercentOff","value":1,"minUnits":1,"label":5}],"orderPromotions":[]}""")]
    [DataRow("PRM-001", """{"linePromotions":[],"orderPromotions":[{"kind":"FixedPrice","value":1,"minSubtotal":0,"label":"x"}]}""")]
    [DataRow("XYZ-001", """{}""")]
    public void FromParametersReturnsNullForUnrecognisedShape(string ruleTypeId, string json)
    {
        Assert.IsNull(DraftInput.FromParameters(ruleTypeId, J(json), Now, null));
    }

    [TestMethod]
    public void FromParametersReturnsNullForUndefinedElement()
    {
        Assert.IsNull(DraftInput.FromParameters("ORD-001", default, Now, null));
    }

    [TestMethod]
    public void FromParametersReturnsNullForDuplicateMembers()
    {
        Assert.IsNull(DraftInput.FromParameters("ORD-001", J("""{"minutes":5,"minutes":6}"""), Now, null));
    }

    [TestMethod]
    public void OptionalMembersMayBeOmitted()
    {
        var shipping = DraftInput.FromParameters("SHP-001", J("""{"fee":3}"""), Now, null)!;
        Assert.AreEqual(3m, shipping.ShippingFee.Fee);
        Assert.IsNull(shipping.ShippingFee.FreeShippingThreshold);

        var promotion = DraftInput.FromParameters("PRM-001", J("""{"linePromotions":[{"target":{"kind":"All"},"kind":"PercentOff","value":1,"label":"x"}],"orderPromotions":[]}"""), Now, null)!;
        Assert.IsNull(promotion.Promotion.LinePromotions[0].MinUnits);
        Assert.IsNull(promotion.Promotion.LinePromotions[0].Target.Id);
    }

    [TestMethod]
    public void FromParametersConvertsEffectiveTimesToUtc()
    {
        var from = new DateTimeOffset(2026, 12, 1, 9, 30, 0, TimeSpan.FromHours(-5));
        var to = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.FromHours(2));

        var input = DraftInput.FromParameters("ORD-001", J("""{"minutes":5}"""), from, to)!;

        Assert.AreEqual(new DateTime(2026, 12, 1, 14, 30, 0, DateTimeKind.Utc), input.EffectiveFromUtc);
        Assert.AreEqual(DateTimeKind.Utc, input.EffectiveFromUtc.Kind);
        Assert.AreEqual(new DateTime(2026, 12, 30, 22, 0, 0, DateTimeKind.Utc), input.EffectiveToUtc);
        Assert.IsNull(DraftInput.FromParameters("ORD-001", J("""{"minutes":5}"""), from, null)!.EffectiveToUtc);
    }

    [TestMethod]
    public void TargetAllWritesNullId()
    {
        var input = new DraftInput();
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm { Target = new TargetForm { Kind = "All", Id = 5 }, Mode = "PercentOff", Value = 10 });

        var entry = input.ToParameters("PRC-001").GetProperty("entries")[0].GetProperty("target");

        Assert.AreEqual("All", entry.GetProperty("kind").GetString());
        Assert.AreEqual(JsonValueKind.Null, entry.GetProperty("id").ValueKind);
    }

    [TestMethod]
    public void ToParametersWritesNullForMissingMinUnitsAndThreshold()
    {
        var input = new DraftInput();
        input.Promotion.LinePromotions.Add(new LinePromotionForm { Target = new TargetForm { Kind = "Item", Id = 9 }, Label = "x", Value = 1 });

        var line = input.ToParameters("PRM-001").GetProperty("linePromotions")[0];
        Assert.AreEqual(JsonValueKind.Null, line.GetProperty("minUnits").ValueKind);
        Assert.AreEqual(9, line.GetProperty("target").GetProperty("id").GetInt32());

        Assert.AreEqual(JsonValueKind.Null, input.ToParameters("SHP-001").GetProperty("freeShippingThreshold").ValueKind);
    }

    [TestMethod]
    public void ToParametersEmitsOnlyTheFormMatchingTheRuleType()
    {
        var input = new DraftInput { GracePeriod = new GracePeriodForm { Minutes = 12 } };

        Assert.IsTrue(JsonElement.DeepEquals(J("""{"minutes":12}"""), input.ToParameters("ORD-001")));
        Assert.IsTrue(JsonElement.DeepEquals(J("""{"policy":"AllOrNothing"}"""), input.ToParameters("ORD-013")));
    }

    [TestMethod]
    public void ToParametersRejectsUnsupportedType()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DraftInput().ToParameters("XYZ-001"));
    }

    [TestMethod]
    public void NewFormsHaveUsableDefaults()
    {
        var input = new DraftInput();

        Assert.AreEqual(1, input.GracePeriod.Minutes);
        Assert.AreEqual("AllOrNothing", input.ShortfallPolicy.Policy);
        Assert.AreEqual(0m, input.ShippingFee.Fee);
        Assert.IsEmpty(input.PriceAdjustment.Entries);
        Assert.IsEmpty(input.Promotion.LinePromotions);
        Assert.IsEmpty(input.Promotion.OrderPromotions);
        Assert.AreEqual("All", new TargetForm().Kind);
        Assert.AreEqual("PercentOff", new PriceAdjustmentEntryForm().Mode);
        Assert.AreEqual("PercentOff", new LinePromotionForm().Kind);
        Assert.AreEqual("PercentOff", new OrderPromotionForm().Kind);
    }

    [TestMethod]
    public void AddAndRemoveEntryKeepOrder()
    {
        var input = new DraftInput();

        input.AddEntry("Entries");
        input.AddEntry("Entries");
        input.AddEntry("Entries");
        input.PriceAdjustment.Entries[0].Value = 1;
        input.PriceAdjustment.Entries[1].Value = 2;
        input.PriceAdjustment.Entries[2].Value = 3;
        input.RemoveEntry("Entries", 1);

        CollectionAssert.AreEqual(new[] { 1m, 3m }, input.PriceAdjustment.Entries.Select(e => e.Value).ToArray());

        input.AddEntry("LinePromotions");
        input.AddEntry("LinePromotions");
        input.Promotion.LinePromotions[0].Label = "a";
        input.Promotion.LinePromotions[1].Label = "b";
        input.RemoveEntry("LinePromotions", 0);
        CollectionAssert.AreEqual(new[] { "b" }, input.Promotion.LinePromotions.Select(l => l.Label).ToArray());

        input.AddEntry("OrderPromotions");
        input.AddEntry("OrderPromotions");
        input.Promotion.OrderPromotions[1].Label = "z";
        input.RemoveEntry("OrderPromotions", 0);
        CollectionAssert.AreEqual(new[] { "z" }, input.Promotion.OrderPromotions.Select(o => o.Label).ToArray());
    }

    [TestMethod]
    public void RemoveEntryIgnoresOutOfRangeIndexAndRejectsUnknownList()
    {
        var input = new DraftInput();
        input.AddEntry("Entries");

        input.RemoveEntry("Entries", 5);
        input.RemoveEntry("Entries", -1);

        Assert.HasCount(1, input.PriceAdjustment.Entries);
        Assert.ThrowsExactly<ArgumentException>(() => input.AddEntry("Nope"));
        Assert.ThrowsExactly<ArgumentException>(() => input.RemoveEntry("Nope", 0));
    }
}
