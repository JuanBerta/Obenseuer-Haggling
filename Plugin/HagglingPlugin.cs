using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

namespace HagglingMod
{
	public class HagglingSave : SkillSaveBase
	{
		protected override string SaveGuid { get { return "JuanBerta_Haggling_Save_001"; } }
		protected override string SaveTitle { get { return "Haggling"; } }
	}

	[BepInPlugin("JuanBerta.Obenseuer.Haggling", "Haggling", "1.0.0")]
	public class HagglingPlugin : BaseUnityPlugin
	{
		internal static CustomSkill Skill;

		internal static ConfigEntry<float> XpPerTrade;
		internal static ConfigEntry<float> DailyDiminishing;
		internal static ConfigEntry<float> DiscountPerLevel;
		internal static ConfigEntry<float> SellBonusPerLevel;
		internal static ConfigEntry<bool> PreventProfitLoop;
		internal static ConfigEntry<bool> DrunkHagglesWorse;
		internal static ConfigEntry<float> MaxDrunkXpBonus;
		internal static ConfigEntry<bool> SilverTongue;

		private void Awake()
		{
			Skill = new CustomSkill("Haggling");
			Skill.BindCommon(Config);
			Bind(Config);
			Skill.CardNote = CardNote;
			SkillHost.Init(this, Logger, Skill, "Haggling_Save", go => go.AddComponent<HagglingSave>());
			Harmony.CreateAndPatchAll(typeof(Hooks));
			Logger.LogInfo("Haggling loaded.");
		}

		private void Update() { SkillHost.Tick(); }

		private static void Bind(ConfigFile cfg)
		{
			XpPerTrade = cfg.Bind("Skill", "XpPerTrade", 12f, CustomSkill.D("Base XP for a trade with a trader (buying or selling). Bigger trades give more, up to 3 times this.", 90, new AcceptableValueRange<float>(1f, 200f)));
			DailyDiminishing = cfg.Bind("Skill", "DailyDiminishing", 0.04f, CustomSkill.D("Each trade today gives a bit less XP than the one before (0.04 = the 25th trade gives half). Resets each in-game day.", 60, new AcceptableValueRange<float>(0f, 1f)));

			DiscountPerLevel = cfg.Bind("Prices", "DiscountPerLevel", 0.012f, CustomSkill.D("How much cheaper traders sell to you per Haggling level (0.012 = 1.2% per level, 12% at level 10).", 100, new AcceptableValueRange<float>(0f, 0.05f)));
			SellBonusPerLevel = cfg.Bind("Prices", "SellBonusPerLevel", 0.012f, CustomSkill.D("How much more traders pay you per Haggling level (0.012 = 1.2% per level, 12% at level 10).", 90, new AcceptableValueRange<float>(0f, 0.05f)));
			PreventProfitLoop = cfg.Bind("Prices", "PreventProfitLoop", true, CustomSkill.D("A trader never pays you more for an item than you'd pay to buy it back from them, so buying and selling the same thing can't make money.", 80));

			DrunkHagglesWorse = cfg.Bind("State", "DrunkHagglesWorse", true, CustomSkill.D("Being drunk shrinks your Haggling price bonus (the Drinking skill makes this smaller).", 90));
			MaxDrunkXpBonus = cfg.Bind("State", "MaxDrunkXpBonus", 1f, CustomSkill.D("Extra XP when drunk, scaled by alcohol in blood (1 = up to +100%). Like the game's skills: worse odds, more learning.", 80, new AcceptableValueRange<float>(0f, 3f)));

			SilverTongue = cfg.Bind("Perk", "SilverTongue", true, CustomSkill.D("Level 10 perk: traders who don't normally buy stolen goods will buy them from you (at the stolen price). They still won't buy back what was stolen from them.", 100));
		}

		// ---- Price bonus ----

		internal static float Strength
		{
			get
			{
				if (!Skill.Enabled.Value) return 0f;
				float s = Skill.EffectiveLevel;
				if (DrunkHagglesWorse.Value) s *= 1f - Skill.Drunkenness;
				return Mathf.Max(0f, s);
			}
		}

		internal static float Discount { get { return Mathf.Clamp(DiscountPerLevel.Value * Strength, 0f, 0.5f); } }
		internal static float SellBonus { get { return Mathf.Clamp(SellBonusPerLevel.Value * Strength, 0f, 0.5f); } }

		private static string CardNote()
		{
			int d = Mathf.RoundToInt(Discount * 100f), s = Mathf.RoundToInt(SellBonus * 100f);
			return "-" + d + "% buy, +" + s + "% sell";
		}

		private static float RoundPrice(float value, Money.Currency currency)
		{
			if (currency == Money.Currency.RM) return Mathf.Max(0.01f, Mathf.Round(value * 100f) / 100f);
			return Mathf.Max(1f, Mathf.Round(value));
		}

		internal static float Adjust(Trade trade, ItemData itemData, int amount, Money.Currency currency, float price)
		{
			if (price <= 0f || itemData == null || itemData.slotController == null || itemData.slotController.slot == null) return price;
			bool playerSells = itemData.slotController.slot.playerSlot;
			if (playerSells)
			{
				float bonus = SellBonus;
				if (bonus <= 0f) return price;
				float result = price * (1f + bonus);
				if (PreventProfitLoop.Value)
				{
					// What the trader would charge you for the same stack, after your discount.
					float buyBack = Trade.GetItemPrice(itemData.item, itemData.owner, itemData.meta, currency, trade.basePurchasePrice, trade.stolenPriceFactor) * amount * (1f - Discount);
					result = Mathf.Min(result, Mathf.Max(price, buyBack * 0.95f));
				}
				return RoundPrice(result, currency);
			}
			float discount = Discount;
			if (discount <= 0f) return price;
			return RoundPrice(price * (1f - discount), currency);
		}

		// ---- XP ----

		internal static void OnTrade(Trade trade, ItemData itemData, int amount, bool bought)
		{
			if (!Skill.Enabled.Value || SkillHost.Save == null || itemData == null || itemData.item == null || amount <= 0) return;
			float value = Trade.GetItemPrice(itemData.item, -1, itemData.meta, Money.Currency.OC) * amount;   // base value in OC
			float size = Mathf.Clamp(Mathf.Sqrt(value) / 6f, 0.3f, 3f);
			float count = SkillHost.Save.AddDaily("trades", 1f);
			float fresh = 1f / (1f + DailyDiminishing.Value * (count - 1f));
			float drunk = 1f + MaxDrunkXpBonus.Value * Skill.Alcohol / 100f;
			string note = (bought ? "bought" : "sold") + (drunk > 1.01f ? ", drunk +" + Mathf.RoundToInt((drunk - 1f) * 100f) + "%" : "");
			Skill.AddXp(XpPerTrade.Value * size * fresh * drunk, note);
		}

		internal static class Hooks
		{
			[HarmonyPatch(typeof(Trade), "GetItemStackPrice")]
			[HarmonyPostfix]
			private static void GetItemStackPrice_Postfix(Trade __instance, ItemData itemData, int amount, Money.Currency currency, ref float __result)
			{
				try
				{
					__result = Adjust(__instance, itemData, amount, currency, __result);
				}
				catch (Exception e) { SkillHost.LogWarning("Haggling price: " + e.Message); }
			}

			[HarmonyPatch(typeof(Trade), "Buy", new Type[] { typeof(ItemData), typeof(int) })]
			[HarmonyPostfix]
			private static void Buy_Postfix(Trade __instance, ItemData itemData, int amount, bool __result)
			{
				if (!__result || __instance is TradeSpecial) return;   // TradeSpecial has its own hook
				try { OnTrade(__instance, itemData, amount, true); } catch (Exception e) { SkillHost.LogWarning("Haggling XP: " + e.Message); }
			}

			[HarmonyPatch(typeof(Trade), "Sell", new Type[] { typeof(ItemData), typeof(int) })]
			[HarmonyPostfix]
			private static void Sell_Postfix(Trade __instance, ItemData itemData, int amount, bool __result)
			{
				if (!__result || __instance is TradeSpecial) return;
				try { OnTrade(__instance, itemData, amount, false); } catch (Exception e) { SkillHost.LogWarning("Haggling XP: " + e.Message); }
			}

			[HarmonyPatch(typeof(TradeSpecial), "Buy", new Type[] { typeof(ItemData), typeof(int) })]
			[HarmonyPostfix]
			private static void SpecialBuy_Postfix(Trade __instance, ItemData itemData, int amount, bool __result)
			{
				if (!__result) return;
				try { OnTrade(__instance, itemData, amount, true); } catch (Exception e) { SkillHost.LogWarning("Haggling XP: " + e.Message); }
			}

			[HarmonyPatch(typeof(TradeSpecial), "Sell", new Type[] { typeof(ItemData), typeof(int) })]
			[HarmonyPostfix]
			private static void SpecialSell_Postfix(Trade __instance, ItemData itemData, int amount, bool __result)
			{
				if (!__result) return;
				try { OnTrade(__instance, itemData, amount, false); } catch (Exception e) { SkillHost.LogWarning("Haggling XP: " + e.Message); }
			}

			// Level 10 perk: traders who refuse stolen goods take them from you anyway.
			[HarmonyPatch(typeof(Trade), "IsStolen")]
			[HarmonyPostfix]
			private static void IsStolen_Postfix(Trade __instance, ItemData itemData, ref bool __result)
			{
				if (!__result || !SilverTongue.Value || Skill.Level < CustomSkill.MaxLevel || itemData == null || itemData.owner <= 0) return;
				try
				{
					int traderId = Traverse.Create(__instance).Field("owner").Field("ID").GetValue<int>();
					if (itemData.owner == traderId && traderId > 0) return;   // never their own stuff
					__result = false;
				}
				catch (Exception) { }
			}
		}
	}
}
