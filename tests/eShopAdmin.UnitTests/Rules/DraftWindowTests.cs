using System.Text.Json;
using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class DraftWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 30, TimeSpan.Zero);

    private static DraftInput InputWith(DateTime from, DateTime? to) => new() { EffectiveFromUtc = from, EffectiveToUtc = to };

    [TestMethod]
    public void WindowRejectsEndNotAfterStart()
    {
        var start = new DateTime(2026, 11, 5, 9, 0, 0);

        var sameInstant = InputWith(start, start).ValidateWindow(Now, out _);
        var earlier = InputWith(start, start.AddMinutes(-1)).ValidateWindow(Now, out _);
        var later = InputWith(start, start.AddMinutes(1)).ValidateWindow(Now, out _);

        Assert.HasCount(1, sameInstant);
        Assert.HasCount(1, sameInstant["EffectiveToUtc"]);
        Assert.IsTrue(earlier.ContainsKey("EffectiveToUtc"));
        Assert.IsEmpty(later);
    }

    [TestMethod]
    public void WindowFlagsPastStart()
    {
        var pastStart = Now.UtcDateTime.AddMinutes(-1);
        var futureStart = Now.UtcDateTime.AddMinutes(1);

        var errors = InputWith(pastStart, null).ValidateWindow(Now, out var startsInPast);
        InputWith(futureStart, null).ValidateWindow(Now, out var futureStartsInPast);
        InputWith(Now.UtcDateTime, null).ValidateWindow(Now, out var nowStartsInPast);

        Assert.IsEmpty(errors);
        Assert.IsTrue(startsInPast);
        Assert.IsFalse(futureStartsInPast);
        Assert.IsFalse(nowStartsInPast);
    }

    [TestMethod]
    public void MissingStartIsAFieldErrorNotAPastStart()
    {
        var errors = InputWith(default, null).ValidateWindow(Now, out var startsInPast);

        Assert.IsTrue(errors.ContainsKey("EffectiveFromUtc"));
        Assert.IsFalse(startsInPast);
    }

    [TestMethod]
    public void EmptyEndMeansOpenEnded()
    {
        var input = new DraftInput { EffectiveFromUtc = new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Unspecified), EffectiveToUtc = null };
        input.GracePeriod.Minutes = 10;

        var create = input.ToCreateRequest("ORD-001");
        var update = input.ToUpdateRequest("ORD-001");

        Assert.IsNull(create.EffectiveTo);
        Assert.IsNull(update.EffectiveTo);
        Assert.AreEqual(TimeSpan.Zero, create.EffectiveFrom.Offset);
        Assert.AreEqual(new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero), create.EffectiveFrom);
        Assert.AreEqual(create.EffectiveFrom, update.EffectiveFrom);
    }

    [TestMethod]
    public void RequestsCarryUtcTimesAndParameters()
    {
        var input = InputWith(new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Local), new DateTime(2026, 12, 2, 9, 0, 0));
        input.GracePeriod.Minutes = 10;

        var create = input.ToCreateRequest("ORD-001");
        var update = input.ToUpdateRequest("ORD-001");

        Assert.AreEqual("ORD-001", create.RuleTypeId);
        Assert.AreEqual("default", create.Scope);
        Assert.AreEqual(10, create.Parameters.GetProperty("minutes").GetInt32());
        Assert.AreEqual(10, update.Parameters.GetProperty("minutes").GetInt32());
        Assert.AreEqual(TimeSpan.Zero, create.EffectiveFrom.Offset);
        Assert.AreEqual(new DateTimeOffset(2026, 12, 2, 9, 0, 0, TimeSpan.Zero), create.EffectiveTo);
        Assert.AreEqual(TimeSpan.Zero, update.EffectiveTo!.Value.Offset);
    }

    [TestMethod]
    [DataRow("2026-11-04T10:00:00Z", "2026-11-04T10:00:00Z")]
    [DataRow("2026-11-04T10:00:01Z", "2026-11-04T10:01:00Z")]
    [DataRow("2026-11-04T10:00:59.9Z", "2026-11-04T10:01:00Z")]
    [DataRow("2026-11-04T10:59:30Z", "2026-11-04T11:00:00Z")]
    public void StartDefaultsToTheNextWholeMinute(string now, string expected)
    {
        var rounded = DraftInput.RoundUpToMinute(DateTimeOffset.Parse(now));

        Assert.AreEqual(DateTime.Parse(expected).ToUniversalTime(), rounded);
        Assert.AreEqual(DateTimeKind.Utc, rounded.Kind);
    }

    [TestMethod]
    public void NullStringsFromFormBindingSerialiseAsEmptyStrings()
    {
        var input = new DraftInput();
        input.ShortfallPolicy.Policy = null!;
        var entry = new PriceAdjustmentEntryForm { Mode = null!, Target = new TargetForm { Kind = null! } };
        input.PriceAdjustment.Entries.Add(entry);
        input.Promotion.LinePromotions.Add(new LinePromotionForm { Kind = null!, Label = null!, Target = new TargetForm { Kind = null! } });
        input.Promotion.OrderPromotions.Add(new OrderPromotionForm { Kind = null!, Label = null! });

        var shortfall = input.ToParameters(RuleForms.ShortfallPolicy);
        var price = input.ToParameters(RuleForms.PriceAdjustment).GetProperty("entries")[0];
        var promotions = input.ToParameters(RuleForms.Promotion);
        var line = promotions.GetProperty("linePromotions")[0];
        var order = promotions.GetProperty("orderPromotions")[0];

        Assert.AreEqual(string.Empty, shortfall.GetProperty("policy").GetString());
        Assert.AreEqual(string.Empty, price.GetProperty("mode").GetString());
        Assert.AreEqual(string.Empty, price.GetProperty("target").GetProperty("kind").GetString());
        Assert.AreEqual(string.Empty, line.GetProperty("kind").GetString());
        Assert.AreEqual(string.Empty, line.GetProperty("label").GetString());
        Assert.AreEqual(string.Empty, line.GetProperty("target").GetProperty("kind").GetString());
        Assert.AreEqual(string.Empty, order.GetProperty("kind").GetString());
        Assert.AreEqual(string.Empty, order.GetProperty("label").GetString());
    }

    [TestMethod]
    public void NullNestedFormsFromFormBindingBecomeEmptyOnes()
    {
        // The SSR form binder assigns null to a nested object or list when the post has no field for it.
        var input = new DraftInput
        {
            GracePeriod = null!,
            ShortfallPolicy = null!,
            ShippingFee = null!,
            PriceAdjustment = null!,
            Promotion = null!,
        };
        input.PriceAdjustment.Entries = null!;
        input.Promotion.LinePromotions = null!;
        input.Promotion.OrderPromotions = null!;

        input.ResolveTargets();

        Assert.IsEmpty(input.PriceAdjustment.Entries);
        Assert.IsEmpty(input.Promotion.LinePromotions);
        Assert.IsEmpty(input.Promotion.OrderPromotions);
        Assert.AreEqual(1, input.GracePeriod.Minutes);
        Assert.AreEqual(ShortfallPolicyForm.AllOrNothing, input.ShortfallPolicy.Policy);
        Assert.AreEqual(0m, input.ShippingFee.Fee);

        var entry = new PriceAdjustmentEntryForm { Target = null! };
        var line = new LinePromotionForm { Target = null! };
        Assert.AreEqual(TargetForm.All, entry.Target.Kind);
        Assert.AreEqual(TargetForm.All, line.Target.Kind);
    }

    [TestMethod]
    public void ResolveTargetsCopiesTheMatchingPickerIntoTheId()
    {
        var input = new DraftInput();
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm { Target = new TargetForm { Kind = TargetForm.Type, TypeId = 3, BrandId = 9, ItemId = 11 } });
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm { Target = new TargetForm { Kind = TargetForm.Brand, TypeId = 3, BrandId = 9, ItemId = 11 } });
        input.Promotion.LinePromotions.Add(new LinePromotionForm { Target = new TargetForm { Kind = TargetForm.Item, TypeId = 3, BrandId = 9, ItemId = 11 } });
        input.Promotion.LinePromotions.Add(new LinePromotionForm { Target = new TargetForm { Kind = TargetForm.All, TypeId = 3 } });
        input.Promotion.LinePromotions.Add(new LinePromotionForm { Target = new TargetForm { Kind = TargetForm.Type, Id = 7 } });

        input.ResolveTargets();

        Assert.AreEqual(3, input.PriceAdjustment.Entries[0].Target.Id);
        Assert.AreEqual(9, input.PriceAdjustment.Entries[1].Target.Id);
        Assert.AreEqual(11, input.Promotion.LinePromotions[0].Target.Id);
        Assert.IsNull(input.Promotion.LinePromotions[1].Target.Id);
        Assert.AreEqual(7, input.Promotion.LinePromotions[2].Target.Id);
    }

    [TestMethod]
    public void FromParametersFillsThePickersAndResolveKeepsTheId()
    {
        using var document = JsonDocument.Parse("""{"entries":[{"target":{"kind":"Brand","id":4},"mode":"PercentOff","value":5}]}""");

        var input = DraftInput.FromParameters(RuleForms.PriceAdjustment, document.RootElement, Now, null)!;
        input.ResolveTargets();

        var target = input.PriceAdjustment.Entries[0].Target;
        Assert.AreEqual(4, target.Id);
        Assert.AreEqual(4, target.BrandId);
        Assert.IsNull(target.TypeId);
        Assert.IsTrue(JsonElement.DeepEquals(document.RootElement, input.ToParameters(RuleForms.PriceAdjustment)));
    }

    [TestMethod]
    [DataRow("save", DraftActionKind.Save, null, -1)]
    [DataRow("add-entries", DraftActionKind.Add, DraftInput.Entries, -1)]
    [DataRow("add-linepromotions", DraftActionKind.Add, DraftInput.LinePromotions, -1)]
    [DataRow("add-orderpromotions", DraftActionKind.Add, DraftInput.OrderPromotions, -1)]
    [DataRow("remove-entries-0", DraftActionKind.Remove, DraftInput.Entries, 0)]
    [DataRow("remove-orderpromotions-12", DraftActionKind.Remove, DraftInput.OrderPromotions, 12)]
    public void ParsesTheSubmitButtonValue(string value, DraftActionKind kind, string? list, int index)
    {
        var action = DraftAction.Parse(value);

        Assert.IsNotNull(action);
        Assert.AreEqual(kind, action.Kind);
        Assert.AreEqual(list, action.List);
        Assert.AreEqual(index, action.Index);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("publish")]
    [DataRow("add-")]
    [DataRow("add-other")]
    [DataRow("remove-entries")]
    [DataRow("remove-entries--1")]
    [DataRow("remove-entries-x")]
    [DataRow("remove-other-1")]
    public void UnknownSubmitValuesAreIgnored(string value)
    {
        Assert.IsNull(DraftAction.Parse(value));
    }

    [TestMethod]
    public void MissingSubmitValueSavesBecauseThatIsAnImplicitSubmit()
    {
        Assert.AreEqual(DraftActionKind.Save, DraftAction.Parse(null)!.Kind);
    }

    [TestMethod]
    public void ButtonValuesRoundTripThroughParse()
    {
        var add = DraftAction.Parse(DraftAction.AddValue(DraftInput.LinePromotions))!;
        var remove = DraftAction.Parse(DraftAction.RemoveValue(DraftInput.Entries, 3))!;

        Assert.AreEqual(new DraftAction(DraftActionKind.Add, DraftInput.LinePromotions, -1), add);
        Assert.AreEqual(new DraftAction(DraftActionKind.Remove, DraftInput.Entries, 3), remove);
    }

    [TestMethod]
    public void FieldPathsResolveToTheModelAndMemberTheInputsBindTo()
    {
        var input = new DraftInput();
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm());
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm());

        var minutes = DraftFieldPath.Resolve(input, "GracePeriod.Minutes");
        var value = DraftFieldPath.Resolve(input, "PriceAdjustment.Entries[1].Value");
        var targetId = DraftFieldPath.Resolve(input, "PriceAdjustment.Entries[0].Target.Id");
        var list = DraftFieldPath.Resolve(input, "PriceAdjustment.Entries");
        var end = DraftFieldPath.Resolve(input, "EffectiveToUtc");

        Assert.AreSame(input.GracePeriod, minutes!.Value.Model);
        Assert.AreEqual("Minutes", minutes.Value.FieldName);
        Assert.AreSame(input.PriceAdjustment.Entries[1], value!.Value.Model);
        Assert.AreEqual("Value", value.Value.FieldName);
        Assert.AreSame(input.PriceAdjustment.Entries[0].Target, targetId!.Value.Model);
        Assert.AreSame(input.PriceAdjustment, list!.Value.Model);
        Assert.AreEqual("Entries", list.Value.FieldName);
        Assert.AreSame(input, end!.Value.Model);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Nope")]
    [DataRow("GracePeriod.Nope")]
    [DataRow("PriceAdjustment.Entries[5].Value")]
    [DataRow("PriceAdjustment.Entries[0]")]
    [DataRow("PriceAdjustment.Entries[-1].Value")]
    [DataRow("GracePeriod[0].Minutes")]
    [DataRow("GracePeriod.Minutes.Nope")]
    [DataRow("Promotion.LinePromotions[0].Label")]
    public void UnresolvableFieldPathsReturnNull(string path)
    {
        var input = new DraftInput();
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm());

        Assert.IsNull(DraftFieldPath.Resolve(input, path));
    }
}
