using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities; // Adicionado para KeybindList
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using Newtonsoft.Json;

namespace VaultMod // Namespace para o Cofre
{
    // =========================================================
    // CLASSE DE CONFIGURAÇÃO (Tecla C como padrão)
    // =========================================================
    public class ModConfig
    {
        // Tecla padrão para abrir o menu: C
        public KeybindList OpenVaultKey { get; set; } = new(SButton.C);
    }

    // =========================================================
    // CLASSE PRINCIPAL (ModEntry)
    // =========================================================
    public class ModEntry : Mod
    {
        private const string DataKey = "MyVaultMod.Data";
        private const string VaultMessageChannel = "VaultMod.VaultComms";

        internal static VaultDataModel VaultData = new VaultDataModel();
        internal static IModHelper? StaticHelper;
        internal static string ModID = "";

        private ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            StaticHelper = helper;
            ModID = this.ModManifest.UniqueID;
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.Multiplayer.ModMessageReceived += OnMessageReceived;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (Game1.getFarm().modData.TryGetValue(DataKey, out string json))
            {
                try
                {
                    VaultData = JsonConvert.DeserializeObject<VaultDataModel>(json) ?? new VaultDataModel();
                }
                catch
                {
                    VaultData = new VaultDataModel();
                }
            }
            else
            {
                VaultData = new VaultDataModel();
            }
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (!Context.IsMainPlayer) return;

            var shippingBin = Game1.getFarm().getShippingBin(Game1.player);
            long dailyIncome = 0;

            foreach (Item item in shippingBin)
            {
                if (item != null)
                {
                    dailyIncome += Utility.getSellToStorePriceOfItem(item);
                }
            }

            if (dailyIncome > 0)
            {
                VaultData.IncomePool += dailyIncome;
                AddTransaction("Fazenda", "Gerou Renda", (int)dailyIncome);
                shippingBin.Clear();
                SaveAndBroadcast();
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null) return;

            // MUDANÇA: Abre o menu do Cofre com a tecla configurada (padrão C)
            if (Config.OpenVaultKey.JustPressed())
            {
                Helper.Input.SuppressActiveKeybinds(Config.OpenVaultKey);
                Game1.activeClickableMenu = new VaultMainMenu();
            }
        }

        private void OnMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID == ModID && e.Type == VaultMessageChannel)
            {
                var msg = e.ReadAs<NetworkMessage>();
                if (msg?.Data != null)
                {
                    VaultData = msg.Data;
                }
            }
        }

        public static void AddTransaction(string who, string action, int amount)
        {
            if (action == "Depositou") VaultData.Balance += amount;
            else if (action == "Retirou") VaultData.Balance -= amount;

            var trans = new Transaction
            {
                Date = $"Dia {Game1.dayOfMonth}, {Game1.currentSeason}",
                Who = who,
                Action = action,
                Amount = amount
            };

            VaultData.History.Insert(0, trans);
            if (VaultData.History.Count > 50) VaultData.History.RemoveAt(VaultData.History.Count - 1);

            SaveAndBroadcast();
        }

        public static void SaveAndBroadcast()
        {
            if (StaticHelper == null) return;

            string json = JsonConvert.SerializeObject(VaultData);

            if (Context.IsMainPlayer)
            {
                Game1.getFarm().modData[DataKey] = json;
            }

            StaticHelper.Multiplayer.SendMessage(new NetworkMessage { Data = VaultData }, VaultMessageChannel, modIDs: new[] { ModID });
        }
    }

    // =========================================================
    // CLASSES DE DADOS
    // =========================================================
    public class VaultDataModel
    {
        public long Balance { get; set; } = 0;
        public long IncomePool { get; set; } = 0;
        public List<Transaction> History { get; set; } = new List<Transaction>();
    }

    public class Transaction
    {
        public string Date { get; set; } = "";
        public string Who { get; set; } = "";
        public string Action { get; set; } = "";
        public int Amount { get; set; } = 0;
    }

    public class NetworkMessage
    {
        public VaultDataModel? Data { get; set; }
    }

    // =========================================================
    // MENUS (INTERFACE GRÁFICA) - CENTRALIZADOS
    // =========================================================

    // --- Menu Principal do Cofre ---
    public class VaultMainMenu : IClickableMenu
    {
        private TextBox inputAmount;
        private ClickableComponent amountClickable;
        private ClickableComponent btnDeposit, btnWithdraw, btnHistory, btnIncome;

        public VaultMainMenu() : base((int)Utility.getTopLeftPositionForCenteringOnScreen(600, 500).X, (int)Utility.getTopLeftPositionForCenteringOnScreen(600, 500).Y, 600, 500, true)
        {
            inputAmount = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Color.Black)
            { X = xPositionOnScreen + 200, Y = yPositionOnScreen + 150, Width = 192, numbersOnly = true };
            amountClickable = new ClickableComponent(new Rectangle(inputAmount.X, inputAmount.Y, inputAmount.Width, 48), "");

            inputAmount.Selected = true;

            btnDeposit = new ClickableComponent(new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + 220, 200, 64), "Depositar");
            btnWithdraw = new ClickableComponent(new Rectangle(xPositionOnScreen + 350, yPositionOnScreen + 220, 200, 64), "Sacar");
            btnHistory = new ClickableComponent(new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + 320, 200, 64), "Extrato");
            btnIncome = new ClickableComponent(new Rectangle(xPositionOnScreen + 350, yPositionOnScreen + 320, 200, 64), "Renda");
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (amountClickable.containsPoint(x, y)) inputAmount.SelectMe();
            else inputAmount.Selected = false;

            if (btnHistory.containsPoint(x, y)) { Game1.activeClickableMenu = new HistoryMenu(); return; }
            if (btnIncome.containsPoint(x, y)) { Game1.activeClickableMenu = new IncomeMenu(); return; }

            if (int.TryParse(inputAmount.Text, out int amount) && amount > 0)
            {
                if (btnDeposit.containsPoint(x, y))
                {
                    if (Game1.player.Money >= amount)
                    {
                        Game1.player.Money -= amount;
                        ModEntry.AddTransaction(Game1.player.Name, "Depositou", amount);
                        Game1.playSound("purchase");
                    }
                    else Game1.addHUDMessage(new HUDMessage("Dinheiro insuficiente na carteira!", 3));
                }
                else if (btnWithdraw.containsPoint(x, y))
                {
                    if (ModEntry.VaultData.Balance >= amount)
                    {
                        Game1.player.Money += amount;
                        ModEntry.AddTransaction(Game1.player.Name, "Retirou", amount);
                        Game1.playSound("coin");
                    }
                    else Game1.addHUDMessage(new HUDMessage("Saldo insuficiente no cofre!", 3));
                }
            }
        }

        // MUDANÇA: Agora o menu fecha com ESC ou C
        public override void receiveKeyPress(Keys key) { if (key == Keys.Escape || key == Keys.C) exitThisMenu(); }

        // No VaultMainMenu.draw
        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            SpriteText.drawStringHorizontallyCenteredAt(b, "COFRE CENTRAL", xPositionOnScreen + width / 2, yPositionOnScreen + 20);

            // CORREÇÃO: Usando Utility.drawTextWithShadow com centralização manual
            string balanceText = $"Saldo: {ModEntry.VaultData.Balance}g";
            Vector2 textSize = Game1.dialogueFont.MeasureString(balanceText);

            Utility.drawTextWithShadow(b,
                balanceText,
                Game1.dialogueFont,
                new Vector2(xPositionOnScreen + width / 2 - textSize.X / 2f, yPositionOnScreen + 80),
                Color.Gold);

            inputAmount.Draw(b);
            DrawBtn(b, btnDeposit, Color.LightGreen);
            DrawBtn(b, btnWithdraw, Color.Salmon);
            DrawBtn(b, btnHistory, Color.LightGray);
            DrawBtn(b, btnIncome, Color.LightBlue);
            drawMouse(b);
        }

        private void DrawBtn(SpriteBatch b, ClickableComponent btn, Color c)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, c, 4f, false);
            Utility.drawTextWithShadow(b, btn.name, Game1.smallFont, new Vector2(btn.bounds.X + 30, btn.bounds.Y + 20), Game1.textColor);
        }
    }

    // --- Menu de Extrato (Cofre) ---
    public class HistoryMenu : IClickableMenu
    {
        public HistoryMenu() : base((int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).X, (int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).Y, 800, 600, true) { }

        public override void receiveKeyPress(Keys key) { if (key == Keys.Escape) Game1.activeClickableMenu = new VaultMainMenu(); }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
            SpriteText.drawStringHorizontallyCenteredAt(b, "Extrato Bancario", xPositionOnScreen + width / 2, yPositionOnScreen + 30);

            int y = yPositionOnScreen + 100;
            if (ModEntry.VaultData.History.Count == 0)
                Utility.drawTextWithShadow(b, "Nenhuma transacao recente.", Game1.smallFont, new Vector2(xPositionOnScreen + 100, y), Game1.textColor);

            foreach (var item in ModEntry.VaultData.History)
            {
                string text = $"{item.Date} - {item.Who}: {item.Action} {item.Amount}g";
                Color c = item.Action == "Depositou" ? Color.Green : (item.Action == "Retirou" ? Color.Red : Color.Blue);
                Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(xPositionOnScreen + 50, y), c);
                y += 40;
                if (y > yPositionOnScreen + height - 50) break;
            }
            drawMouse(b);
        }
    }

    // --- Menu de Renda (Cofre) ---
    public class IncomeMenu : IClickableMenu
    {
        private TextBox splitCountBox;
        private ClickableComponent splitButton, confirmDistributeButton;
        private List<DistributionSlot> slots = new List<DistributionSlot>();
        private List<Farmer> availableTargets;
        private int amountPerSlot = 0;

        public IncomeMenu() : base((int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).X, (int)Utility.getTopLeftPositionForCenteringOnScreen(800, 600).Y, 800, 600, true)
        {
            availableTargets = Game1.getOnlineFarmers().ToList();
            splitCountBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Color.Black)
            { X = xPositionOnScreen + 100, Y = yPositionOnScreen + 130, Width = 100, numbersOnly = true, Text = "2" };

            splitButton = new ClickableComponent(new Rectangle(xPositionOnScreen + 220, yPositionOnScreen + 130, 150, 48), "Simular");
            confirmDistributeButton = new ClickableComponent(new Rectangle(xPositionOnScreen + width - 250, yPositionOnScreen + height - 80, 200, 64), "Distribuir");
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
        }

        public override void receiveKeyPress(Keys key) { if (key == Keys.Escape) Game1.activeClickableMenu = new VaultMainMenu(); }

        private void GenerateSlots()
        {
            slots.Clear();
            if (int.TryParse(splitCountBox.Text, out int divisions) && divisions > 0)
            {
                long totalIncome = ModEntry.VaultData.IncomePool;
                if (totalIncome <= 0) { Game1.addHUDMessage(new HUDMessage("Sem renda acumulada!", 3)); return; }

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
                    ModEntry.AddTransaction("Renda", $"Pagou {slot.SelectedTarget.Name}", amountPerSlot);
                    slot.SelectedTarget.Money += amountPerSlot;
                }
                else
                {
                    ModEntry.AddTransaction("Renda", "Moveu para o Cofre", amountPerSlot);
                    ModEntry.VaultData.Balance += amountPerSlot;
                }
            }
            ModEntry.VaultData.IncomePool -= totalUsed;
            ModEntry.SaveAndBroadcast();
            Game1.playSound("reward");
            Game1.addHUDMessage(new HUDMessage("Renda distribuida!", 2));
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
            SpriteText.drawStringHorizontallyCenteredAt(b, "Gestao de Renda", xPositionOnScreen + width / 2, yPositionOnScreen + 30);

            Utility.drawTextWithShadow(b, $"Renda Acumulada: {ModEntry.VaultData.IncomePool}g", Game1.dialogueFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 80), Color.LightGreen);
            Utility.drawTextWithShadow(b, "Dividir em:", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 140), Game1.textColor);

            splitCountBox.Draw(b);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), splitButton.bounds.X, splitButton.bounds.Y, splitButton.bounds.Width, splitButton.bounds.Height, Color.White, 4f, false);
            Utility.drawTextWithShadow(b, "Simular", Game1.smallFont, new Vector2(splitButton.bounds.X + 20, splitButton.bounds.Y + 12), Game1.textColor);

            foreach (var slot in slots)
            {
                string label = $"Parte {slot.Index + 1}: {amountPerSlot}g  -> ";
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(slot.X, slot.Y + 10), Game1.textColor);
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), slot.ButtonBounds.X, slot.ButtonBounds.Y, slot.ButtonBounds.Width, slot.ButtonBounds.Height, Color.LightBlue, 4f, false);
                string targetName = slot.SelectedTarget == null ? "O Cofre" : slot.SelectedTarget.Name;
                Utility.drawTextWithShadow(b, targetName, Game1.smallFont, new Vector2(slot.ButtonBounds.X + 15, slot.ButtonBounds.Y + 10), Color.Blue);
            }

            if (slots.Count > 0)
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), confirmDistributeButton.bounds.X, confirmDistributeButton.bounds.Y, confirmDistributeButton.bounds.Width, confirmDistributeButton.bounds.Height, Color.LightGreen, 4f, false);
                Utility.drawTextWithShadow(b, "DISTRIBUIR", Game1.dialogueFont, new Vector2(confirmDistributeButton.bounds.X + 25, confirmDistributeButton.bounds.Y + 10), Color.White);
            }
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
}