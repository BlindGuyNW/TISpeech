using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using MelonLoader;
using UnityEngine;
using PavonisInteractive.TerraInvicta;
using ModelShark;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for faction resources - uses the game's actual tooltip delegates.
    /// </summary>
    public class ResourceReader
    {
        /// <summary>
        /// Resource items that appear in the HUD, in order.
        /// </summary>
        public enum ResourceItem
        {
            Money,
            Influence,
            Operations,
            Boost,
            Research,
            MissionControl,
            ControlPointCap,
            Water,
            Volatiles,
            Metals,
            NobleMetals,
            Fissiles,
            Antimatter,
            Exotics
        }

        /// <summary>
        /// Get display name for a resource item.
        /// </summary>
        public static string GetResourceName(ResourceItem item)
        {
            switch (item)
            {
                case ResourceItem.Money: return "Money";
                case ResourceItem.Influence: return "Influence";
                case ResourceItem.Operations: return "Operations";
                case ResourceItem.Boost: return "Boost";
                case ResourceItem.Research: return "Research";
                case ResourceItem.MissionControl: return "Mission Control";
                case ResourceItem.ControlPointCap: return "Control Point Cap";
                case ResourceItem.Water: return "Water";
                case ResourceItem.Volatiles: return "Volatiles";
                case ResourceItem.Metals: return "Metals";
                case ResourceItem.NobleMetals: return "Noble Metals";
                case ResourceItem.Fissiles: return "Fissiles";
                case ResourceItem.Antimatter: return "Antimatter";
                case ResourceItem.Exotics: return "Exotics";
                default: return "Unknown";
            }
        }

        private TIFactionState GetFaction()
        {
            return GameControl.control?.activePlayer;
        }

        /// <summary>
        /// Read a one-line summary for navigation (current value + daily change).
        /// </summary>
        public string ReadSummary(ResourceItem item)
        {
            try
            {
                var faction = GetFaction();
                if (faction == null)
                    return $"{GetResourceName(item)}: No active faction";

                switch (item)
                {
                    case ResourceItem.Money:
                    case ResourceItem.Influence:
                    case ResourceItem.Operations:
                    case ResourceItem.Boost:
                    case ResourceItem.Research:
                        return GetPrimaryResourceSummary(faction, ToFactionResource(item));

                    case ResourceItem.MissionControl:
                        return GetMissionControlSummary(faction);

                    case ResourceItem.ControlPointCap:
                        return GetControlPointCapSummary(faction);

                    case ResourceItem.Water:
                    case ResourceItem.Volatiles:
                    case ResourceItem.Metals:
                    case ResourceItem.NobleMetals:
                    case ResourceItem.Fissiles:
                    case ResourceItem.Antimatter:
                    case ResourceItem.Exotics:
                        return GetSpaceResourceSummary(faction, ToFactionResource(item));

                    default:
                        return "Unknown resource";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading resource summary: {ex.Message}");
                return $"{GetResourceName(item)}: Error";
            }
        }

        /// <summary>
        /// Read detailed breakdown - gets the game's actual tooltip text.
        /// </summary>
        public string ReadDetail(ResourceItem item)
        {
            try
            {
                var faction = GetFaction();
                if (faction == null)
                    return "No active faction";

                // Try to get the tooltip text from the game's HUD
                string tooltipText = GetTooltipTextFromHUD(item);
                if (!string.IsNullOrEmpty(tooltipText))
                {
                    return TISpeechMod.CleanText(tooltipText);
                }

                // Fallback to our own generation if HUD not available
                return GetFallbackDetail(faction, item);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading resource detail: {ex.Message}");
                return $"Error reading {GetResourceName(item)}: {ex.Message}";
            }
        }

        /// <summary>
        /// Get the tooltip text directly from the game's GeneralControlsController.
        /// </summary>
        private string GetTooltipTextFromHUD(ResourceItem item)
        {
            try
            {
                var controller = UnityEngine.Object.FindObjectOfType<GeneralControlsController>();
                if (controller == null)
                    return null;

                TooltipTrigger trigger = GetTooltipTrigger(controller, item);
                if (trigger == null)
                    return null;

                return GetTooltipBodyText(trigger);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting tooltip from HUD: {ex.Message}");
                return null;
            }
        }

        private TooltipTrigger GetTooltipTrigger(GeneralControlsController controller, ResourceItem item)
        {
            switch (item)
            {
                case ResourceItem.Money: return controller.moneyTooltipTrigger;
                case ResourceItem.Influence: return controller.influenceTooltipTrigger;
                case ResourceItem.Operations: return controller.opsTooltipTrigger;
                case ResourceItem.Boost: return controller.boostTooltipTrigger;
                case ResourceItem.Research: return controller.researchTooltipTrigger;
                case ResourceItem.MissionControl: return controller.missionControlTooltipTrigger;
                case ResourceItem.ControlPointCap: return controller.controlPointMaintenanceTrigger;
                case ResourceItem.Water: return controller.waterTooltipTrigger;
                case ResourceItem.Volatiles: return controller.volatilesTooltipTrigger;
                case ResourceItem.Metals: return controller.baseMetalsTooltipTrigger;
                case ResourceItem.NobleMetals: return controller.nobleMetalsTooltipTrigger;
                case ResourceItem.Fissiles: return controller.fissilesTooltipTrigger;
                case ResourceItem.Antimatter: return controller.antimatterTooltipTrigger;
                case ResourceItem.Exotics: return controller.exoticsTooltipTrigger;
                default: return null;
            }
        }

        private string GetTooltipBodyText(TooltipTrigger trigger)
        {
            if (trigger == null || trigger.parameterizedTextFields == null)
                return null;

            // Find the "BodyText" field and invoke its delegate
            foreach (var field in trigger.parameterizedTextFields)
            {
                if (field.name == "BodyText" && field.del != null)
                {
                    return field.del();
                }
            }

            return null;
        }

        private FactionResource ToFactionResource(ResourceItem item)
        {
            switch (item)
            {
                case ResourceItem.Money: return FactionResource.Money;
                case ResourceItem.Influence: return FactionResource.Influence;
                case ResourceItem.Operations: return FactionResource.Operations;
                case ResourceItem.Boost: return FactionResource.Boost;
                case ResourceItem.Research: return FactionResource.Research;
                case ResourceItem.MissionControl: return FactionResource.MissionControl;
                case ResourceItem.Water: return FactionResource.Water;
                case ResourceItem.Volatiles: return FactionResource.Volatiles;
                case ResourceItem.Metals: return FactionResource.Metals;
                case ResourceItem.NobleMetals: return FactionResource.NobleMetals;
                case ResourceItem.Fissiles: return FactionResource.Fissiles;
                case ResourceItem.Antimatter: return FactionResource.Antimatter;
                case ResourceItem.Exotics: return FactionResource.Exotics;
                default: return FactionResource.None;
            }
        }

        #region Summary Methods

        private string GetPrimaryResourceSummary(TIFactionState faction, FactionResource resource)
        {
            float current = faction.GetCurrentResourceAmount(resource);
            float daily = faction.GetDailyIncome(resource);
            string name = GetResourceName(ToResourceItem(resource));

            if (daily == 0)
                return $"{name}: {current:N0}, no daily change";
            else
                return $"{name}: {current:N0}, {daily:+0.00;-0.00} per day";
        }

        private string GetMissionControlSummary(TIFactionState faction)
        {
            int income = faction.MissionControlIncome;
            int usage = faction.GetMissionControlUsage();
            int available = faction.AvailableMissionControl;
            int shortage = faction.MissionControlShortage;

            if (shortage > 0)
                return $"Mission Control: {usage}/{income}, shortage {shortage}";
            else
                return $"Mission Control: {usage}/{income}, {available} available";
        }

        private string GetControlPointCapSummary(TIFactionState faction)
        {
            float usage = faction.GetBaselineControlPointMaintenanceCost();
            float cap = faction.GetControlPointMaintenanceFreebieCap();
            float annual = faction.GetAnnualControlPointMaintenanceCost();

            if (annual > 0)
            {
                float daily = annual / 365.2422f;
                return $"CP Cap: {usage:N0}/{cap:N0}, costing {daily:N2} influence per day";
            }
            else
                return $"CP Cap: {usage:N0}/{cap:N0}, within limit";
        }

        private string GetSpaceResourceSummary(TIFactionState faction, FactionResource resource)
        {
            float current = faction.GetCurrentResourceAmount(resource);
            float daily = faction.GetDailyIncome(resource);
            string name = GetResourceName(ToResourceItem(resource));

            if (current < 0.01f && daily == 0)
                return $"{name}: None";
            else if (daily == 0)
                return $"{name}: {FormatSpaceResource(current)}";
            else
                return $"{name}: {FormatSpaceResource(current)}, {daily:+0.000;-0.000} per day";
        }

        private string FormatSpaceResource(float value)
        {
            if (value >= 1000)
                return $"{value:N0}";
            else if (value >= 1)
                return $"{value:N1}";
            else if (value >= 0.001f)
                return $"{value:N3}";
            else
                return $"{value:G3}";
        }

        #endregion

        #region Fallback Detail Methods (if HUD not available)

        private string GetFallbackDetail(TIFactionState faction, ResourceItem item)
        {
            var sb = new StringBuilder();
            string name = GetResourceName(item);
            sb.AppendLine($"{name} Details");
            sb.AppendLine();

            switch (item)
            {
                case ResourceItem.Money:
                case ResourceItem.Influence:
                case ResourceItem.Operations:
                case ResourceItem.Boost:
                case ResourceItem.Research:
                    var resource = ToFactionResource(item);
                    float current = faction.GetCurrentResourceAmount(resource);
                    float daily = faction.GetDailyIncome(resource);
                    float monthly = faction.GetMonthlyIncome(resource);
                    float yearly = faction.GetYearlyIncome(resource);

                    sb.AppendLine($"Stockpile: {current:N0}");
                    sb.AppendLine($"Daily: {daily:+0.00;-0.00}");
                    sb.AppendLine($"Monthly: {monthly:+0.0;-0.0}");
                    sb.AppendLine($"Annual: {yearly:+0;-0}");
                    break;

                case ResourceItem.MissionControl:
                    sb.AppendLine($"Income: {faction.MissionControlIncome}");
                    sb.AppendLine($"Usage: {faction.GetMissionControlUsage()}");
                    sb.AppendLine($"Available: {faction.AvailableMissionControl}");
                    break;

                case ResourceItem.ControlPointCap:
                    float usage = faction.GetBaselineControlPointMaintenanceCost();
                    float cap = faction.GetControlPointMaintenanceFreebieCap();
                    sb.AppendLine($"Usage: {usage:N1} / {cap:N0}");
                    float annual = faction.GetAnnualControlPointMaintenanceCost();
                    if (annual > 0)
                        sb.AppendLine($"Influence cost: {annual / 365.2422f:N2} per day");
                    break;

                default:
                    var spaceRes = ToFactionResource(item);
                    float spaceCurrent = faction.GetCurrentResourceAmount(spaceRes);
                    float spaceDaily = faction.GetDailyIncome(spaceRes);
                    sb.AppendLine($"Stockpile: {FormatSpaceResource(spaceCurrent)}");
                    sb.AppendLine($"Daily: {spaceDaily:+0.000;-0.000}");
                    break;
            }

            return sb.ToString();
        }

        private ResourceItem ToResourceItem(FactionResource resource)
        {
            switch (resource)
            {
                case FactionResource.Money: return ResourceItem.Money;
                case FactionResource.Influence: return ResourceItem.Influence;
                case FactionResource.Operations: return ResourceItem.Operations;
                case FactionResource.Boost: return ResourceItem.Boost;
                case FactionResource.Research: return ResourceItem.Research;
                case FactionResource.MissionControl: return ResourceItem.MissionControl;
                case FactionResource.Water: return ResourceItem.Water;
                case FactionResource.Volatiles: return ResourceItem.Volatiles;
                case FactionResource.Metals: return ResourceItem.Metals;
                case FactionResource.NobleMetals: return ResourceItem.NobleMetals;
                case FactionResource.Fissiles: return ResourceItem.Fissiles;
                case FactionResource.Antimatter: return ResourceItem.Antimatter;
                case FactionResource.Exotics: return ResourceItem.Exotics;
                default: return ResourceItem.Money;
            }
        }

        #endregion
    }
}
