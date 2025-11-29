using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VaultMod // <<<< CORRIGIDO
{
    /*************************************************************************
     * 1. CONFIGURAÇÃO (ModConfig e Enums de Acesso)
     *************************************************************************/

    public enum VaultAccess
    {
        Todos,
        ApenasHost
    }

    public class ModConfig
    {
        public SButton Hotkey { get; set; } = SButton.F5;
        public bool CaptureShippingBinIncome { get; set; } = true;
        public VaultAccess RequiredAccessLevel { get; set; } = VaultAccess.Todos;
        public bool EnableHistoryMenu { get; set; } = true;
        public bool EnableIncomeMenu { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public int HistoryLimit { get; set; } = 50;
    }

    /*************************************************************************
     * 2. MODELOS DE DADOS
     *************************************************************************/

    public enum TransactionType
    {
        Deposito,
        Saque,
        RendaAcumulada,
        RendaDistribuida
    }

    public class Transaction
    {
        public string Date { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public long Amount { get; set; }
        public TransactionType Type { get; set; }

        public Transaction() { }

        public string GetDescription()
        {
            string key = $"history.action.{Type.ToString().ToLower()}";
            return ModEntry.FormatTranslation(key, Amount).Replace("{Player}", PlayerName).Replace("{player}", PlayerName);
        }
    }

    public class VaultDataModel
    {
        public long Balance { get; set; } = 0;
        public long IncomePool { get; set; } = 0;
        public List<Transaction> History { get; set; } = new List<Transaction>();
    }

    /*************************************************************************
     * 3. INTERFACE (MENUS)
     *************************************************************************/

    // --- Menu Principal do Cofre ---
    public class VaultMainMenu : IClickableMenu
    {
        private TextBox inputAmount = null!;
        private ClickableComponent amountClickable = null!;
        private ClickableComponent btnDeposit = null!;
        private ClickableComponent btnWithdraw = null!;
        private ClickableComponent btnHistory = null!;
        private ClickableComponent btnIncome = null!;

        public VaultMainMenu()
            : base((int)Utility.getTopLeftPositionForCenteringOnScreen(600, 500).X, (int)Utility.getTopLeftPositionForCenteringOnScreen(600, 500).Y, 600, 500, true)
        {
            Texture2D tex = null!;
            try { tex = Game1.content.Load<Texture2D>("LooseSprites\\textBox"); } catch { tex = null!; }

            inputAmount = new TextBox(tex, null, Game1.smallFont, Color.Black)
            { X = xPositionOnScreen + 200, Y = yPositionOnScreen + 150, Width = 192, numbersOnly = true };
            amountClickable = new ClickableComponent(new Rectangle(inputAmount.X, inputAmount.Y, inputAmount.Width, 48), "");

            inputAmount.Selected = true;

            btnDeposit = new ClickableComponent(new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + 220, 200, 64), ModEntry.I18n.Get("button.deposit"));
            btnWithdraw = new ClickableComponent(new Rectangle(xPositionOnScreen + 350, yPositionOnScreen + 220, 200, 64), ModEntry.I18n.Get("button.withdraw"));
            btnHistory = new ClickableComponent(new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + 320, 200, 64), ModEntry.I18n.Get("tab.history"));
            btnIncome = new ClickableComponent(new Rectangle(xPositionOnScreen + 350, yPositionOnScreen + 320, 200, 64), ModEntry.I18n.Get("tab.income"));
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (amountClickable.containsPoint(x, y)) inputAmount.SelectMe();
            else inputAmount.Selected = false;

            if (ModEntry.Config.EnableHistoryMenu && btnHistory.containsPoint(x, y)) { Game1.activeClickableMenu = new HistoryMenu(); return; }
            if (ModEntry.Config.EnableIncomeMenu && btnIncome.containsPoint(x, y)) { Game1.activeClickableMenu = new IncomeMenu(); return; }

            if (long.TryParse(inputAmount.Text, out long amount) && amount > 0)
            {
                if (btnDeposit.containsPoint(x, y))
                {
                    if (Game1.player.Money >= amount)
                    {
                        long depositAmount = Math.Min(amount, int.MaxValue);
                        Game1.player.Money -= (int)depositAmount;
                        ModEntry.AddTransaction(TransactionType.Deposito, depositAmount, Game1.player.Name);
                        Game1.playSound("purchase");
                    }
                    else Game1.addHUDMessage(new HUDMessage(ModEntry.I18n.Get("message.low_wallet")));
                }
                else if (btnWithdraw.containsPoint(x, y))
                {
                    if (!ModEntry.HasRequiredPermission())
                    {
                        Game1.addHUDMessage(new HUDMessage(ModEntry.I18n.Get("message.permission_denied")));
                        return;
                    }

                    if (ModEntry.VaultData.Balance >= amount)
                    {
                        long withdrawAmount = amount;
                        if (amount > int.MaxValue)
                        {
                            withdrawAmount = int.MaxValue;
                            Game1.addHUDMessage(new HUDMessage(ModEntry.I18n.Get("message.withdraw_limit", new { Amount = int.MaxValue })));
                        }

                        Game1.player.Money += (int)withdrawAmount;
                        ModEntry.AddTransaction(TransactionType.Saque, withdrawAmount, Game1.player.Name);
                        Game1.playSound("coin");
                    }
                    else Game1.addHUDMessage(new HUDMessage(ModEntry.I18n.Get("message.low_vault")));
                }
            }
            base.receiveLeftClick(x, y, playSound);
        }

        public override void receiveKeyPress(Keys key) { if (key == Keys.Escape || key == Keys.C) exitThisMenu(); }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = ModEntry.I18n.Get("menu.title");
            SpriteText.drawStringHorizontallyCenteredAt(b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 20);

            string balance = ModEntry.FormatTranslation("menu.balance", ModEntry.VaultData.Balance);
            Vector2 textSize = Game1.dialogueFont.MeasureString(balance);
            Utility.drawTextWithShadow(b, balance, Game1.dialogueFont, new Vector2(xPositionOnScreen + width / 2 - textSize.X / 2f, yPositionOnScreen + 80), Color.Gold);

            inputAmount.Draw(b);
            DrawBtn(b, btnDeposit, Color.LightGreen);
            DrawBtn(b, btnWithdraw, Color.Salmon);
            if (ModEntry.Config.EnableHistoryMenu) DrawBtn(b, btnHistory, Color.LightGray);
            if (ModEntry.Config.EnableIncomeMenu) DrawBtn(b, btnIncome, Color.LightBlue);

            base.draw(b);
            drawMouse(b);
        }

        private void DrawBtn(SpriteBatch b, ClickableComponent btn, Color c)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, c, 4f, false);
            Utility.drawTextWithShadow(b, btn.name, Game1.smallFont, new Vector2(btn.bounds.X + 30, btn.bounds.Y + 20), Game1.textColor);
        }
    }

    // --- Menu de Extrato (History) ---
    public class HistoryMenu : IClickableMenu
    {
        public HistoryMenu() : base((int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).X, (int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).Y, 800, 600, true) { }

        public override void receiveKeyPress(Keys key) { if (key == Keys.Escape) Game1.activeClickableMenu = new VaultMainMenu(); }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = ModEntry.I18n.Get("history.title");
            SpriteText.drawStringHorizontallyCenteredAt(b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 30);

            int y = yPositionOnScreen + 100;
            if (ModEntry.VaultData.History.Count == 0)
                Utility.drawTextWithShadow(b, ModEntry.I18n.Get("history.no_transactions"), Game1.smallFont, new Vector2(xPositionOnScreen + 100, y), Game1.textColor);

            int limit = Math.Min(ModEntry.VaultData.History.Count, ModEntry.Config.HistoryLimit);
            for (int i = 0; i < limit; i++)
            {
                var item = ModEntry.VaultData.History[i];
                string text = $"{item.Date} - {item.GetDescription()}";
                Color c = (item.Type == TransactionType.Saque) ? Color.Red : Color.Green;
                Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(xPositionOnScreen + 50, y), c);
                y += 40;
                if (y > yPositionOnScreen + height - 50) break;
            }
            base.draw(b);
            drawMouse(b);
        }
    }

    // --- Menu de Renda (Income) ---
    public class IncomeMenu : IClickableMenu
    {
        private TextBox splitCountBox = null!;
        private ClickableComponent splitButton = null!;
        private ClickableComponent confirmDistributeButton = null!;
        private List<DistributionSlot> slots = new List<DistributionSlot>();
        private List<Farmer> availableTargets = new List<Farmer>();
        private int amountPerSlot = 0;

        public IncomeMenu() : base((int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).X, (int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).Y, 800, 600, true)
        {
            availableTargets = Game1.getOnlineFarmers().ToList();
            Texture2D tex = null!;
            try { tex = Game1.content.Load<Texture2D>("LooseSprites\\textBox"); } catch { tex = null!; }

            splitCountBox = new TextBox(tex, null, Game1.smallFont, Color.Black)
            {
                Width = 100,
                numbersOnly = true,
                Text = "2",
                X = xPositionOnScreen + width - 360,
                Y = yPositionOnScreen + 130
            };

            splitButton = new ClickableComponent(new Rectangle(splitCountBox.X + splitCountBox.Width + 12, splitCountBox.Y, 150, 48), ModEntry.I18n.Get("income.simulate"));
            confirmDistributeButton = new ClickableComponent(new Rectangle(xPositionOnScreen + width - 250, yPositionOnScreen + height - 80, 200, 64), ModEntry.I18n.Get("income.distribute"));
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (splitButton.containsPoint(x, y))
            {
                GenerateSlots();
                Game1.playSound("bigDeSelect");
            }
            foreach (var slot in slots)
            {
                if (slot.ButtonBounds.Contains(x, y)) { slot.CycleTarget(availableTargets); Game1.playSound("drumkit6"); }
            }
            if (confirmDistributeButton.containsPoint(x, y) && slots.Count > 0)
            {
                DistributeMoney();
                Game1.activeClickableMenu = new VaultMainMenu();
            }
            splitCountBox.Update();
            base.receiveLeftClick(x, y, playSound);
        }

        public override void receiveKeyPress(Keys key) { if (key == Keys.Escape) Game1.activeClickableMenu = new VaultMainMenu(); }

        private void GenerateSlots()
        {
            slots.Clear();
            if (int.TryParse(splitCountBox.Text, out int divisions) && divisions > 0)
            {
                long totalIncome = ModEntry.VaultData.IncomePool;
                if (totalIncome <= 0) { Game1.addHUDMessage(new HUDMessage(ModEntry.I18n.Get("income.no_funds"))); return; }

                amountPerSlot = (int)(totalIncome / divisions);
                for (int i = 0; i < divisions; i++) slots.Add(new DistributionSlot(i, xPositionOnScreen + 50, yPositionOnScreen + 200 + (i * 60)));
            }
        }

        private void DistributeMoney()
        {
            long totalUsed = 0;
            foreach (var slot in slots)
            {
                totalUsed += amountPerSlot;
                if (slot.SelectedTarget != null)
                {
                    slot.SelectedTarget.Money += amountPerSlot;
                    // Registra como distribuição no histórico (não chama AddTransaction para evitar alterar Balance)
                    ModEntry.VaultData.History.Insert(0, new Transaction
                    {
                        Date = Utility.getDateStringFor(Game1.Date.DayOfMonth, Game1.Date.SeasonIndex, Game1.Date.Year),
                        PlayerName = slot.SelectedTarget.Name,
                        Amount = amountPerSlot,
                        Type = TransactionType.RendaDistribuida
                    });
                }
                else
                {
                    // De volta pro cofre (atualiza o balance diretamente)
                    ModEntry.VaultData.Balance += amountPerSlot;
                    ModEntry.VaultData.History.Insert(0, new Transaction
                    {
                        Date = Utility.getDateStringFor(Game1.Date.DayOfMonth, Game1.Date.SeasonIndex, Game1.Date.Year),
                        PlayerName = ModEntry.I18n.Get("income.target.vault"),
                        Amount = amountPerSlot,
                        Type = TransactionType.RendaDistribuida
                    });
                }
            }
            ModEntry.VaultData.IncomePool -= totalUsed;
            ModEntry.SaveData();
            ModEntry.SyncData();
            Game1.playSound("reward");

            string msg = ModEntry.FormatTranslation("message.distributed", totalUsed);
            if (ModEntry.Config.ShowNotifications) Game1.addHUDMessage(new HUDMessage(msg));
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = ModEntry.I18n.Get("income.title");
            SpriteText.drawStringHorizontallyCenteredAt(b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 30);

            string incomeBalance = ModEntry.FormatTranslation("income.balance", ModEntry.VaultData.IncomePool);
            Utility.drawTextWithShadow(b, incomeBalance, Game1.dialogueFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 80), Color.LightGreen);

            Utility.drawTextWithShadow(b, ModEntry.I18n.Get("income.split_label"), Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 140), Game1.textColor);

            splitCountBox.Draw(b);

            // Botão Simular
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), splitButton.bounds.X, splitButton.bounds.Y, splitButton.bounds.Width, splitButton.bounds.Height, Color.White, 4f, false);
            Utility.drawTextWithShadow(b, splitButton.name, Game1.smallFont, new Vector2(splitButton.bounds.X + 20, splitButton.bounds.Y + 12), Game1.textColor);

            foreach (var slot in slots)
            {
                string slotLabel = ModEntry.I18n.Get("income.slot_label");
                string label = string.Format("{0} {1}: {2}g -> ", slotLabel, slot.Index + 1, amountPerSlot);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(slot.X, slot.Y + 10), Game1.textColor);

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), slot.ButtonBounds.X, slot.ButtonBounds.Y, slot.ButtonBounds.Width, slot.ButtonBounds.Height, Color.LightBlue, 4f, false);

                string targetName = slot.SelectedTarget == null ? ModEntry.I18n.Get("income.target.vault") : slot.SelectedTarget.Name;
                Utility.drawTextWithShadow(b, targetName, Game1.smallFont, new Vector2(slot.ButtonBounds.X + 15, slot.ButtonBounds.Y + 10), Color.Blue);
            }

            if (slots.Count > 0)
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), confirmDistributeButton.bounds.X, confirmDistributeButton.bounds.Y, confirmDistributeButton.bounds.Width, confirmDistributeButton.bounds.Height, Color.LightGreen, 4f, false);
                Utility.drawTextWithShadow(b, confirmDistributeButton.name, Game1.dialogueFont, new Vector2(confirmDistributeButton.bounds.X + 25, confirmDistributeButton.bounds.Y + 10), Color.White);
            }
            base.draw(b);
            drawMouse(b);
        }
    }

    public class DistributionSlot
    {
        public int Index;
        public int X, Y;
        public Rectangle ButtonBounds;
        public Farmer? SelectedTarget;

        public DistributionSlot(int index, int x, int y)
        {
            Index = index; X = x; Y = y;
            ButtonBounds = new Rectangle(x + 250, y, 300, 48);
            SelectedTarget = null;
        }

        public void CycleTarget(List<Farmer> players)
        {
            if (SelectedTarget == null)
            {
                if (players.Count > 0) SelectedTarget = players[0];
            }
            else
            {
                int currentIdx = players.IndexOf(SelectedTarget);
                if (currentIdx == players.Count - 1) SelectedTarget = null;
                else SelectedTarget = players[currentIdx + 1];
            }
        }
    }

    /*************************************************************************
     * 4. MOD ENTRY (LÓGICA PRINCIPAL)
     *************************************************************************/

    public class ModEntry : Mod
    {
        internal static IModHelper StaticHelper = null!;
        internal static string ModID = null!;
        internal static ModConfig Config = null!;
        internal static ITranslationHelper I18n = null!;
        internal static IMonitor Logger = null!;
        internal static VaultDataModel VaultData { get; private set; } = new VaultDataModel();

        public override void Entry(IModHelper helper)
        {
            StaticHelper = helper;
            Logger = this.Monitor;
            ModID = ModManifest.UniqueID;
            I18n = helper.Translation;
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
        }

        // --- Helpers ---

        public static string FormatTranslation(string key, long amount)
        {
            string template = I18n.Get(key);
            string amountN0 = amount.ToString("N0");
            string result = template.Replace("{Amount:N0}", amountN0)
                                    .Replace("{amount:N0}", amountN0)
                                    .Replace("{Amount}", amountN0)
                                    .Replace("{amount}", amountN0);

            if (!result.Contains("g") && (template.Contains("{Amount") || template.Contains("{amount")))
            {
                result = result.Trim() + "g";
            }
            return result;
        }

        public static bool HasRequiredPermission()
        {
            if (Config.RequiredAccessLevel == VaultAccess.Todos) return true;
            return Context.IsMainPlayer;
        }

        public static void AddTransaction(TransactionType type, long amount, string playerName)
        {
            if (!Context.IsWorldReady || (!Context.IsMainPlayer && Context.IsMultiplayer)) return;

            switch (type)
            {
                case TransactionType.Deposito:
                    VaultData.Balance += amount; break;
                case TransactionType.Saque:
                    VaultData.Balance -= amount; break;
            }

            VaultData.History.Insert(0, new Transaction
            {
                Date = Utility.getDateStringFor(Game1.Date.DayOfMonth, Game1.Date.SeasonIndex, Game1.Date.Year),
                PlayerName = playerName,
                Amount = amount,
                Type = type
            });

            if (VaultData.History.Count > Config.HistoryLimit)
                VaultData.History = VaultData.History.Take(Config.HistoryLimit).ToList();

            long notifyAmt = (type == TransactionType.Saque) ? -amount : amount;
            string msg = FormatTranslation($"message.{type.ToString().ToLower()}", notifyAmt);

            if (Config.ShowNotifications) Game1.addHUDMessage(new HUDMessage(msg));

            SaveData();
            SyncData();
        }

        // CORREÇÃO FINAL: Usando WriteSaveData para salvar por mundo
        public static void SaveData()
        {
            if (Context.IsMainPlayer) StaticHelper.Data.WriteSaveData(ModID, VaultData);
        }

        public static void SyncData()
        {
            if (!Context.IsMultiplayer) return;
            StaticHelper.Multiplayer.SendMessage(VaultData, "SyncData", modIDs: new[] { ModID });
        }

        // --- Events ---

        // CORREÇÃO FINAL: Usando ReadSaveData para carregar por mundo
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                // Carrega dados específicos do save, resolvendo o problema do histórico compartilhado.
                VaultData = StaticHelper.Data.ReadSaveData<VaultDataModel>(ModID) ?? new VaultDataModel();
                if (Context.IsMultiplayer) SyncData();
            }
            else
            {
                VaultData = new VaultDataModel();
            }
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (!Context.IsMainPlayer || !Config.CaptureShippingBinIncome) return;

            long totalSales = 0;
            var farm = Game1.getFarm();
            var shippingBin = farm.getShippingBin(Game1.player);

            Logger.Log($"[VaultMod] Processando fim do dia. Itens na caixa: {shippingBin.Count}", LogLevel.Debug);

            foreach (Item item in shippingBin)
            {
                if (item != null)
                {
                    int price = Utility.getSellToStorePriceOfItem(item);
                    totalSales += price;
                }
            }

            if (totalSales > 0)
            {
                Logger.Log($"[VaultMod] Vendas totais calculadas: {totalSales}g. Adicionando ao cofre.", LogLevel.Info);

                VaultData.IncomePool += totalSales;

                VaultData.History.Insert(0, new Transaction
                {
                    Date = Utility.getDateStringFor(Game1.Date.DayOfMonth, Game1.Date.SeasonIndex, Game1.Date.Year),
                    PlayerName = I18n.Get("history.player.farm_sales"),
                    Amount = totalSales,
                    Type = TransactionType.RendaAcumulada
                });

                string msg = FormatTranslation("message.rendaacumulada", totalSales);
                if (Config.ShowNotifications) Game1.addHUDMessage(new HUDMessage(msg));
            }

            shippingBin.Clear();

            SaveData();
            SyncData();
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null || Game1.player.UsingTool) return;
            if (e.Button == Config.Hotkey)
            {
                Game1.activeClickableMenu = new VaultMainMenu();
                StaticHelper.Input.Suppress(e.Button);
            }
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID == ModID && e.Type == "SyncData" && !Context.IsMainPlayer)
            {
                VaultData = e.ReadAs<VaultDataModel>();
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = StaticHelper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null) return;

            gmcm.Register(ModManifest, () => Config = new ModConfig(), () => StaticHelper.WriteConfig(Config));

            gmcm.AddSectionTitle(ModManifest, () => "Settings");
            gmcm.AddKeybind(ModManifest, () => Config.Hotkey, (val) => Config.Hotkey = val, () => I18n.Get("config.hotkey.name"), () => I18n.Get("config.hotkey.desc"));
            gmcm.AddBoolOption(ModManifest, () => Config.CaptureShippingBinIncome, (val) => Config.CaptureShippingBinIncome = val, () => I18n.Get("config.capture_bin.name"), () => I18n.Get("config.capture_bin.desc"));
            gmcm.AddBoolOption(ModManifest, () => Config.ShowNotifications, (val) => Config.ShowNotifications = val, () => I18n.Get("config.show_notifications.name"), () => I18n.Get("config.show_notifications.desc"));

            gmcm.AddSectionTitle(ModManifest, () => "Menu Features");
            gmcm.AddBoolOption(ModManifest, () => Config.EnableHistoryMenu, (val) => Config.EnableHistoryMenu = val, () => I18n.Get("config.enable_history.name"), () => I18n.Get("config.enable_history.desc"));
            gmcm.AddBoolOption(ModManifest, () => Config.EnableIncomeMenu, (val) => Config.EnableIncomeMenu = val, () => I18n.Get("config.enable_income.name"), () => I18n.Get("config.enable_income.desc"));

            gmcm.AddTextOption(ModManifest,
                () => Config.RequiredAccessLevel.ToString(),
                (val) => Config.RequiredAccessLevel = (VaultAccess)Enum.Parse(typeof(VaultAccess), val),
                () => I18n.Get("config.access_level.name"),
                () => I18n.Get("config.access_level.desc"),
                new string[] { VaultAccess.Todos.ToString(), VaultAccess.ApenasHost.ToString() }
            );
        }
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
    }
}