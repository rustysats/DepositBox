using System;
using System.Collections.Generic;
using System.Globalization; // For CultureInfo
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("DepositBox", "rustysats", "0.2.0")]
    [Description("Drop box that registers drops for admin while removing items from the game.")]
    internal class DepositBox : RustPlugin
    {
        private static DepositBox instance;
        // Configuration variables
        private int DepositItemID;
        private ulong DepositBoxSkinID;
        // Permission constants
        private const string permPlace = "depositbox.place";
        private const string permCheck = "depositbox.check";
        private const string permAdminCheck = "depositbox.admincheck";
        private DepositLog depositLog;
        private Dictionary<Item, BasePlayer> depositTrack = new Dictionary<Item, BasePlayer>(); // Track deposits

        #region Oxide Hooks
        void Init()
        {
            instance = this;
            LoadConfiguration();
            LoadDepositLog();
            permission.RegisterPermission(permPlace, this);
            permission.RegisterPermission(permCheck, this);
            permission.RegisterPermission(permAdminCheck, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            Config["DepositItemID"] = -1779183908;    // Default Item ID for deposits (paper)
            Config["DepositBoxSkinID"] = 1641384897;  // Default skin ID for the deposit box
            SaveConfig();
        }

        void OnServerInitialized(bool initial)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is StorageContainer storageContainer)
                {
                    OnEntitySpawned(storageContainer);
                }
            }
        }

        void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is StorageContainer storageContainer && storageContainer.TryGetComponent(out DepositBoxRestriction restriction))
                {
                    restriction.Destroy();
                }
            }
            instance = null;
        }

        void OnEntitySpawned(StorageContainer container)
        {
            if (container == null || container.skinID != DepositBoxSkinID) return;  // Early return for non-matching containers
            if (!container.TryGetComponent(out DepositBoxRestriction mono))
            {
                mono = container.gameObject.AddComponent<DepositBoxRestriction>();
                mono.container = container.inventory;  // Assign inventory upon component addition
                mono.InitDepositBox();
            }
        }
        #endregion

        #region Commands
        [ChatCommand("depositbox")]
        private void GiveDepositBox(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permPlace))
            {
                player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            player.inventory.containerMain.GiveItem(ItemManager.CreateByItemID(833533164, 1, DepositBoxSkinID));
            player.ChatMessage(lang.GetMessage("BoxGiven", this, player.UserIDString));
        }

        [ChatCommand("checkdeposits")]
        private void CheckDepositsCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permCheck))
            {
                player.ChatMessage(lang.GetMessage("NoCheckPermission", this, player.UserIDString));
                return;
            }

            if (depositLog == null || depositLog.Deposits.Count == 0)
            {
                player.ChatMessage(lang.GetMessage("NoDepositData", this, player.UserIDString));
                return;
            }

            // Group the deposits by SteamID and calculate the total amount deposited for each player
            var depositSummary = depositLog.Deposits
                .GroupBy(entry => entry.SteamId)
                .Select(group => new
                {
                    SteamId = group.Key,
                    TotalAmount = group.Sum(entry => entry.AmountDeposited)
                })
                .ToList();

            // Calculate the total amount deposited by all players
            int totalDeposited = depositSummary.Sum(summary => summary.TotalAmount);

            // Find the current player's total deposits
            var playerSummary = depositSummary.FirstOrDefault(summary => summary.SteamId == player.UserIDString);

            if (playerSummary != null)
            {
                // Calculate the percentage of total deposits for the current player
                double percentageOfTotal = ((double)playerSummary.TotalAmount / totalDeposited) * 100;
                player.ChatMessage(lang.GetMessage("PlayerDepositSummary", this, player.UserIDString)
                    .Replace("{amount}", playerSummary.TotalAmount.ToString(CultureInfo.InvariantCulture))
                    .Replace("{percentage}", percentageOfTotal.ToString("F2", CultureInfo.InvariantCulture)));
            }
            else
            {
                player.ChatMessage(lang.GetMessage("NoPlayerDeposits", this, player.UserIDString));
            }

            // Admin view if player has both permissions
            if (permission.UserHasPermission(player.UserIDString, permAdminCheck))
            {
                player.ChatMessage(lang.GetMessage("DepositTotals", this, player.UserIDString));
                foreach (var summary in depositSummary)
                {
                    double percentage = ((double)summary.TotalAmount / totalDeposited) * 100;
                    player.ChatMessage(lang.GetMessage("DepositEntrySummary", this, player.UserIDString)
                        .Replace("{steamid}", summary.SteamId)
                        .Replace("{amount}", summary.TotalAmount.ToString(CultureInfo.InvariantCulture))
                        .Replace("{percentage}", percentage.ToString("F2", CultureInfo.InvariantCulture)));
                }
            }
        }
        #endregion

        #region DepositBoxRestriction Class
        public class DepositBoxRestriction : FacepunchBehaviour
        {
            public ItemContainer container;
            public void InitDepositBox()
            {
                container.canAcceptItem += CanAcceptItem;
                container.onItemAddedRemoved += OnItemAddedRemoved;
            }
            private bool CanAcceptItem(Item item, int targetPos)
            {
                // Only allow the configured deposit item to be deposited
                if (item == null || item.info == null || item.info.itemid != DepositBox.instance.DepositItemID)
                {
                    return false;
                }
                if (item.GetOwnerPlayer() is BasePlayer player)
                {
                    DepositBox.instance.TrackDeposit(item, player); // Track the item with player reference
                }
                return true;
            }
            private void OnItemAddedRemoved(Item item, bool added)
            {
                // Early exit if item isn't added or isn't the correct deposit item
                if (!added || item.info.itemid != DepositBox.instance.DepositItemID) return;
                // Try to get the player who deposited the item
                if (DepositBox.instance.depositTrack.TryGetValue(item, out BasePlayer player))
                {
                    DepositBox.instance.LogDeposit(player, item.amount); // Log the deposit first
                    DepositBox.instance.depositTrack.Remove(item); // Remove from tracking
                    // Now remove the deposited item from the box, after logging is complete
                    item.Remove();
                }
            }
            public void Destroy()
            {
                container.canAcceptItem -= CanAcceptItem;
                container.onItemAddedRemoved -= OnItemAddedRemoved;
                Destroy(this);
            }
        }
        #endregion

        #region Logging
        private class DepositLog
        {
            [JsonProperty("deposits")]
            public List<DepositEntry> Deposits { get; set; } = new List<DepositEntry>();
        }

        private class DepositEntry
        {
            [JsonProperty("steamid")]
            public string SteamId { get; set; }
            [JsonProperty("timestamp")]
            public string Timestamp { get; set; }
            [JsonProperty("amount_deposited")]
            public int AmountDeposited { get; set; }
        }

        public void LogDeposit(BasePlayer player, int amount)
        {
            // Record this deposit
            depositLog.Deposits.Add(new DepositEntry
            {
                SteamId = player.UserIDString,
                Timestamp = DateTime.UtcNow.ToString("o"),
                AmountDeposited = amount
            });
            SaveDepositLog(); // Save the log after recording the deposit
        
            // Calculate the player's total deposits
            int playerTotalDeposits = depositLog.Deposits
                .Where(entry => entry.SteamId == player.UserIDString)
                .Sum(entry => entry.AmountDeposited);
        
            // Calculate the total deposits of all players
            int totalDepositedByAllPlayers = depositLog.Deposits.Sum(entry => entry.AmountDeposited);
        
            // Calculate the player's percentage of all deposits
            double playerPercentageOfTotal = ((double)playerTotalDeposits / totalDepositedByAllPlayers) * 100;
        
            // Send the updated deposit message to the player
            player.ChatMessage(lang.GetMessage("DepositRecorded", this, player.UserIDString)
                .Replace("{amount}", amount.ToString(CultureInfo.InvariantCulture))
                .Replace("{total_amount}", playerTotalDeposits.ToString(CultureInfo.InvariantCulture))
                .Replace("{percentage}", playerPercentageOfTotal.ToString("F2", CultureInfo.InvariantCulture)));
        }

        public void TrackDeposit(Item item, BasePlayer player)
        {
            if (item != null && player != null)
            {
                depositTrack[item] = player; // Track the item with its owner
            }
        }

        private void LoadDepositLog()
        {
            depositLog = Interface.Oxide.DataFileSystem.ReadObject<DepositLog>("DepositBoxLog") ?? new DepositLog();
        }

        private void SaveDepositLog()
        {
            Interface.Oxide.DataFileSystem.WriteObject("DepositBoxLog", depositLog);
        }
        #endregion

        #region Configuration
        private void LoadConfiguration()
        {
            DepositItemID = Convert.ToInt32(Config["DepositItemID"], CultureInfo.InvariantCulture); // Specified CultureInfo
            DepositBoxSkinID = Convert.ToUInt64(Config["DepositBoxSkinID"], CultureInfo.InvariantCulture); // Specified CultureInfo
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to place this box.",
                ["BoxGiven"] = "You have received a Deposit Box.",
                ["DepositRecorded"] = "Your deposit of {amount} has been recorded. You have deposited a total of {total_amount} items, which is {percentage}% of all deposits.",
                ["PlacedNoPerm"] = "You have placed a deposit box but lack permission to place it.",
                ["NoCheckPermission"] = "You do not have permission to check deposits.",
                ["NoDepositData"] = "No deposit data found.",
                ["PlayerDepositSummary"] = "You have deposited a total of {amount} items, which is {percentage}% of all deposits.",
                ["NoPlayerDeposits"] = "You have not made any deposits.",
                ["DepositTotals"] = "Deposit Totals:",
                ["DepositEntrySummary"] = "SteamID: {steamid}, Total Deposited: {amount}, {percentage}% of total."
            }, this);
        }
        #endregion
    }
}
