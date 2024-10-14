## Overview

The **DepositBox** plugin allows players to deposit specific items (e.g., paper) into a dropbox, logging the deposits and removing the items from the game. This can be used to create events or competitions, such as a system where players compete to turn in the most items (similar to the Twitch Rust event where players compete for dog tags).

### Features
- Only designated items can be deposited.
- Logs all deposits for tracking.
- Configurable deposit box skin and item type.
- Prevents non-whitelisted items from being deposited.
- Can be used to run events or competitions where players turn in items for rewards.

Once installed players with permission will be able to create a deposit box using /depositbox as a chat command. They can then place this box down allowing players to make deposits into it. All deposits are logged to the data folder.

## Installation

1. Upload the `DepositBox.cs` file to your Rust server under the `/oxide/plugins/` directory.
2. Restart or reload the server for the plugin to initialize:
   ```bash
   oxide.reload DepositBox
   ```
3. Ensure you grant users permission to use the deposit box:
   ```bash
   oxide.grant user <username> depositbox.place
   ```

## Configuration

Upon the first run, a default configuration file will be generated at `/oxide/config/DepositBox.json`. The configuration contains the following settings:

```json
{
    "DepositItemID": -1779183908,      // The item ID for your deposit unit (default: -1779183908)
    "DepositBoxSkinID": 1641384897   // The skin ID for the deposit box
}
```

You can edit these values directly in the configuration file if needed.

- **DepositItemID**: The Rust item ID of the item that can be deposited (default: paper with ID `-1779183908`).
- **DepositBoxSkinID**: The Rust skin ID applied to the deposit box (default: `1641384897`).

### Customizing the Configuration:

1. Navigate to `/oxide/config/DepositBox.json`.
2. Modify the values as needed.
3. Save the file and reload the plugin:
   ```bash
   oxide.reload DepositBox
   ```

### Important Note on Skin IDs:
- The DepositBoxSkinID is used to differentiate between regular storage containers and deposit boxes. Admins should ensure that they select a skin ID that is not actively available in the skin box to avoid confusion or accidental misuse by players. Using a skin that is easily accessible to players could result in unintended behavior where non-deposit boxes are treated as deposit boxes.

## Permissions

- `depositbox.place`: Grants a player permission to place a deposit box.

To assign this permission, use the following command:
```bash
oxide.grant user <username> depositbox.place
```

## Functionality

### Hooks

- **Init()**: Initializes the plugin, loads the configuration, and registers permissions.
- **OnServerInitialized()**: Scans the server for `StorageContainer` entities and applies the plugin's functionality to deposit boxes.
- **Unload()**: Cleans up and removes deposit box restrictions when the plugin is unloaded.
- **OnEntitySpawned()**: When a `StorageContainer` is spawned, the plugin checks if it’s a deposit box and ensures it follows the required rules.

### Item Handling

- The plugin restricts item deposits to a hardcoded whitelist (currently, only paper can be deposited).
- If an item is not on the whitelist, it will remain in the player's inventory, and only paper will be removed and logged.
- **Logging**: Every time a player deposits paper into the box, the action is logged for administrative tracking.

### Custom Logging

- All paper deposits are logged in the `oxide/data/DepositBoxLog.json` file, structured as follows:
  ```json
  {
    "SteamID": "player_steam_id",
    "amount_deposited": "amount_deposited",
    "Timestamp": "2024-09-22T12:00:00"
  }
  ```
  This allows server administrators to keep track of deposits and monitor player activity.
