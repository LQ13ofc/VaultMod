# 💰 Vault Mod

A comprehensive, shared gold management system for Stardew Valley. Deposit, withdraw, track income, and distribute farm earnings with full multiplayer support.

**Requires SMAPI.**

---

## ✨ Features

* **Per-Save Vault Balance:** Gold balance and transaction history are now stored **per-save file** (world-specific), ensuring data integrity across different games.
* **Shared Gold Balance:** A central, shared vault accessible by all players in a multiplayer session.
* **Deposit & Withdraw:** Simple UI to move money between your wallet and the vault.
* **Automatic Income Capture:** Automatically collects all gold from the Shipping Bin at the end of the day into a separate **Income Pool**.
* **Income Distribution Menu:** Use the Income Pool to simulate and distribute earnings evenly among players, or re-deposit into the main vault balance.
* **Transaction History:** Keeps a detailed, limited history (configurable limit) of all deposits, withdrawals, and income events.
* **Configurable Access:** Restrict high-privilege actions (like withdrawing) to the host player only.
* **GMCM Support:** Full in-game configuration via Generic Mod Config Menu.

---

## ⚙️ How to Use

1.  **Open the Menu:** Press the default hotkey, **F5**, (or your custom key) to open the Vault Main Menu.
2.  **Vault:** Deposit or withdraw funds from the main balance.
3.  **History:** View the transaction history for the current farm save.
4.  **Income:** Access the pool of money collected from the Shipping Bin and manage its distribution.

### Income Distribution

All daily farm earnings are automatically placed into the **Income Pool**.

1.  Navigate to the **Income** tab.
2.  Enter the number of **splits** you want (e.g., `2` for two players, or `3` for three equal shares).
3.  Click **Simulate** to see how much gold each share receives.
4.  Click on the share's name to cycle the recipient between:
    * An Online Player (e.g., `Farmer Name`)
    * The **Vault** (re-deposit into the main balance).
5.  Click **Distribute** to finalize the payout.

---

## 📥 Installation

1.  **Install SMAPI.**
2.  Download the latest release of the **Vault Mod**.
3.  Unzip the file and place the `VaultMod` folder into your `Stardew Valley/Mods` directory.
4.  (Optional, but recommended) Install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) to configure the mod in-game.

---

## ⌨️ Configuration (GMCM)

The mod is fully configurable through the [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (GMCM).

| Setting | Default | Description |
| :--- | :--- | :--- |
| **Open Vault Hotkey** | `F5` | The key used to open the main menu. |
| **Capture Shipping Bin Income** | `True` | If true, daily sales from the bin go to the Income Pool instead of your wallet. |
| **Required Access Level** | `Todos` | Restricts powerful actions (like withdrawing gold) to `Host Only` or allows `All` players. |
| **History Limit** | `50` | The maximum number of transactions saved in the history log. |
| **Show Notifications** | `True` | Toggles in-game HUD messages for transactions and income. |

---

## 🛠️ Building from Source

If you want to compile this mod yourself:

1.  Clone the repository: `git clone https://github.com/LQ13ofc/VaultMod.git`
2.  Navigate to the source directory.
3.  Run `dotnet build` to compile the mod into a `VaultMod.dll`.

---

## 🤝 Thanks

* Thanks to **LQ13ofc** for the initial concept and code structure.
* Thanks to **Vinicius** for some of the final concepts.
* Thanks to **LQ13ofc** and the Stardew Modding community for guidance and best practices.
