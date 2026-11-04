using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class ItemNameTests
{
    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string> { [7] = "Trail Runner", [9] = "  " };

    [TestMethod]
    public void NameIsShownForAKnownItemId()
        => Assert.AreEqual("Trail Runner", new TargetForm { Kind = TargetForm.Item, Id = 7 }.ItemName(Names));

    [TestMethod]
    public void UnknownIdBlankNameOrNoLookupShowNoName()
    {
        Assert.IsNull(new TargetForm { Kind = TargetForm.Item, Id = 8 }.ItemName(Names));
        Assert.IsNull(new TargetForm { Kind = TargetForm.Item, Id = 9 }.ItemName(Names));
        Assert.IsNull(new TargetForm { Kind = TargetForm.Item, Id = null }.ItemName(Names));
        Assert.IsNull(new TargetForm { Kind = TargetForm.Item, Id = 7 }.ItemName(null));
    }

    [TestMethod]
    [DataRow(TargetForm.All)]
    [DataRow(TargetForm.Brand)]
    [DataRow(TargetForm.Type)]
    public void OnlyItemTargetsShowAName(string kind)
        => Assert.IsNull(new TargetForm { Kind = kind, Id = 7 }.ItemName(Names));

    [TestMethod]
    public void ItemTargetIdsCoverBothListsOnceEach()
    {
        var input = new DraftInput();
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm { Target = new TargetForm { Kind = TargetForm.Item, Id = 7 } });
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm { Target = new TargetForm { Kind = TargetForm.Brand, Id = 3 } });
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm { Target = new TargetForm { Kind = TargetForm.Item, Id = null } });
        input.Promotion.LinePromotions.Add(new LinePromotionForm { Target = new TargetForm { Kind = TargetForm.Item, Id = 7 } });
        input.Promotion.LinePromotions.Add(new LinePromotionForm { Target = new TargetForm { Kind = TargetForm.Item, Id = 11 } });

        CollectionAssert.AreEquivalent(new[] { 7, 11 }, input.ItemTargetIds().ToArray());
    }

    [TestMethod]
    public void ResolvedItemIdIsTheOneLookedUp()
    {
        var input = new DraftInput();
        input.PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm { Target = new TargetForm { Kind = TargetForm.Item, ItemId = 5 } });
        input.ResolveTargets();

        CollectionAssert.AreEqual(new[] { 5 }, input.ItemTargetIds().ToArray());
    }
}
